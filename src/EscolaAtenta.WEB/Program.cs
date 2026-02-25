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
var apiBaseUrl = appSettings.GetValue<string>("BaseUrl") ?? "http://localhost:5000";

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
// Configura o HttpClient com a URL da API e o handler de erros global
builder.Services.AddScoped(sp => 
{
    var toastService = sp.GetRequiredService<IToastService>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var httpClient = new HttpClient 
    { 
        BaseAddress = new Uri(apiBaseUrl) 
    };
    
    // Adiciona o handler de tratamento de erros global
    var delegatingHandler = new GlobalErrorHandlingHandler(toastService, loggerFactory.CreateLogger<GlobalErrorHandlingHandler>());
    httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    
    return httpClient;
});

// Registra o ApiService para comunicação com o backend
builder.Services.AddScoped<ApiService>();

await builder.Build().RunAsync();
