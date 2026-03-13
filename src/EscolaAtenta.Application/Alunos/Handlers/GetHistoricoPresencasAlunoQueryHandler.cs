using EscolaAtenta.Application.Alunos.DTOs;
using EscolaAtenta.Application.Alunos.Queries;
using EscolaAtenta.Domain.Interfaces;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EscolaAtenta.Application.Alunos.Handlers;

public class GetHistoricoPresencasAlunoQueryHandler : IRequestHandler<GetHistoricoPresencasAlunoQuery, IEnumerable<HistoricoPresencaDto>>
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<GetHistoricoPresencasAlunoQueryHandler> _logger;

    public GetHistoricoPresencasAlunoQueryHandler(
        AppDbContext context,
        ICurrentUserService currentUser,
        ILogger<GetHistoricoPresencasAlunoQueryHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<IEnumerable<HistoricoPresencaDto>> Handle(GetHistoricoPresencasAlunoQuery request, CancellationToken cancellationToken)
    {
        // Resolve GUID real: aceita tanto GUID direto quanto ID local do WatermelonDB
        Guid alunoGuid;
        if (!Guid.TryParse(request.AlunoIdOuExterno, out alunoGuid))
        {
            // ID local do WatermelonDB — resolve via SyncLog
            var syncLog = await _context.SyncLogs
                .Where(s => s.IdExterno == request.AlunoIdOuExterno && s.TabelaOrigem == "alunos")
                .Select(s => s.EntidadeId)
                .FirstOrDefaultAsync(cancellationToken);

            if (syncLog == Guid.Empty)
                return Enumerable.Empty<HistoricoPresencaDto>();

            alunoGuid = syncLog;
        }

        var alunoExiste = await _context.Alunos.AnyAsync(a => a.Id == alunoGuid, cancellationToken);
        if (!alunoExiste)
            return Enumerable.Empty<HistoricoPresencaDto>();

        _logger.LogInformation(
            "[AUDITORIA] Consulta histórico de presenças — AlunoId={AlunoId}",
            alunoGuid);

        var limite = DateTime.UtcNow.AddDays(-request.Dias);

        // SQLite não suporta ORDER BY em DateTimeOffset — filtra e ordena em memória
        var historico = await _context.RegistrosPresenca
            .Where(r => r.AlunoId == alunoGuid)
            .Select(r => new HistoricoPresencaDto(
                r.Chamada.DataHora.UtcDateTime,
                r.Status.ToString(),
                null
            ))
            .ToListAsync(cancellationToken);

        return historico
            .Where(h => h.DataDaChamada >= limite)
            .OrderByDescending(h => h.DataDaChamada);
    }
}
