using EscolaAtenta.Domain.Common;

namespace EscolaAtenta.Domain.Events;

/// <summary>
/// Evento de domínio disparado quando um aluno atinge o limite de faltas
/// configurado para geração de alerta de evasão.
/// 
/// Decisão: O evento carrega apenas os dados necessários para o handler
/// criar o AlertaEvasao — AlunoId e TurmaId. O handler buscará os dados
/// completos do aluno se necessário, evitando serialização desnecessária.
/// 
/// Este evento é despachado pela entidade Aluno via VerificarLimiteFaltas()
/// e processado pelo LimiteFaltasAtingidoHandler na camada Application,
/// que cria o AlertaEvasao de forma desacoplada do fluxo de chamada.
/// </summary>
public sealed record LimiteFaltasAtingidoEvent(
    Guid AlunoId,
    Guid TurmaId,
    string NomeAluno,
    int TotalFaltas,
    int LimiteConfigurado
) : IDomainEvent
{
    /// <summary>Momento em que o limite foi atingido, sempre em UTC.</summary>
    public DateTimeOffset OcorridoEm { get; } = DateTimeOffset.UtcNow;
}
