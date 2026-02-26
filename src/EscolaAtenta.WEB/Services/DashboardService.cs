// Serviço para consumo da API de Dashboard
using System.Net.Http.Json;
using EscolaAtenta.WEB.Models;

namespace EscolaAtenta.WEB.Services;

/// <summary>
/// Serviço para consumir a API de Dashboard.
/// </summary>
public class DashboardService
{
    private readonly HttpClient _httpClient;

    public DashboardService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Obtém lista de alunos com faltas.
    /// </summary>
    public async Task<IReadOnlyList<AlunoComFaltasDto>?> GetAlunosComFaltasAsync(Guid? turmaId = null)
    {
        var url = turmaId.HasValue 
            ? $"api/v1/dashboard/alunos-com-faltas?turmaId={turmaId}" 
            : "api/v1/dashboard/alunos-com-faltas";
            
        return await _httpClient.GetFromJsonAsync<IReadOnlyList<AlunoComFaltasDto>>(url);
    }

    /// <summary>
    /// Obtém lista de turmas para dropdown.
    /// </summary>
    public async Task<IReadOnlyList<TurmaDto>?> GetTurmasAsync()
    {
        return await _httpClient.GetFromJsonAsync<IReadOnlyList<TurmaDto>>("api/v1/turmas");
    }
}
