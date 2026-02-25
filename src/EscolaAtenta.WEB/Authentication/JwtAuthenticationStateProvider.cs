// AuthenticationStateProvider customizado para Blazor WASM
// Lê o JWT do localStorage e cria um AuthenticationState baseado nos claims do token
// O token é anexado automaticamente ao cabeçalho Authorization em todas as requisições HTTP

using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace EscolaAtenta.WEB.Authentication;

/// <summary>
/// Constants para chaves do localStorage
/// </summary>
public static class AuthConstants
{
    public const string TokenKey = "authToken";
    public const string UserNameKey = "userName";
}

/// <summary>
/// AuthenticationStateProvider que persiste o JWT no localStorage
/// Decisão: localStorage é mais simples que IndexedDB para este caso de uso
/// Em produção com alta segurança, considere usar httpOnly cookies
/// </summary>
public class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IJSRuntime _jsRuntime;
    private ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

    public JwtAuthenticationStateProvider(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Retorna o estado de autenticação atual
    /// </summary>
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await GetTokenAsync();
        
        if (string.IsNullOrEmpty(token))
        {
            return new AuthenticationState(_anonymous);
        }

        var claims = ParseClaimsFromJwt(token);
        var identity = new ClaimsIdentity(claims, "jwt");
        var user = new ClaimsPrincipal(identity);

        return new AuthenticationState(user);
    }

    /// <summary>
    /// Called quando o usuario faz login - armazena o token no localStorage
    /// </summary>
    public async Task MarkUserAsAuthenticated(string token)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", AuthConstants.TokenKey, token);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    /// <summary>
    /// Obtem o email do usuario dos claims do JWT
    /// </summary>
    public async Task<string?> GetUserEmailAsync()
    {
        var token = await GetTokenAsync();
        if (string.IsNullOrEmpty(token)) return null;

        var claims = ParseClaimsFromJwt(token);
        return claims.FirstOrDefault(c => c.Type == "email" || c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
    }

    /// <summary>
    /// Called quando o usuario faz logout - remove o token do localStorage
    /// </summary>
    public async Task MarkUserAsLoggedOut()
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", AuthConstants.TokenKey);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_anonymous)));
    }

    /// <summary>
    /// Recupera o token do localStorage
    /// </summary>
    private async Task<string?> GetTokenAsync()
    {
        return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", AuthConstants.TokenKey);
    }

    /// <summary>
    /// Parse dos claims do JWT - decodifica o token sem validação
    /// Nota: A validação real é feita pela API, aqui apenas lemos os claims
    /// </summary>
    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        
        // Tenta ler o token - se falhar, retorna claims vazios
        if (!handler.CanReadToken(jwt))
        {
            return Enumerable.Empty<Claim>();
        }

        var token = handler.ReadJwtToken(jwt);
        return token.Claims;
    }
}
