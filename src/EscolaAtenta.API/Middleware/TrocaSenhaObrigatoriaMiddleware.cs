using Microsoft.AspNetCore.Authorization;

namespace EscolaAtenta.API.Middleware;

/// <summary>Marca a única operação autenticada disponível enquanto a troca está pendente.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PermitirTrocaPendenteAttribute : Attribute { }

public sealed class TrocaSenhaObrigatoriaMiddleware
{
    private readonly RequestDelegate _next;
    public TrocaSenhaObrigatoriaMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (context.User.Identity?.IsAuthenticated == true
            && context.User.FindFirst("deve_alterar_senha")?.Value == "true"
            && endpoint?.Metadata.GetMetadata<IAuthorizeData>() is not null
            && endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null
            && endpoint.Metadata.GetMetadata<PermitirTrocaPendenteAttribute>() is null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new {
                code = "troca_senha_obrigatoria",
                detail = "Altere sua senha antes de acessar o sistema."
            });
            return;
        }
        await _next(context);
    }
}
