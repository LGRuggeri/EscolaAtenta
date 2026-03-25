using EscolaAtenta.Application.Alunos.Commands;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Interfaces;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EscolaAtenta.Application.Alunos.Handlers;

public class AtualizarAlunoHandler : IRequestHandler<AtualizarAlunoCommand, Unit>
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AtualizarAlunoHandler> _logger;

    public AtualizarAlunoHandler(
        AppDbContext context,
        ICurrentUserService currentUser,
        ILogger<AtualizarAlunoHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Unit> Handle(AtualizarAlunoCommand request, CancellationToken cancellationToken)
    {
        var aluno = await _context.Alunos.FindAsync([request.Id], cancellationToken);

        // SEGURANÇA: Retorna 404 para não expor a existência do ID a um atacante
        if (aluno == null)
            throw new KeyNotFoundException($"Aluno com ID '{request.Id}' não encontrado.");

        // IDOR: Administrador pode alterar qualquer aluno; demais papéis precisam de vínculo com a turma
        if (_currentUser.Papel != nameof(PapelUsuario.Administrador)
            && Guid.TryParse(_currentUser.UsuarioId, out var uid)
            && !await _context.UsuarioTurmas.AnyAsync(
                ut => ut.TurmaId == aluno.TurmaId && ut.UsuarioId == uid, cancellationToken))
        {
            throw new KeyNotFoundException($"Aluno com ID '{request.Id}' não encontrado.");
        }

        // Log de auditoria: rastreia quem alterou qual aluno
        _logger.LogInformation(
            "[AUDITORIA] Aluno atualizado — AlunoId={AlunoId} TurmaId={TurmaId} UsuarioId={UsuarioId} Papel={Papel}",
            request.Id, aluno.TurmaId, _currentUser.UsuarioId, _currentUser.Papel);

        aluno.Atualizar(request.Nome, request.Matricula);

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
