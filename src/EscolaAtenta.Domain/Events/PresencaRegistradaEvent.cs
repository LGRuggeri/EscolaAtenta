using EscolaAtenta.Domain.Common;
using EscolaAtenta.Domain.Enums;

namespace EscolaAtenta.Domain.Events;

/// <summary>
/// Evento de domínio disparado quando uma presença é registrada em uma chamada.
/// 
/// Decisão: Evento granular que permite que outros bounded contexts (ex: relatórios,
/// notificações) reajam ao registro de presença sem acoplamento direto.
/// Nesta fase, é usado principalmente para rastreabilidade e futura integração
/// com sistemas de relatório em tempo real.
/// </summary>
public sealed record PresencaRegistradaEvent(
    Guid ChamadaId,
    Guid AlunoId,
    Guid TurmaId,
    StatusPresenca Status
) : IDomainEvent
{
    public DateTimeOffset OcorridoEm { get; } = DateTimeOffset.UtcNow;
}
