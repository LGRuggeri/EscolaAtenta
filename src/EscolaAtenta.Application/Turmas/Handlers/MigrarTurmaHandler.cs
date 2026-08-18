using EscolaAtenta.Application.Turmas.Commands;
using EscolaAtenta.Application.Turmas.DTOs;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Exceptions;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EscolaAtenta.Application.Turmas.Handlers;

public class MigrarTurmaHandler : IRequestHandler<MigrarTurmaCommand, MigrarTurmaResultadoDto>
{
    private readonly AppDbContext _context;
    private readonly ILogger<MigrarTurmaHandler> _logger;

    public MigrarTurmaHandler(AppDbContext context, ILogger<MigrarTurmaHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<MigrarTurmaResultadoDto> Handle(MigrarTurmaCommand request, CancellationToken cancellationToken)
    {
        if (request.TurmaOrigemId == request.TurmaDestinoId)
            throw new DomainException("A turma de origem e destino devem ser diferentes.");

        var turmaOrigem = await _context.Turmas.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TurmaOrigemId, cancellationToken);
        if (turmaOrigem == null)
            throw new DomainException("A turma de origem não existe.");

        var turmaDestino = await _context.Turmas.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TurmaDestinoId, cancellationToken);
        if (turmaDestino == null)
            throw new DomainException("A turma de destino não existe.");

        var alunos = await _context.Alunos
            .Include(a => a.HistoricoTurmas)
            .Where(a => a.TurmaId == request.TurmaOrigemId)
            .ToListAsync(cancellationToken);

        var erros = new List<string>();
        int transferidos = 0;
        int ignorados = 0;

        foreach (var aluno in alunos)
        {
            try
            {
                if (!aluno.HistoricoTurmas.Any())
                {
                    var matriculaInicial = new AlunoTurmaHistorico(
                        Guid.NewGuid(),
                        aluno.Id,
                        aluno.TurmaId,
                        turmaOrigem.AnoLetivo,
                        new DateTime(turmaOrigem.AnoLetivo, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        dataFim: null,
                        "Retroalimentação automática");
                    _context.AlunosTurmasHistorico.Add(matriculaInicial);
                }

                var novaMatricula = aluno.TransferirTurma(
                    request.TurmaDestinoId,
                    turmaDestino.AnoLetivo,
                    request.DataTransferencia,
                    request.Motivo);

                _context.AlunosTurmasHistorico.Add(novaMatricula);

                transferidos++;
            }
            catch (DomainException ex)
            {
                ignorados++;
                erros.Add($"Aluno {aluno.Id} ({aluno.Nome}): {ex.Message}");
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[AUDITORIA] Migração de turma — Origem={TurmaOrigemId} Destino={TurmaDestinoId} Transferidos={Transferidos} Ignorados={Ignorados}",
            request.TurmaOrigemId,
            request.TurmaDestinoId,
            transferidos,
            ignorados);

        return new MigrarTurmaResultadoDto(transferidos, ignorados, erros.AsReadOnly());
    }
}
