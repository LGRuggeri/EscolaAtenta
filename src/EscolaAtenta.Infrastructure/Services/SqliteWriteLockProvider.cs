using System.Threading;
using System.Threading.Tasks;
using EscolaAtenta.Domain.Interfaces;

namespace EscolaAtenta.Infrastructure.Services;

/// <summary>
/// Implementação Singleton do lock global de escrita para o SQLite.
/// Emprega um SemaphoreSlim(1, 1) para enfileirar as transações em memória.
/// </summary>
public class SqliteWriteLockProvider : ISqliteWriteLockProvider
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task WaitAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
    }

    public void Release()
    {
        _semaphore.Release();
    }
}
