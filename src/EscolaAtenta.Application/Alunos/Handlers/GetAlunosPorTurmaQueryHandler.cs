using EscolaAtenta.Application.Alunos.DTOs;
using EscolaAtenta.Application.Alunos.Queries;
using EscolaAtenta.Domain.Interfaces;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EscolaAtenta.Application.Alunos.Handlers;

public class GetAlunosPorTurmaQueryHandler : IRequestHandler<GetAlunosPorTurmaQuery, IReadOnlyList<AlunoDto>>
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<GetAlunosPorTurmaQueryHandler> _logger;

    public GetAlunosPorTurmaQueryHandler(
        AppDbContext context,
        ICurrentUserService currentUser,
        ILogger<GetAlunosPorTurmaQueryHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AlunoDto>> Handle(GetAlunosPorTurmaQuery request, CancellationToken cancellationToken)
    {
        // SEGURANÇA: Valida que a turma existe antes de retornar lista vazia
        // Distingue "turma vazia" de "turma inexistente" — previne enumeração de IDs
        var turmaExiste = await _context.Turmas.AnyAsync(t => t.Id == request.TurmaId, cancellationToken);
        if (!turmaExiste)
            throw new KeyNotFoundException($"Turma com ID '{request.TurmaId}' não encontrada.");

        // TODO: [IDOR] Quando existir a tabela UsuarioTurma (vínculo Professor → Turma),
        // adicionar validação de ownership:
        // if (!await _context.UsuarioTurmas.AnyAsync(ut => ut.TurmaId == request.TurmaId && ut.UsuarioId == Guid.Parse(_currentUser.UsuarioId)))
        //     throw new KeyNotFoundException($"Turma com ID '{request.TurmaId}' não encontrada."); // 404 para não revelar existência

        _logger.LogInformation(
            "[AUDITORIA] Consulta alunos por turma — TurmaId={TurmaId} UsuarioId={UsuarioId}",
            request.TurmaId, _currentUser.UsuarioId);

        var alunos = await _context.Alunos
            .AsNoTracking()
            .Where(a => a.TurmaId == request.TurmaId)
            .OrderBy(a => a.Nome)
            .Select(a => new AlunoDto(
                a.Id,
                a.Nome,
                a.Matricula,
                a.TurmaId,
                a.FaltasConsecutivasAtuais,
                a.FaltasNoTrimestre,
                a.TotalFaltas,
                a.AtrasosNoTrimestre))
            .ToListAsync(cancellationToken);

        return alunos;
    }
}
