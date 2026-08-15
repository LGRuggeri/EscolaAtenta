using EscolaAtenta.Application.Chamadas.Queries;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Exceptions;
using EscolaAtenta.Domain.Interfaces;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Chamadas.Handlers;

public class ObterChamadaPorTurmaEDiaHandler : IRequestHandler<ObterChamadaPorTurmaEDiaQuery, ChamadaPorDiaDto?>
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ObterChamadaPorTurmaEDiaHandler(AppDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ChamadaPorDiaDto?> Handle(
        ObterChamadaPorTurmaEDiaQuery request,
        CancellationToken cancellationToken)
    {
        var turmaId = await ResolverTurmaIdAsync(request.TurmaId, cancellationToken);
        if (turmaId == Guid.Empty)
            return null;

        // IDOR: Administrador pode consultar qualquer turma; demais papéis precisam de vínculo
        if (_currentUser.Papel != nameof(PapelUsuario.Administrador)
            && Guid.TryParse(_currentUser.UsuarioId, out var ownerCheck)
            && !await _context.UsuarioTurmas.AnyAsync(
                ut => ut.TurmaId == turmaId && ut.UsuarioId == ownerCheck, cancellationToken))
        {
            throw new DomainException("Você não tem permissão para consultar chamada desta turma.");
        }

        var chamada = (await _context.Chamadas
            .AsNoTracking()
            .Include(c => c.RegistrosPresenca)
            .ThenInclude(r => r.Aluno)
            .Where(c => c.TurmaId == turmaId)
            .ToListAsync(cancellationToken))
            .Where(c => c.DataChamada == request.Data.Date)
            .OrderByDescending(c => c.DataCriacao)
            .ThenBy(c => c.Id)
            .FirstOrDefault();

        if (chamada is null)
            return null;

        var prazoEdicao = chamada.DataCriacao.AddDays(7);
        var podeEditar = DateTimeOffset.UtcNow <= prazoEdicao;

        // Resolve IDs de alunos para IDs locais do WatermelonDB quando existirem SyncLogs.
        var alunoIds = chamada.RegistrosPresenca.Select(r => r.AlunoId).Distinct().ToList();
        var syncLogsAlunos = await _context.SyncLogs
            .AsNoTracking()
            .Where(s => s.TabelaOrigem == "alunos" && alunoIds.Contains(s.EntidadeId))
            .ToDictionaryAsync(s => s.EntidadeId, s => s.IdExterno, cancellationToken);

        return new ChamadaPorDiaDto
        {
            ChamadaId = chamada.Id,
            DataHora = chamada.DataHora,
            ResponsavelId = chamada.ResponsavelId,
            PodeEditar = podeEditar,
            Registros = chamada.RegistrosPresenca
                .Select(r => new RegistroPresencaPorDiaDto
                {
                    AlunoId = syncLogsAlunos.TryGetValue(r.AlunoId, out var idLocal) && !string.IsNullOrEmpty(idLocal)
                        ? idLocal
                        : r.AlunoId.ToString(),
                    NomeAluno = r.Aluno?.Nome ?? string.Empty,
                    Status = r.Status.ToString()
                })
                .ToList()
        };
    }

    /// <summary>
    /// Resolve o identificador da turma. Pode ser o GUID do servidor ou o ID externo
    /// do WatermelonDB para turmas criadas offline e sincronizadas.
    /// </summary>
    private async Task<Guid> ResolverTurmaIdAsync(string turmaId, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(turmaId, out var guid))
            return guid;

        var syncLog = await _context.SyncLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TabelaOrigem == "turmas" && s.IdExterno == turmaId, cancellationToken);

        return syncLog?.EntidadeId ?? Guid.Empty;
    }
}
