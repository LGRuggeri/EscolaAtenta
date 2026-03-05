using MediatR;

namespace EscolaAtenta.Application.Chamadas.Commands;

// ── Resultado ────────────────────────────────────────────────────────────────

public record SyncPushResult(int RegistrosSincronizados, int AlertasGerados);

// ── Command principal ────────────────────────────────────────────────────────

/// <summary>
/// Recebe o delta de registros de presença criados offline pelo WatermelonDB.
/// O celular envia apenas os registros "sujos" (sincronizado == false),
/// agrupados por turma, para serem persistidos no PostgreSQL.
/// </summary>
public record SyncPushCommand(
    SyncChanges Changes,
    long LastPulledAt
) : IRequest<SyncPushResult>;

// ── Estrutura do Delta ───────────────────────────────────────────────────────

public class SyncChanges
{
    public SyncTableData<RegistroPresencaSyncDto> RegistrosPresenca { get; set; } = new();
}

public class SyncTableData<T>
{
    public List<T> Created { get; set; } = [];
    public List<T> Updated { get; set; } = [];
    public List<string> Deleted { get; set; } = [];
}

// ── DTO unitário de presença vindo do SQLite ─────────────────────────────────

/// <summary>
/// Representa um registro de presença gerado offline pelo WatermelonDB.
///
/// Atenção:
/// - Id é alfanumérico (gerado pelo WatermelonDB, ex: "abc123xyz").
/// - AlunoId e TurmaId são Guids reais do PostgreSQL, sincronizados previamente.
/// - Data é um Unix timestamp em milissegundos (epoch ms do SQLite).
/// - Status é string: "Presente", "Falta" ou "Atraso".
/// </summary>
public class RegistroPresencaSyncDto
{
    public string Id { get; set; } = string.Empty;
    public Guid AlunoId { get; set; }
    public Guid TurmaId { get; set; }
    public long Data { get; set; }
    public string Status { get; set; } = string.Empty;
}
