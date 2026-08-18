using EscolaAtenta.Application.Alunos.DTOs;
using EscolaAtenta.Application.Alunos.Queries;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EscolaAtenta.Application.Alunos.Handlers;

public class ObterHistoricoTurmasAlunoHandler : IRequestHandler<ObterHistoricoTurmasAlunoQuery, IEnumerable<HistoricoTurmaAlunoDto>>
{
    private readonly AppDbContext _context;
    private readonly ILogger<ObterHistoricoTurmasAlunoHandler> _logger;

    public ObterHistoricoTurmasAlunoHandler(AppDbContext context, ILogger<ObterHistoricoTurmasAlunoHandler> logger)
    {
        _context = context;
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
