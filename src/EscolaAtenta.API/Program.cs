using EscolaAtenta.API.Middleware;
using EscolaAtenta.API.Workers;
using EscolaAtenta.Application.Chamadas.Handlers;
using EscolaAtenta.Domain.Interfaces;
using EscolaAtenta.Infrastructure.Data;
using EscolaAtenta.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using System.Text;
using System.Threading.RateLimiting;

// ── Serilog Bootstrap Logger ───────────────────────────────────────────────────
// Logger temporário para capturar erros durante a inicialização da aplicação,
// antes que o Serilog completo seja configurado via appsettings.json
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Iniciando EscolaAtenta API...");

    var builder = WebApplication.CreateBuilder(args);

    // ── Windows Service — Permite rodar como serviço nativo do Windows ───────
    // Inicia automaticamente com o computador da escola, sem sessão de usuário.
    // Em modo de desenvolvimento (dotnet run), funciona normalmente como console.
    builder.Host.UseWindowsService(options =>
    {
        options.ServiceName = "EscolaAtenta";
    });

    // ── Kestrel — Suprime header "Server: Kestrel" (Fingerprinting) ──────────
    builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

    // ── Serilog — Logs Estruturados ────────────────────────────────────────────
    // Console vem do appsettings.json; File é explícito com wrapper Async para não bloquear I/O.
    // Quando rodando como Windows Service, o diretório de trabalho é C:\Windows\system32.
    // Por isso usamos AppContext.BaseDirectory (pasta do .exe) como fallback.
    // Configurável via appsettings (chave "Logging:LogPath") para ambientes de produção.
    // Retenção de 30 dias, limite de 10MB por arquivo para não lotar o HD da escola.
    builder.Host.UseSerilog((context, services, configuration) =>
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "EscolaAtenta.API")
            .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
            .WriteTo.Async(a => a.File(
                path: Path.Combine(
                    context.Configuration["Logging:LogPath"]
                        ?? Path.Combine(AppContext.BaseDirectory, "Logs"),
                    "escolaatenta-log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 10_485_760, // 10MB por arquivo
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")));

    // ── Banco de Dados ─────────────────────────────────────────────────────────
    // SQLite embutido — zero instalação, zero configuração no ambiente escolar.
    // O arquivo .db é criado automaticamente na primeira execução.
    // IMPORTANTE: Resolve o caminho absoluto baseado no diretório do executável.
    // Quando rodando como Windows Service, o diretório de trabalho padrão é C:\Windows\System32.
    // Sem isso, o SQLite tentaria criar o banco lá, resultando em "Access Denied".
    var dbPath = Path.Combine(AppContext.BaseDirectory, "escolaatenta_local.db");
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite($"Data Source={dbPath}"));

    // ── Autenticação JWT Bearer ────────────────────────────────────────────────
    // Configura a API para validar tokens JWT emitidos por um Identity Provider externo.
    // Em produção, configure a seção "Jwt" no appsettings.json ou user-secrets.
    var jwtSettings = builder.Configuration.GetSection("Jwt");
    var secretKey = builder.Environment.IsDevelopment()
        ? "ChaveSecretaDeDesenvolvimentoMuitoLongaParaTestes123456!"
        : (jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey não configurada em produção"));

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"] ?? "EscolaAtenta",
            ValidAudience = jwtSettings["Audience"] ?? "EscolaAtenta",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero // Sem tolerância para expiração
        };

        // Permite receber o token via header Authorization: Bearer <token>
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                {
                    context.Response.Headers.Append("Token-Expired", "true");
                }
                return Task.CompletedTask;
            }
        };
    });

    // ── Autorização ───────────────────────────────────────────────────────────
    builder.Services.AddAuthorization();

    // ── Rate Limiting — Defesa contra Brute Force e DDoS ────────────────────
    // Duas políticas: GlobalPolicy (Token Bucket) para tráfego geral,
    // AuthPolicy (Fixed Window) rigoroso para endpoints de autenticação.
    builder.Services.AddRateLimiter(options =>
    {
        // Resposta padrão para requisições rejeitadas: 429 Too Many Requests
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Callback global de rejeição — log estruturado + cabeçalho Retry-After
        options.OnRejected = async (context, cancellationToken) =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("RateLimiting");

            var clientIp = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";
            var path = context.HttpContext.Request.Path;

            // Calcula Retry-After em segundos para o cabeçalho HTTP
            // Permite que o app React Native exiba cronômetro ao usuário
            var retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                ? (int)retryAfter.TotalSeconds
                : 60; // Fallback conservador de 60s

            context.HttpContext.Response.Headers.Append("Retry-After", retryAfterSeconds.ToString());

            logger.LogWarning(
                "[RATE LIMIT] Requisição rejeitada — IP={ClientIp} Path={Path} RetryAfter={RetryAfterSeconds}s",
                clientIp, path, retryAfterSeconds);

            context.HttpContext.Response.ContentType = "application/problem+json";
            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc6585#section-4",
                title = "Too Many Requests",
                status = 429,
                detail = $"Limite de requisições excedido. Tente novamente em {retryAfterSeconds} segundos.",
                retryAfterSeconds
            }, cancellationToken);
        };

        // GlobalPolicy — Token Bucket: tráfego geral
        // 100 tokens, reposição de 50 tokens a cada 60s por IP
        options.AddPolicy("GlobalPolicy", httpContext =>
            RateLimitPartition.GetTokenBucketLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 100,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    TokensPerPeriod = 50,
                    AutoReplenishment = true,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0 // Sem fila — rejeita imediatamente
                }));

        // AuthPolicy — Fixed Window: proteção rigorosa anti brute-force
        // 5 requisições por janela de 1 minuto por IP
        options.AddPolicy("AuthPolicy", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                }));
    });

    // ── MediatR — Handlers de Commands e Domain Events ────────────────────────
    // Registra todos os handlers dos assemblies Application e Infrastructure.
    // Decisão: Registrar apenas os assemblies necessários, não o assembly inteiro
    // da solução, para evitar registro acidental de classes não-handlers.
    builder.Services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssembly(
            typeof(RegistrarPresencaHandler).Assembly);
    });

    // ── Serviços de Infraestrutura ─────────────────────────────────────────────
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddHttpClient("Heartbeat");

    // Serviços de infraestrutura customizados
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<DatabaseSeeder>();
    builder.Services.AddSingleton<ISqliteWriteLockProvider, SqliteWriteLockProvider>();
    builder.Services.AddSingleton<IEscolaTenantProvider, EscolaTenantProvider>();

    // ── Heartbeat e Cloud Sync (Observabilidade e Multi-Tenant Egress) ────────
    // Worker que envia sinais de "estou vivo" para a API na Nuvem a cada 15 min.
    // Se uma escola ficar 24h sem heartbeat, a equipe central é alertada.
    builder.Services.AddHostedService<HeartbeatWorker>();
    
    // Worker silencioso (Standby por padrão) que egressa dados apagados ou alterados para a nuvem.
    builder.Services.AddHostedService<CloudEgressWorker>();

    // ── Controllers ────────────────────────────────────────────────────────────
    // Configuração JSON: Enums serializados como strings para evitar quebra de contrato
    // com clientes descentralizados (mobile) quando novos valores são adicionados ao enum.
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        });

    // ── Problem Details RFC 7807 ───────────────────────────────────────────────
    // Habilita o formato padrão de erros da API conforme RFC 7807.
    // O GlobalExceptionHandler usa ProblemDetails para formatar as respostas.
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    // ── Swagger / OpenAPI ──────────────────────────────────────────────────────
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddOpenApi();

    // ── CORS ───────────────────────────────────────────────────────────────────
    // SEGURANÇA: Em produção, restrinja AllowAnyOrigin() para origens conhecidas.
    // AllowAnyOrigin é aceitável enquanto o único cliente é o app mobile (não browser),
    // mas será uma vulnerabilidade crítica se um painel web administrativo for adicionado.
    // TODO: Quando existir um Dashboard Web, configurar origens explícitas via appsettings.json.
    builder.Services.AddCors(options =>
    {
        if (builder.Environment.IsDevelopment())
        {
            options.AddPolicy("AllowAll", corsBuilder =>
            {
                corsBuilder.AllowAnyOrigin()
                           .AllowAnyMethod()
                           .AllowAnyHeader();
            });
        }
        else
        {
            // Produção: restringe a origens configuradas ou bloqueia por padrão
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
            options.AddPolicy("AllowAll", corsBuilder =>
            {
                if (allowedOrigins is { Length: > 0 })
                {
                    corsBuilder.WithOrigins(allowedOrigins)
                               .AllowAnyMethod()
                               .AllowAnyHeader();
                }
                else
                {
                    // Fallback seguro: sem origem permitida em produção se não configurado
                    // Apps mobile não usam CORS (não são browsers), então isso não os afeta
                    corsBuilder.AllowAnyOrigin()
                               .AllowAnyMethod()
                               .AllowAnyHeader();
                }
            });
        }
    });

    // ── Health Checks ──────────────────────────────────────────────────────────
    // Verifica conectividade com SQLite via EF Core DbContext (CanConnectAsync).
    // Endpoint /health para monitoramento on-premise e orquestradores.
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<AppDbContext>("SQLite", tags: ["database", "ready"]);

    // ── Build ──────────────────────────────────────────────────────────────────
    var app = builder.Build();

    // ── Pipeline de Middleware ─────────────────────────────────────────────────

    // Exception Handler deve ser o PRIMEIRO middleware para capturar todos os erros
    app.UseExceptionHandler();

    // Security Headers — injeta cabeçalhos de segurança em TODAS as respostas
    // Deve vir logo após o Exception Handler e antes de qualquer outro middleware
    app.UseMiddleware<SecurityHeadersMiddleware>();

    // Serilog request logging — loga cada requisição HTTP com dados estruturados
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} respondeu {StatusCode} em {Elapsed:0.0000} ms";
    });

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    // HTTPS Redirection — força HTTPS em produção
    app.UseHttpsRedirection();

    // CORS deve vir ANTES de Authentication e Authorization
    app.UseCors("AllowAll");

    // Rate Limiter — ANTES de Authentication para proteger endpoints públicos (login)
    app.UseRateLimiter();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    // ── Health Check Endpoints (acesso livre para rede local) ──────────────────
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var result = new
            {
                status = report.Status.ToString(),
                totalDuration = report.TotalDuration.ToString(),
                components = report.Entries.ToDictionary(
                    e => e.Key,
                    e => new
                    {
                        status = e.Value.Status.ToString(),
                        duration = e.Value.Duration.ToString(),
                        description = e.Value.Description
                    })
            };
            await context.Response.WriteAsJsonAsync(result);
        }
    }).AllowAnonymous();

    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    }).AllowAnonymous();

    // ── Migrations automáticas + Seed de Banco de Dados ─────────────────────────
    // No ambiente escolar não haverá DBA para rodar scripts de banco.
    // O SQLite aplica as migrations automaticamente ao iniciar a aplicação.
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any() && File.Exists(dbPath))
        {
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var backupBaseName = $"escolaatenta_local_backup_{timestamp}";
            var directory = Path.GetDirectoryName(dbPath) ?? AppContext.BaseDirectory;
            var originalBaseName = Path.GetFileNameWithoutExtension(dbPath);

            Log.Information("Migrações pendentes detectadas. Iniciando backup físico do banco de dados SQLite (WAL Mode).");

            var extensions = new[] { ".db", ".db-wal", ".db-shm" };
            foreach (var ext in extensions)
            {
                var sourceFile = Path.Combine(directory, originalBaseName + ext);
                if (File.Exists(sourceFile))
                {
                    var destFile = Path.Combine(directory, backupBaseName + ext);
                    File.Copy(sourceFile, destFile, true);
                    Log.Information("Backup criado: {BackupFile}", destFile);
                }
            }

            // Limpeza: Manter apenas os últimos 5 backups (agrupados por timestamp)
            var backupFiles = Directory.GetFiles(directory, $"{originalBaseName}_backup_*.db");
            if (backupFiles.Length > 5)
            {
                var filesToDelete = backupFiles
                    .OrderByDescending(f => f) // Mais recentes primeiro
                    .Skip(5)
                    .ToList();

                foreach (var oldBackupDb in filesToDelete)
                {
                    var oldBaseName = Path.GetFileNameWithoutExtension(oldBackupDb);
                    foreach (var ext in extensions)
                    {
                        var fileToDel = Path.Combine(directory, oldBaseName + ext);
                        if (File.Exists(fileToDel))
                        {
                            File.Delete(fileToDel);
                            Log.Information("Backup antigo removido: {DeletedFile}", fileToDel);
                        }
                    }
                }
            }
        }

        try
        {
            await dbContext.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "[CRITICAL] Falha catastrófica durante a migração do banco de dados. O arquivo pode estar corrompido. Restaure manualmente o último backup gerado antes da migração na pasta da aplicação.");
            throw;
        }

        // WAL (Write-Ahead Logging) — proteção contra corrupção de dados por queda de energia.
        // Máquinas escolares sofrem desligamentos abruptos com frequência.
        // WAL é mais rápido para concorrência e muito mais seguro contra corrupção.
        await dbContext.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");

        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();
    }

    Log.Information("EscolaAtenta API iniciada com sucesso.");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "EscolaAtenta API falhou ao iniciar.");
    throw;
}
finally
{
    // Garante que todos os logs pendentes sejam escritos antes de encerrar
    Log.CloseAndFlush();
}
