using EscolaAtenta.Domain.Interfaces;

namespace EscolaAtenta.Application.Tests.Fakes;

public class FakeCurrentUserService : ICurrentUserService
{
    public string UsuarioId { get; init; } = "admin-teste";
    public string Papel { get; init; } = "Administrador";
    public bool EstaAutenticado { get; init; } = true;
}
