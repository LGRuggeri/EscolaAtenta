using EscolaAtenta.Application.Alertas.Commands;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Alertas.Handlers;

public class ResolverAlertaHandler : IRequestHandler<ResolverAlertaCommand, bool>
{
    private readonly AppDbContext _context;

    public ResolverAlertaHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(ResolverAlertaCommand request, CancellationToken cancellationToken)
    {
        var alerta = await _context.AlertasEvasao.FirstOrDefaultAsync(a => a.Id == request.AlertaId, cancellationToken);
        
        if (alerta == null)
        {
            return false;
        }

        alerta.MarcarComoResolvido(request.Tratativa);

        _context.AlertasEvasao.Update(alerta);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
