using EscolaAtenta.Domain.Interfaces;

namespace EscolaAtenta.Application.Tests.Fakes;

public class FakeSqliteWriteLockProvider : ISqliteWriteLockProvider
{
    public Task WaitAsync(CancellationToken ct = default) => Task.CompletedTask;
    public void Release() { }
}
