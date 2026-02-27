using MediatR;

namespace EscolaAtenta.Application.Alertas.Commands;

public class ResolverAlertaCommand : IRequest<bool>
{
    public Guid AlertaId { get; set; }
    public string Tratativa { get; set; } = string.Empty;
}
