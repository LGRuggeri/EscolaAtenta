using EscolaAtenta.Application.Alertas.Commands;
using EscolaAtenta.Infrastructure.Data;
using EscolaAtenta.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Alertas.Handlers;

public class ResolverAlertaHandler : IRequestHandler<ResolverAlertaCommand, bool>
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ResolverAlertaHandler(AppDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(ResolverAlertaCommand request, CancellationToken cancellationToken)
    {
        var alerta = await _context.AlertasEvasao.FirstOrDefaultAsync(a => a.Id == request.AlertaId, cancellationToken);
        
        if (alerta == null)
        {
            return false;
        }

        if (!Guid.TryParse(_currentUserService.UsuarioId, out var usuarioId))
        {
            throw new UnauthorizedAccessException("Usuário inválido ou não autenticado.");
        }

        alerta.MarcarComoResolvido(usuarioId, request.Justificativa);

        _context.AlertasEvasao.Update(alerta);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
