using EscolaAtenta.API.Middleware;
using EscolaAtenta.Application.Chamadas.Handlers;
using EscolaAtenta.Domain.Interfaces;
using EscolaAtenta.Infrastructure.Data;
using EscolaAtenta.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;

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

    // ── Serilog — Logs Estruturados ────────────────────────────────────────────
    // Substitui o ILogger padrão do ASP.NET Core pelo Serilog.
    // Configuração lida do appsettings.json (seção "Serilog").
    // Logs estruturados permitem queries em Seq, Elasticsearch, Grafana Loki, etc.
    builder.Host.UseSerilog((context, services, configuration) =>
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "EscolaAtenta.API")
            .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName));

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
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll",
            corsBuilder =>
            {
                corsBuilder.AllowAnyOrigin()
                           .AllowAnyMethod()
                           .AllowAnyHeader();
            });
    });

    // ── Health Checks ──────────────────────────────────────────────────────────
    // Endpoint /health para monitoramento por load balancers e orquestradores (K8s).
    // Verifica a conectividade com o PostgreSQL.
    builder.Services.AddHealthChecks()
        .AddNpgSql(
            connectionString: builder.Configuration.GetConnectionString("DefaultConnection")!,
            name: "postgresql",
            tags: ["database", "ready"]);

    // ── Build ──────────────────────────────────────────────────────────────────
    var app = builder.Build();

    // ── Pipeline de Middleware ─────────────────────────────────────────────────

    // Exception Handler deve ser o PRIMEIRO middleware para capturar todos os erros
    app.UseExceptionHandler();

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

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    // Health Check endpoints
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });

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
