using EscolaAtenta.Application.Alunos.DTOs;
using EscolaAtenta.Application.Alunos.Queries;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Interfaces;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EscolaAtenta.Application.Alunos.Handlers;

public class ObterHistoricoTurmasAlunoHandler : IRequestHandler<ObterHistoricoTurmasAlunoQuery, IEnumerable<HistoricoTurmaAlunoDto>>
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ObterHistoricoTurmasAlunoHandler> _logger;

    public ObterHistoricoTurmasAlunoHandler(
        AppDbContext context,
        ICurrentUserService currentUser,
        ILogger<ObterHistoricoTurmasAlunoHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<IEnumerable<HistoricoTurmaAlunoDto>> Handle(ObterHistoricoTurmasAlunoQuery request, CancellationToken cancellationToken)
    {
        Guid alunoGuid;
        if (!Guid.TryParse(request.AlunoIdOuExterno, out alunoGuid))
        {
            var syncLog = await _context.SyncLogs
                .AsNoTracking()
                .Where(s => s.IdExterno == request.AlunoIdOuExterno && s.TabelaOrigem == "alunos")
                .Select(s => s.EntidadeId)
                .FirstOrDefaultAsync(cancellationToken);

            if (syncLog == Guid.Empty)
                return Enumerable.Empty<HistoricoTurmaAlunoDto>();

            alunoGuid = syncLog;
        }

        // IDOR: apenas administradores ou usuários vinculados a alguma turma do histórico
        // do aluno podem consultar seu histórico de turmas.
        if (_currentUser.Papel != nameof(PapelUsuario.Administrador)
            && Guid.TryParse(_currentUser.UsuarioId, out var usuarioId))
        {
            var turmasDoHistorico = await _context.AlunosTurmasHistorico
                .AsNoTracking()
                .Where(h => h.AlunoId == alunoGuid)
                .Select(h => h.TurmaId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var temVinculo = await _context.UsuarioTurmas
                .AsNoTracking()
                .AnyAsync(ut => ut.UsuarioId == usuarioId && turmasDoHistorico.Contains(ut.TurmaId), cancellationToken);

            if (!temVinculo)
                return Enumerable.Empty<HistoricoTurmaAlunoDto>();
        }

        var historico = await _context.AlunosTurmasHistorico
            .AsNoTracking()
            .Where(h => h.AlunoId == alunoGuid)
            .OrderByDescending(h => h.DataInicio)
            .Select(h => new HistoricoTurmaAlunoDto(
                h.TurmaId,
                h.Turma.Nome,
                h.Turma.Turno,
                h.AnoLetivo,
                h.DataInicio,
                h.DataFim,
                h.Ativa,
                h.Motivo))
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "[AUDITORIA] Consulta histórico de turmas — AlunoId={AlunoId} Registros={Registros}",
            alunoGuid,
            historico.Count);

        return historico;
    }
}
