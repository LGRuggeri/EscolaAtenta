using EscolaAtenta.Domain.Interfaces;
using EscolaAtenta.Application.Common;
using EscolaAtenta.Application.Turmas.DTOs;
using EscolaAtenta.Application.Turmas.Queries;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Turmas.Handlers;

public class GetTurmasQueryHandler : IRequestHandler<GetTurmasQuery, IReadOnlyList<TurmaDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly AppDbContext _context;

    public GetTurmasQueryHandler(AppDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<TurmaDto>> Handle(GetTurmasQuery request, CancellationToken cancellationToken)
    {
        var usuarioId = AutorizacaoUsuario.ExigirIdentidade(_currentUser);
        var administrador = _currentUser.Papel == "Administrador";
        var turmasPermitidas = _context.UsuarioTurmas.Where(ut => ut.UsuarioId == usuarioId).Select(ut => ut.TurmaId);
        var turmas = await _context.Turmas.Where(t => administrador || turmasPermitidas.Contains(t.Id))
            .AsNoTracking()
            .OrderBy(t => t.Nome)
            .Select(t => new TurmaDto(t.Id, t.Nome, t.Turno, t.AnoLetivo))
            .ToListAsync(cancellationToken);

        return turmas;
    }
}
