namespace EscolaAtenta.Domain.Common;

/// <summary>
/// Marker interface para Domain Events.
/// Domain Events representam fatos que ocorreram no domínio e que outros
/// componentes do sistema podem reagir de forma desacoplada.
/// 
/// Decisão: Herda de INotification do MediatR para permitir despacho via
/// mediator sem criar abstração adicional desnecessária.
/// </summary>
public interface IDomainEvent : MediatR.INotification
{
    /// <summary>
    /// Momento em que o evento ocorreu — sempre em UTC para evitar
    /// ambiguidades de fuso horário em sistemas distribuídos.
    /// </summary>
    DateTimeOffset OcorridoEm { get; }
}
