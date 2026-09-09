using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Interfaces;

namespace EscolaAtenta.Application.Common;

/// <summary>Valida identidade antes de conceder acesso aos dados escolares.</summary>
internal static class AutorizacaoUsuario
{
    public static Guid ExigirIdentidade(ICurrentUserService usuario)
    {
        if (!usuario.EstaAutenticado || !Guid.TryParse(usuario.UsuarioId, out var id) || id == Guid.Empty
            || usuario.Papel is not ("Administrador" or "Supervisao" or "Monitor"))
            throw new UnauthorizedAccessException("Usuário inválido ou não autenticado.");
        return id;
    }

    public static void ExigirAdministrador(ICurrentUserService usuario)
    {
        ExigirIdentidade(usuario);
        if (usuario.Papel != nameof(PapelUsuario.Administrador))
            throw new UnauthorizedAccessException("Somente administradores podem alterar cadastros.");
    }
}
