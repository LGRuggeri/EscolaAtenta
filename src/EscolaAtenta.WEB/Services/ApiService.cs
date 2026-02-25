// Servico de API para comunicacao com o backend
// Encapsula as chamadas HTTP para a API
using System.Net.Http.Json;

namespace EscolaAtenta.WEB.Services;

/// <summary>
/// Response do login da API.
/// </summary>
public record LoginApiResponse(string Token, string Email, string Papel, DateTimeOffset ExpiresAt);

/// <summary>
/// Servico para comunicacao com a API.
/// </summary>
public class ApiService
{
    private readonly HttpClient _httpClient;
    private readonly IToastService _toastService;

    public ApiService(HttpClient httpClient, IToastService toastService)
    {
        _httpClient = httpClient;
        _toastService = toastService;
    }

    /// <summary>
    /// Realiza login na API.
    /// </summary>
    public async Task<LoginApiResponse?> LoginAsync(string email, string senha)
    {
        var request = new { Email = email, Senha = senha };
        
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/v1/auth/login", request);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<LoginApiResponse>();
            }
            
            return null;
        }
        catch (Exception)
        {
            // Erro ja tratado pelo GlobalErrorHandlingHandler
            return null;
        }
    }
}
