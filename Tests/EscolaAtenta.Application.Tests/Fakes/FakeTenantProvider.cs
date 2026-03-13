using EscolaAtenta.Domain.Interfaces;

namespace EscolaAtenta.Application.Tests.Fakes;

public class FakeTenantProvider : IEscolaTenantProvider
{
    public Guid EscolaId { get; init; } = new Guid("00000000-0000-0000-0000-000000000001");
}
