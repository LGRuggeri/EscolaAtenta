using System.Threading;
using System.Threading.Tasks;

namespace EscolaAtenta.Domain.Interfaces;

/// <summary>
/// Provedor global para controle de concorrência de escritas pesadas no SQLite.
/// Por ser um banco single-file (com WAL), escritas simultâneas podem disparar 
/// DbUpdateException (Database is locked). Este serviço serializa em memória 
/// essas chamadas antes que cheguem ao disco.
/// </summary>
public interface ISqliteWriteLockProvider
{
    Task WaitAsync(CancellationToken ct = default);
    void Release();
}
