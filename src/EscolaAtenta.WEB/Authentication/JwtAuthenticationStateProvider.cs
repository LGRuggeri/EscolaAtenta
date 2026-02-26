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
        
        // Garante que a ClaimsIdentity use o padrao oficial da Microsoft para Roles
        // E mapeia a claim "email" (que existe no payload do JWT) para ser o Name
        // Se o Name for nulo, a policy interna TheAuthorizarizationCore do Blazor ignora as Roles por segurança.
        var identity = new ClaimsIdentity(claims, "jwt", "email", ClaimTypes.Role);
        var user = new ClaimsPrincipal(identity);

        // [DIAGNOSTICO]: Validando C# Internals exatamente 
        var claimsDiagnosticos = claims.Select(c => $"{c.Type}: {c.Value}").ToArray();
        Console.WriteLine($"[AUTH-STATE] JWT Parser OK. IsAuthenticated: {identity.IsAuthenticated}, Name: {identity.Name}");
        Console.WriteLine($"[AUTH-STATE] IsInRole(Administrador): {user.IsInRole("Administrador")}");
        foreach (var c in claimsDiagnosticos) { Console.WriteLine($"[CLAIM] {c}"); }

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
        var claims = new List<Claim>();
        var payload = jwt.Split('.')[1];
        var jsonBytes = ParseBase64WithoutPadding(payload);
        var keyValuePairs = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);

        if (keyValuePairs != null)
        {
            var possiveisChavesRole = new[] { "role", "papel", ClaimTypes.Role, "http://schemas.microsoft.com/ws/2008/06/identity/claims/role" };
            var roleEncontrada = false;

            foreach (var chaveDesc in possiveisChavesRole)
            {
                if (keyValuePairs.TryGetValue(chaveDesc, out object? rolesObj) && rolesObj != null)
                {
                    if (rolesObj is System.Text.Json.JsonElement element)
                    {
                        if (element.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var item in element.EnumerateArray())
                            {
                                var parsedRole = item.GetString();
                                if (!string.IsNullOrWhiteSpace(parsedRole))
                                {
                                    claims.Add(new Claim(ClaimTypes.Role, parsedRole));
                                    roleEncontrada = true;
                                }
                            }
                        }
                        else if (element.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            var valorRole = element.GetString();
                            if (!string.IsNullOrWhiteSpace(valorRole))
                            {
                                // Blazor WASM pode ter strings com escape de array stringificado
                                if (valorRole.Trim().StartsWith("["))
                                {
                                    try
                                    {
                                        var parsedRoles = System.Text.Json.JsonSerializer.Deserialize<string[]>(valorRole);
                                        if (parsedRoles != null)
                                        {
                                            foreach (var parsedRole in parsedRoles)
                                            {
                                                claims.Add(new Claim(ClaimTypes.Role, parsedRole));
                                                roleEncontrada = true;
                                            }
                                        }
                                    }
                                    catch 
                                    {
                                        // Fallback se não for um JSON array stringificado
                                        claims.Add(new Claim(ClaimTypes.Role, valorRole));
                                        roleEncontrada = true;
                                    }
                                }
                                else
                                {
                                    claims.Add(new Claim(ClaimTypes.Role, valorRole));
                                    roleEncontrada = true;
                                }
                            }
                        }
                        keyValuePairs.Remove(chaveDesc);
                    }
                    else
                    {
                        // Fallback pre-JsonElement ou outro tipo
                        var valorRole = rolesObj.ToString();
                        if (!string.IsNullOrWhiteSpace(valorRole))
                        {
                            if (valorRole.Trim().StartsWith("["))
                            {
                                try
                                {
                                    var parsedRoles = System.Text.Json.JsonSerializer.Deserialize<string[]>(valorRole);
                                    if (parsedRoles != null)
                                    {
                                        foreach (var parsedRole in parsedRoles)
                                        {
                                            claims.Add(new Claim(ClaimTypes.Role, parsedRole));
                                            roleEncontrada = true;
                                        }
                                    }
                                }
                                catch
                                {
                                    claims.Add(new Claim(ClaimTypes.Role, valorRole));
                                    roleEncontrada = true;
                                }
                            }
                            else
                            {
                                claims.Add(new Claim(ClaimTypes.Role, valorRole));
                                roleEncontrada = true;
                            }
                            keyValuePairs.Remove(chaveDesc);
                        }
                    }
                }
            }

            foreach (var kvp in keyValuePairs)
            {
                if (kvp.Key == "papel") continue; 
                
                string claimValue = string.Empty;
                if (kvp.Value is System.Text.Json.JsonElement elementProp)
                {
                    claimValue = elementProp.ValueKind == System.Text.Json.JsonValueKind.String ? 
                        elementProp.GetString() ?? "" : 
                        elementProp.GetRawText();
                }
                else
                {
                    claimValue = kvp.Value?.ToString() ?? "";
                }
                
                // Ignorar claims vazias para manter o token limpo
                if(!string.IsNullOrEmpty(claimValue))
                    claims.Add(new Claim(kvp.Key, claimValue));
            }
        }
        
        return claims;
    }

    private static byte[] ParseBase64WithoutPadding(string base64)
    {
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64.Replace('-', '+').Replace('_', '/'));
    }
}
