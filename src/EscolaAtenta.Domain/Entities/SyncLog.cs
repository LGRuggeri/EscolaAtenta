namespace EscolaAtenta.Domain.Entities;

/// <summary>
/// Registro de idempotência para sincronização offline-first.
/// Cada IdExterno corresponde ao ID alfanumérico gerado pelo WatermelonDB no celular.
/// Impede que o mesmo registro offline seja processado duas vezes pelo backend.
/// EntidadeId mapeia de volta ao Guid do PostgreSQL — essencial para processar Updates.
/// </summary>
public class SyncLog
{
    public Guid Id { get; set; }

    /// <summary>
    /// ID gerado pelo WatermelonDB (alfanumérico, ex: "abc123xyz").
    /// Indexado para busca rápida durante a verificação de idempotência.
    /// </summary>
    public string IdExterno { get; set; } = string.Empty;

    /// <summary>
    /// ID da entidade correspondente no PostgreSQL (ex: RegistroPresenca.Id).
    /// Usado para localizar o registro no banco ao processar Updates do WatermelonDB.
    /// </summary>
    public Guid EntidadeId { get; set; }

    /// <summary>
    /// Nome da tabela de origem no WatermelonDB (ex: "registros_presenca").
    /// </summary>
    public string TabelaOrigem { get; set; } = string.Empty;

    public DateTimeOffset SincronizadoEm { get; set; }
}
