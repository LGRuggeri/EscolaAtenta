using EscolaAtenta.Domain.Interfaces;
using Microsoft.AspNetCore.Http;

namespace EscolaAtenta.Infrastructure.Services;

/// <summary>
/// Implementação de ICurrentUserService que extrai o usuário do HttpContext.
/// 
/// Decisão: Usa IHttpContextAccessor para acessar o contexto da requisição atual.
/// Em ambientes sem contexto HTTP (jobs, testes), retorna "sistema" como fallback.
/// 
/// O claim "sub" (subject) é o padrão JWT para identificador do usuário.
/// Fallback para "nameidentifier" para compatibilidade com ASP.NET Identity.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Retorna o identificador do usuário autenticado.
    /// Prioridade: claim "sub" > claim "nameidentifier" > "sistema" (fallback).
    /// </summary>
    public string UsuarioId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;

            if (user?.Identity?.IsAuthenticated != true)
                return "sistema";

            // Padrão JWT (OpenID Connect)
            var sub = user.FindFirst("sub")?.Value;
            if (!string.IsNullOrEmpty(sub))
                return sub;

            // Compatibilidade com ASP.NET Identity
            var nameId = user.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            return nameId ?? "sistema";
        }
    }

    public bool EstaAutenticado =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;
}
