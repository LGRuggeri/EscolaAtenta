using EscolaAtenta.Application.Turmas.Commands;
using EscolaAtenta.Application.Turmas.DTOs;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Infrastructure.Data;
using MediatR;

namespace EscolaAtenta.Application.Turmas.Handlers;

public class CriarTurmaHandler : IRequestHandler<CriarTurmaCommand, TurmaDto>
{
    private readonly AppDbContext _context;

    public CriarTurmaHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<TurmaDto> Handle(CriarTurmaCommand request, CancellationToken cancellationToken)
    {
        var turma = new Turma(
            id: Guid.NewGuid(),
            nome: request.Nome,
            turno: request.Turno,
            anoLetivo: request.AnoLetivo
        );

        _context.Turmas.Add(turma);
        await _context.SaveChangesAsync(cancellationToken);

        return new TurmaDto(turma.Id, turma.Nome, turma.Turno, turma.AnoLetivo);
    }
}
