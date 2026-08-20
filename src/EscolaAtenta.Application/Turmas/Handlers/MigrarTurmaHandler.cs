using EscolaAtenta.Application.Turmas.Commands;
using EscolaAtenta.Application.Turmas.DTOs;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Exceptions;
using EscolaAtenta.Domain.Interfaces;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EscolaAtenta.Application.Turmas.Handlers;

public class MigrarTurmaHandler : IRequestHandler<MigrarTurmaCommand, MigrarTurmaResultadoDto>
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<MigrarTurmaHandler> _logger;

    public MigrarTurmaHandler(AppDbContext context, ICurrentUserService currentUser, ILogger<MigrarTurmaHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<MigrarTurmaResultadoDto> Handle(MigrarTurmaCommand request, CancellationToken cancellationToken)
    {
        // IDOR: migração em lote altera todos os alunos de uma turma; restrita a administradores.
        if (_currentUser.Papel != nameof(PapelUsuario.Administrador))
            throw new DomainException("Apenas administradores podem executar migração em lote de turmas.");

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
                var alunoAtual = aluno;
                if (!alunoAtual.HistoricoTurmas.Any())
                {
                    // Garante que a matrícula retroalimentada faça parte da coleção
                    // do aluno, para que TransferirTurma possa encerrá-la corretamente.
                    var matriculaInicial = new AlunoTurmaHistorico(
                        Guid.NewGuid(),
                        alunoAtual.Id,
                        alunoAtual.TurmaId,
                        turmaOrigem.AnoLetivo,
                        new DateTime(turmaOrigem.AnoLetivo, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        dataFim: null,
                        "Retroalimentação automática");
                    _context.AlunosTurmasHistorico.Add(matriculaInicial);
                    await _context.SaveChangesAsync(cancellationToken);

                    alunoAtual = await _context.Alunos
                        .Include(a => a.HistoricoTurmas)
                        .FirstAsync(a => a.Id == alunoAtual.Id, cancellationToken);
                }

                var novaMatricula = alunoAtual.TransferirTurma(
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
