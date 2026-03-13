using EscolaAtenta.Application.Turmas.Commands;
using EscolaAtenta.Domain.Interfaces;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EscolaAtenta.Application.Turmas.Handlers;

public class AtualizarTurmaHandler : IRequestHandler<AtualizarTurmaCommand, Unit>
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AtualizarTurmaHandler> _logger;

    public AtualizarTurmaHandler(
        AppDbContext context,
        ICurrentUserService currentUser,
        ILogger<AtualizarTurmaHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Unit> Handle(AtualizarTurmaCommand request, CancellationToken cancellationToken)
    {
        var turma = await _context.Turmas.FindAsync([request.Id], cancellationToken);

        // SEGURANÇA: Retorna 404 para não expor a existência do ID a um atacante
        if (turma == null)
            throw new KeyNotFoundException($"Turma com ID '{request.Id}' não encontrada.");

        // TODO: [IDOR] Quando existir a tabela UsuarioTurma (vínculo Professor → Turma),
        // adicionar validação de ownership aqui para garantir que só o professor da turma possa alterá-la.

        // Log de auditoria: rastreia quem alterou qual turma
        _logger.LogInformation(
            "[AUDITORIA] Turma atualizada — TurmaId={TurmaId} UsuarioId={UsuarioId} Papel={Papel}",
            request.Id, _currentUser.UsuarioId, _currentUser.Papel);

        turma.Atualizar(request.Nome, request.Turno, request.AnoLetivo);

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
