// Servico de API para comunicacao com o backend
// Encapsula as chamadas HTTP para a API
using System.Net.Http.Json;
using EscolaAtenta.WEB.Models;

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

    // --- Turmas ---
    public async Task<IReadOnlyList<TurmaDto>> GetTurmasAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<IReadOnlyList<TurmaDto>>("api/v1/turmas") ?? new List<TurmaDto>();
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"Erro ao buscar turmas: {ex.Message}");
            return new List<TurmaDto>();
        }
    }

    public async Task<TurmaDto?> CriarTurmaAsync(CriarTurmaRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/v1/turmas", request);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<TurmaDto>();
                
            _toastService.ShowError("Erro ao criar turma. Verifique os dados.");
            return null;
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"Erro de comunicação: {ex.Message}");
            return null;
        }
    }

    // --- Alunos ---
    public async Task<IReadOnlyList<AlunoDto>> GetAlunosPorTurmaAsync(Guid turmaId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<IReadOnlyList<AlunoDto>>($"api/v1/alunos/turma/{turmaId}") ?? new List<AlunoDto>();
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"Erro ao buscar alunos: {ex.Message}");
            return new List<AlunoDto>();
        }
    }

    public async Task<AlunoDto?> CriarAlunoAsync(CriarAlunoRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/v1/alunos", request);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<AlunoDto>();
                
            _toastService.ShowError("Erro ao criar aluno. Verifique os dados.");
            return null;
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"Erro de comunicação: {ex.Message}");
            return null;
        }
    }

    // --- Chamadas ---
    public async Task<RealizarChamadaResult?> RealizarChamadaAsync(RealizarChamadaRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/v1/chamadas/realizar", request);
            if (response.IsSuccessStatusCode)
            {
                _toastService.ShowSuccess("Chamada realizada com sucesso!");
                return await response.Content.ReadFromJsonAsync<RealizarChamadaResult>();
            }
                
            var error = await response.Content.ReadAsStringAsync();
            _toastService.ShowError($"Erro ao realizar chamada: {error}");
            return null;
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"Erro de comunicação: {ex.Message}");
            return null;
        }
    }

    // --- Usuários ---
    public async Task<UsuarioCriadoResponse?> CriarUsuarioAsync(CriarUsuarioRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/v1/usuarios", request);
            
            if (response.IsSuccessStatusCode)
            {
                _toastService.ShowSuccess("Usuário criado com sucesso!");
                return await response.Content.ReadFromJsonAsync<UsuarioCriadoResponse>();
            }
            
            // Tenta ler mensagem de erro da API (BadRequest)
            var errorResponse = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            var detalheErro = errorResponse != null && errorResponse.TryGetValue("detail", out var detail) 
                ? detail 
                : "Verifique os dados informados.";
                
            _toastService.ShowError($"Erro ao criar usuário: {detalheErro}");
            return null;
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"Erro de comunicação: {ex.Message}");
            return null;
        }
    }
}
