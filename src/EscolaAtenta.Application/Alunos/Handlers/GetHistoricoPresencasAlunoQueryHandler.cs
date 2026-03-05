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
        // SEGURANÇA: Valida que o aluno existe antes de retornar lista vazia
        var alunoExiste = await _context.Alunos.AnyAsync(a => a.Id == request.AlunoId, cancellationToken);
        if (!alunoExiste)
            throw new KeyNotFoundException($"Aluno com ID '{request.AlunoId}' não encontrado.");

        // TODO: [IDOR] Quando existir a tabela UsuarioTurma, adicionar validação de ownership:
        // var aluno = await _context.Alunos.AsNoTracking().FirstOrDefaultAsync(a => a.Id == request.AlunoId);
        // if (!await _context.UsuarioTurmas.AnyAsync(ut => ut.TurmaId == aluno.TurmaId && ut.UsuarioId == Guid.Parse(_currentUser.UsuarioId)))
        //     throw new KeyNotFoundException($"Aluno com ID '{request.AlunoId}' não encontrado.");

        _logger.LogInformation(
            "[AUDITORIA] Consulta histórico de presenças — AlunoId={AlunoId} UsuarioId={UsuarioId}",
            request.AlunoId, _currentUser.UsuarioId);

        var historico = await _context.RegistrosPresenca
            .Where(r => r.AlunoId == request.AlunoId)
            .OrderByDescending(r => r.Chamada.DataHora)
            .Select(r => new HistoricoPresencaDto(
                r.Chamada.DataHora.UtcDateTime,
                r.Status.ToString(),
                null // Future-proofing: the current entity doesn't have Justificativa mapped in DB
            ))
            .ToListAsync(cancellationToken);

        return historico;
    }
}
