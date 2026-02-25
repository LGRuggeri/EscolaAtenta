// DelegatingHandler para interceptar respostas HTTP e tratar erros globalmente
// Desserializa ProblemDetails (RFC 7807) e exibe mensagens via ToastService
// Cores: areia branca (#faf8f3) + dourado (#c9a227)

using System.Net;
using System.Text.Json;
using EscolaAtenta.WEB.Services;
using Microsoft.Extensions.Logging;

namespace EscolaAtenta.WEB.Handlers;

/// <summary>
/// Modelo RFC 7807 Problem Details - formato padrao de erros da API
/// </summary>
public class ProblemDetails
{
    public string? Type { get; set; }
    public string? Title { get; set; }
    public int? Status { get; set; }
    public string? Detail { get; set; }
    public string? Instance { get; set; }
    public Dictionary<string, JsonElement>? Extensions { get; set; }
}

/// <summary>
/// DelegatingHandler que intercepta todas as respostas HTTP da API
/// Detecta erros (4xx, 5xx), tenta parsear RFC 7807 ProblemDetails, e exibe via Toast
/// </summary>
public class GlobalErrorHandlingHandler : DelegatingHandler
{
    private readonly IToastService _toastService;
    private readonly ILogger<GlobalErrorHandlingHandler> _logger;

    public GlobalErrorHandlingHandler(IToastService toastService, ILogger<GlobalErrorHandlingHandler> logger)
    {
        _toastService = toastService;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        // So trata erros HTTP (4xx e 5xx) - sucesso (2xx) passa normalmente
        if (!response.IsSuccessStatusCode)
        {
            await HandleErrorResponseAsync(response);
        }

        return response;
    }

    private async Task HandleErrorResponseAsync(HttpResponseMessage response)
    {
        var statusCode = (int)response.StatusCode;
        var problemDetails = await TryParseProblemDetailsAsync(response);
        
        // Gera mensagem de erro amigavel baseada no status code
        var userMessage = GenerateUserMessage(statusCode, problemDetails, response);
        
        // Log para debugging
        _logger.LogWarning("HTTP {StatusCode}: {Title} - {Detail}", 
            statusCode, 
            problemDetails?.Title ?? response.ReasonPhrase,
            problemDetails?.Detail ?? "Erro desconhecido");

        // Exibe notificação visual ao usuario
        _toastService.ShowError(userMessage);
    }

    /// <summary>
    /// Tenta desserializar o corpo da resposta como ProblemDetails (RFC 7807)
    /// </summary>
    private async Task<ProblemDetails?> TryParseProblemDetailsAsync(HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync();
            
            // Verifica se o conteudo e JSON valido
            if (string.IsNullOrWhiteSpace(content) || !content.TrimStart().StartsWith('{'))
            {
                return null;
            }

            // Tenta deserializar como ProblemDetails
            return JsonSerializer.Deserialize<ProblemDetails>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            // Resposta nao e JSON valido - ignora
            return null;
        }
    }

    /// <summary>
    /// Gera mensagem amigavel para o usuario baseada no status code e ProblemDetails
    /// </summary>
    private string GenerateUserMessage(int statusCode, ProblemDetails? problemDetails, HttpResponseMessage response)
    {
        // Se temos detalhes do ProblemDetails, usa-os
        if (problemDetails?.Detail != null)
        {
            return problemDetails.Detail;
        }

        // Fallback para mensagens genericas baseadas no status code
        return statusCode switch
        {
            // Erros de concurrencia (optimistic locking)
            (int)HttpStatusCode.Conflict => "Registro alterado por outro usuário. Tente novamente.",
            
            // Erros de validacao de dominio
            (int)HttpStatusCode.UnprocessableEntity => problemDetails?.Title ?? "Erro de validação. Verifique os dados informados.",
            
            // Erros de autenticacao
            (int)HttpStatusCode.Unauthorized => "Sessão expirada. Por favor, faça login novamente.",
            (int)HttpStatusCode.Forbidden => "Você não tem permissão para realizar esta ação.",
            
            // Recurso nao encontrado
            (int)HttpStatusCode.NotFound => "Registro não encontrado.",
            
            // Erros de validacao (model state)
            (int)HttpStatusCode.BadRequest => problemDetails?.Title ?? "Dados inválidos.",
            
            // Erros de servidor
            (int)HttpStatusCode.InternalServerError => "Erro interno do servidor. Tente novamente mais tarde.",
            (int)HttpStatusCode.ServiceUnavailable => "Serviço temporariamente indisponível.",
            
            // Default
            _ => $"Erro ({statusCode}): {problemDetails?.Title ?? response.ReasonPhrase}"
        };
    }
}
