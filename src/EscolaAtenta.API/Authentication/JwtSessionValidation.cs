using System.Security.Claims;
using EscolaAtenta.Infrastructure.Data;
using EscolaAtenta.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.API.Authentication;

public static class JwtSessionValidation
{
    public static async Task ValidarAsync(TokenValidatedContext context)
    {
        var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.Principal?.FindFirstValue("sub");
        if (!Guid.TryParse(id, out var usuarioId))
        {
            context.Fail("Sessão inválida.");
            return;
        }
        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var usuario = await db.Usuarios.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == usuarioId, context.HttpContext.RequestAborted);
        if (usuario is null || !usuario.PodeAcessar()
            || context.Principal?.FindFirstValue("versao_sessao") != VersaoSessao.Calcular(usuario))
        {
            // JWTs legados, contas desativadas e estados anteriores exigem nova sessão.
            context.Fail("Sessão revogada. Entre novamente.");
        }
    }
}
