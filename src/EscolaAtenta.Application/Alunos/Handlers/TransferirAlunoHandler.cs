using EscolaAtenta.Application.Alunos.Commands;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Exceptions;
using EscolaAtenta.Domain.Interfaces;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EscolaAtenta.Application.Alunos.Handlers;

public class TransferirAlunoHandler : IRequestHandler<TransferirAlunoCommand>
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<TransferirAlunoHandler> _logger;

    public TransferirAlunoHandler(
        AppDbContext context,
        ICurrentUserService currentUser,
        ILogger<TransferirAlunoHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task Handle(TransferirAlunoCommand request, CancellationToken cancellationToken)
    {
        var aluno = await _context.Alunos
            .Include(a => a.HistoricoTurmas)
            .FirstOrDefaultAsync(a => a.Id == request.AlunoId, cancellationToken);

        if (aluno == null)
            throw new KeyNotFoundException($"Aluno com ID '{request.AlunoId}' não encontrado.");

        var turmaDestino = await _context.Turmas.FirstOrDefaultAsync(t => t.Id == request.NovaTurmaId, cancellationToken);
        if (turmaDestino == null)
            throw new DomainException("A turma de destino não existe.");

        // IDOR: Administrador pode transferir qualquer aluno; demais precisam estar vinculados
        // às turmas de origem E de destino para evitar movimentação cruzada não autorizada.
        if (_currentUser.Papel != nameof(PapelUsuario.Administrador)
            && Guid.TryParse(_currentUser.UsuarioId, out var uid))
        {
            var turmasVinculadas = await _context.UsuarioTurmas
                .Where(ut => ut.UsuarioId == uid && (ut.TurmaId == aluno.TurmaId || ut.TurmaId == request.NovaTurmaId))
                .Select(ut => ut.TurmaId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (!turmasVinculadas.Contains(aluno.TurmaId) || !turmasVinculadas.Contains(request.NovaTurmaId))
            {
                throw new DomainException("Você não tem permissão para transferir alunos entre essas turmas.");
            }
        }

        if (!aluno.HistoricoTurmas.Any())
        {
            // Compatibilidade: aluno sem histórico ainda não foi retroalimentado.
            // Cria o histórico inicial diretamente no contexto, persiste e recarrega
            // o aluno para que TransferirTurma encontre a matrícula ativa e a encerre.
            var turmaAtual = await _context.Turmas.AsNoTracking().FirstOrDefaultAsync(t => t.Id == aluno.TurmaId, cancellationToken);
            var anoLetivoAtual = turmaAtual?.AnoLetivo ?? DateTime.UtcNow.Year;
            var matriculaInicial = new AlunoTurmaHistorico(
                Guid.NewGuid(),
                aluno.Id,
                aluno.TurmaId,
                anoLetivoAtual,
                new DateTime(anoLetivoAtual, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                dataFim: null,
                "Retroalimentação automática");
            _context.AlunosTurmasHistorico.Add(matriculaInicial);
            await _context.SaveChangesAsync(cancellationToken);

            aluno = await _context.Alunos
                .Include(a => a.HistoricoTurmas)
                .FirstAsync(a => a.Id == request.AlunoId, cancellationToken);
        }

        var novaMatricula = aluno.TransferirTurma(
            request.NovaTurmaId,
            turmaDestino.AnoLetivo,
            request.DataTransferencia,
            request.Motivo);

        _context.AlunosTurmasHistorico.Add(novaMatricula);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[AUDITORIA] Aluno transferido — AlunoId={AlunoId} NovaTurmaId={NovaTurmaId} UsuarioId={UsuarioId} Papel={Papel}",
            request.AlunoId,
            request.NovaTurmaId,
            _currentUser.UsuarioId,
            _currentUser.Papel);
    }
}
