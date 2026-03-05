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

    /// <summary>
    /// Retorna o papel (role) do usuário autenticado.
    /// Extrai do claim "role" ou ClaimTypes.Role do token JWT.
    /// </summary>
    public string Papel
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;

            if (user?.Identity?.IsAuthenticated != true)
                return string.Empty;

            // Busca claim "role" (padrão JWT customizado)
            var role = user.FindFirst("role")?.Value;
            if (!string.IsNullOrEmpty(role))
                return role;

            // Fallback para ClaimTypes.Role (padrão ASP.NET Identity)
            var claimRole = user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return claimRole ?? string.Empty;
        }
    }

    public bool EstaAutenticado =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;
}
