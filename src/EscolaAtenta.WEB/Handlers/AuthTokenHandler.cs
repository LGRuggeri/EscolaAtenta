// DelegatingHandler que injeta o JWT do localStorage em toda requisição HTTP
// Garante que as rotas [Authorize] da API recebam o Bearer Token automaticamente

using Microsoft.JSInterop;
using EscolaAtenta.WEB.Authentication;

namespace EscolaAtenta.WEB.Handlers;

/// <summary>
/// Intercepta requisições HTTP saindo do Blazor WASM e injeta
/// o cabeçalho Authorization: Bearer {token} usando o JWT
/// armazenado em localStorage.
/// </summary>
public class AuthTokenHandler : DelegatingHandler
{
    private readonly IJSRuntime _jsRuntime;

    public AuthTokenHandler(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Recupera o token do localStorage
        var token = await _jsRuntime.InvokeAsync<string?>(
            "localStorage.getItem", AuthConstants.TokenKey);

        // Se o token existe, injeta no cabeçalho Authorization
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
