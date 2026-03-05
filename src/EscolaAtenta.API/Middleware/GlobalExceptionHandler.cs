using EscolaAtenta.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.API.Middleware;

/// <summary>
/// Handler global de exceções que retorna respostas no formato RFC 7807 (Problem Details).
/// 
/// Mapeamento de exceções para HTTP Status Codes:
/// - DomainException → 422 Unprocessable Entity (violação de regra de negócio)
/// - DbUpdateConcurrencyException → 409 Conflict (conflito de concorrência otimista)
/// - KeyNotFoundException → 404 Not Found
/// - Exception (genérica) → 500 Internal Server Error
/// 
/// Decisão: Usar IExceptionHandler (ASP.NET Core 8+) em vez de middleware customizado.
/// É mais integrado com o pipeline e suporta múltiplos handlers em cadeia.
/// 
/// IMPORTANTE: Em produção, detalhes internos (stack trace) NUNCA são expostos.
/// Em desenvolvimento, o detalhe completo é incluído para facilitar debugging.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail) = MapException(exception);

        // Log estruturado — nível varia conforme a severidade
        if (statusCode >= 500)
        {
            _logger.LogError(exception,
                "Erro interno não tratado: {ExceptionType} - {Message}",
                exception.GetType().Name, exception.Message);
        }
        else
        {
            _logger.LogWarning(
                "Erro de negócio: {ExceptionType} - {Message}",
                exception.GetType().Name, exception.Message);
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        // Em desenvolvimento, inclui informações adicionais para debugging
        if (_environment.IsDevelopment())
        {
            problemDetails.Extensions["exceptionType"] = exception.GetType().FullName;
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        // Retorna true para indicar que a exceção foi tratada
        return true;
    }

    private static (int StatusCode, string Title, string Detail) MapException(Exception exception)
    {
        return exception switch
        {
            // Erro de Login (E-mail/Senha inválidos) → 401
            CredenciaisInvalidasException credEx =>
                (StatusCodes.Status401Unauthorized,
                 "Nao Autorizado",
                 credEx.Message),

            // Violação de regra de negócio do domínio → 422
            DomainException domainEx =>
                (StatusCodes.Status422UnprocessableEntity,
                 "Violação de Regra de Negócio",
                 domainEx.Message),

            // Conflito de concorrência otimista → 409
            DbUpdateConcurrencyException =>
                (StatusCodes.Status409Conflict,
                 "Conflito de Concorrência",
                 "O registro foi modificado por outro usuário. Por favor, recarregue e tente novamente."),

            // Recurso não encontrado → 404
            KeyNotFoundException notFoundEx =>
                (StatusCodes.Status404NotFound,
                 "Recurso Não Encontrado",
                 notFoundEx.Message),

            // Argumento inválido → 400
            ArgumentException argEx =>
                (StatusCodes.Status400BadRequest,
                 "Requisição Inválida",
                 argEx.Message),

            // Erro genérico → 500 (sem expor detalhes internos)
            _ =>
                (StatusCodes.Status500InternalServerError,
                 "Erro Interno do Servidor",
                 "Ocorreu um erro inesperado. Por favor, tente novamente mais tarde.")
        };
    }
}
