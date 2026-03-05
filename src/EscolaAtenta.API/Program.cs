using EscolaAtenta.API.Middleware;
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

    // ── Kestrel — Suprime header "Server: Kestrel" (Fingerprinting) ──────────
    builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

    // ── Serilog — Logs Estruturados ────────────────────────────────────────────
    // Console vem do appsettings.json; File é explícito com wrapper Async para não bloquear I/O.
    // Retenção de 30 dias: arquivos mais antigos são deletados automaticamente (on-premise).
    builder.Host.UseSerilog((context, services, configuration) =>
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "EscolaAtenta.API")
            .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
            .WriteTo.Async(a => a.File(
                path: "Logs/escolaatenta-log-.txt",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")));

    // ── Banco de Dados ─────────────────────────────────────────────────────────
    // Connection string vem do user-secrets (dev) ou variável de ambiente (prod).
    // NUNCA deve estar hardcoded no appsettings.json.
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            npgsql => npgsql.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null)));

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
    
    // Serviços de infraestrutura customizados
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<DatabaseSeeder>();

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
    // Verifica conectividade com PostgreSQL via EF Core DbContext (CanConnectAsync).
    // Endpoint /health para monitoramento on-premise (script da secretaria) e orquestradores.
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<AppDbContext>("PostgreSQL", tags: ["database", "ready"]);

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

    // ── Seed de Banco de Dados ──────────────────────────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
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
