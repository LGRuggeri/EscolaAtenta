using System.Text.Json.Serialization;
using MediatR;

namespace EscolaAtenta.Application.Chamadas.Queries;

// ── Query ────────────────────────────────────────────────────────────────────

/// <summary>
/// Solicita o delta de Turmas e Alunos para sincronização com o WatermelonDB.
/// Se LastPulledAt for 0 ou null, retorna todos os registros ativos (primeiro login).
/// Caso contrário, retorna apenas o que mudou desde o timestamp informado.
/// </summary>
public record SyncPullQuery(long? LastPulledAt) : IRequest<SyncPullResult>;

// ── Resultado (contrato WatermelonDB) ────────────────────────────────────────

public class SyncPullResult
{
    public SyncPullChanges Changes { get; set; } = new();
    public long Timestamp { get; set; }
}

public class SyncPullChanges
{
    public SyncPullTableData<TurmaSyncDto> Turmas { get; set; } = new();
    public SyncPullTableData<AlunoSyncDto> Alunos { get; set; } = new();

    /// <summary>
    /// registros_presenca: sempre vazio no Pull (só sobe via Push).
    /// A propriedade usa snake_case para alinhar com o nome da tabela no WatermelonDB.
    /// </summary>
    [JsonPropertyName("registros_presenca")]
    public SyncPullTableData<object> RegistrosPresenca { get; set; } = new();
}

public class SyncPullTableData<T>
{
    public List<T> Created { get; set; } = [];
    public List<T> Updated { get; set; } = [];
    public List<string> Deleted { get; set; } = [];
}

// ── DTOs com nomes de coluna do WatermelonDB (snake_case) ────────────────────

/// <summary>
/// DTO da Turma no formato esperado pelo WatermelonDB.
/// O `id` será o Guid do PostgreSQL convertido para string — o WatermelonDB
/// o aceita como ID do registro local, garantindo correspondência 1:1.
/// </summary>
public class TurmaSyncDto
{
    public string Id { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string Turno { get; set; } = string.Empty;
}

/// <summary>
/// DTO do Aluno no formato esperado pelo WatermelonDB.
/// turma_id usa snake_case para casar com a coluna do schema SQLite.
/// </summary>
public class AlunoSyncDto
{
    public string Id { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;

    [JsonPropertyName("turma_id")]
    public string TurmaId { get; set; } = string.Empty;
}
