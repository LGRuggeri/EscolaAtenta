using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using EscolaAtenta.WEB;
using EscolaAtenta.WEB.Services;
using EscolaAtenta.WEB.Handlers;
using EscolaAtenta.WEB.Authentication;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ── Configurações ───────────────────────────────────────────────────────────
// Lê a configuração do appsettings.json
var appSettings = builder.Configuration.GetSection("ApiSettings");
var apiBaseUrl = appSettings.GetValue<string>("BaseUrl") ?? "http://localhost:5114";

// ── Autenticação JWT ──────────────────────────────────────────────────────────
// Registra o AuthenticationStateProvider customizado que lê o JWT do localStorage
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();

// Registra o AuthenticationStateProvider principal como o customizado
builder.Services.AddScoped<AuthenticationStateProvider>(sp => 
    sp.GetRequiredService<JwtAuthenticationStateProvider>());

// Servico de notificacoes Toast - registrado como Scoped para ser compartilhado
builder.Services.AddScoped<IToastService, ToastService>();

// ── HttpClient para a API ───────────────────────────────────────────────────
// Pipeline: AuthTokenHandler → GlobalErrorHandlingHandler → HttpClientHandler
// O AuthTokenHandler injeta o Bearer JWT em toda requisição
// O GlobalErrorHandlingHandler trata erros (4xx/5xx) exibindo Toast

builder.Services.AddScoped<AuthTokenHandler>();
builder.Services.AddScoped<GlobalErrorHandlingHandler>();

builder.Services.AddScoped(sp =>
{
    var authHandler = sp.GetRequiredService<AuthTokenHandler>();
    var errorHandler = sp.GetRequiredService<GlobalErrorHandlingHandler>();
    
    // Montar pipeline: AuthToken → ErrorHandling → Network
    authHandler.InnerHandler = errorHandler;
    errorHandler.InnerHandler = new HttpClientHandler();
    
    var httpClient = new HttpClient(authHandler)
    { 
        BaseAddress = new Uri(apiBaseUrl) 
    };
    
    httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    return httpClient;
});

// Registra o ApiService para comunicação com o backend
builder.Services.AddScoped<ApiService>();

// Registra o DashboardService para consumo da API de Dashboard
builder.Services.AddScoped<DashboardService>();

await builder.Build().RunAsync();
