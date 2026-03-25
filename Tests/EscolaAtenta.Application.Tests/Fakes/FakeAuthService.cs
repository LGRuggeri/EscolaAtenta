using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Interfaces;

namespace EscolaAtenta.Application.Tests.Fakes;

/// <summary>
/// IAuthService falso para testes — aceita qualquer senha que contenha "correta".
/// </summary>
public class FakeAuthService : IAuthService
{
    public LoginResult GerarToken(Usuario usuario) =>
        new(
            Token: "fake-jwt-token",
            Email: usuario.Email,
            Papel: usuario.Papel.ToString(),
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1)
        );

    public bool ValidarSenha(string senha, string hashArmazenado) =>
        senha == "senha-correta";

    public string GerarHashSenha(string senha) => $"hash-{senha}";
}
