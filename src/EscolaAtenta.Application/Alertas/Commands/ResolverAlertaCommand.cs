using MediatR;

namespace EscolaAtenta.Application.Alertas.Commands;

public class ResolverAlertaCommand : IRequest<bool>
{
    public Guid AlertaId { get; set; }
    public string Justificativa { get; set; } = string.Empty;
}
