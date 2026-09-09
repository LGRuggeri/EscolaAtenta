using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EscolaAtenta.Domain.Entities;

namespace EscolaAtenta.Infrastructure.Services;

/// <summary>Vincula o JWT ao estado persistido da conta sem expor o hash da senha.</summary>
public static class VersaoSessao
{
    public static string Calcular(Usuario usuario)
    {
        var estado = string.Join("|", usuario.Id.ToString("D"), usuario.HashSenha,
            ((int)usuario.Papel).ToString(CultureInfo.InvariantCulture), usuario.Ativo,
            usuario.DeveAlterarSenha,
            usuario.DataAtualizacao?.UtcTicks.ToString(CultureInfo.InvariantCulture) ?? "inicial");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(estado)));
    }
}
