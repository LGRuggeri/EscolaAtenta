using EscolaAtenta.Application.Chamadas.Commands;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Exceptions;
using EscolaAtenta.Domain.Interfaces;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EscolaAtenta.Application.Chamadas.Handlers;

public class RealizarChamadaHandler : IRequestHandler<RealizarChamadaCommand, RealizarChamadaResult>
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<RealizarChamadaHandler> _logger;

    public RealizarChamadaHandler(
        AppDbContext context,
        ICurrentUserService currentUser,
        ILogger<RealizarChamadaHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<RealizarChamadaResult> Handle(RealizarChamadaCommand request, CancellationToken cancellationToken)
    {
        // 1. Verifica se a Turma existe
        var turmaExiste = await _context.Turmas.AnyAsync(t => t.Id == request.TurmaId, cancellationToken);
        if (!turmaExiste)
            throw new DomainException($"A turma informada '{request.TurmaId}' não existe.");

        // IDOR: Administrador pode operar qualquer turma; demais papéis precisam de vínculo
        if (_currentUser.Papel != nameof(PapelUsuario.Administrador)
            && Guid.TryParse(_currentUser.UsuarioId, out var ownerCheck)
            && !await _context.UsuarioTurmas.AnyAsync(
                ut => ut.TurmaId == request.TurmaId && ut.UsuarioId == ownerCheck, cancellationToken))
        {
            throw new DomainException("Você não tem permissão para realizar chamada nesta turma.");
        }

        // SEGURANÇA: Usa o UsuarioId do token JWT como responsável da chamada
        // Em vez de confiar cegamente no ResponsavelId enviado pelo cliente (vetor de spoofing).
        var responsavelIdSeguro = _currentUser.EstaAutenticado
            && Guid.TryParse(_currentUser.UsuarioId, out var parsedUserId)
            ? parsedUserId
            : request.ResponsavelId;

        // 2. Determina a data/hora da chamada (retroativa ou atual)
        var dataHora = request.Data ?? DateTimeOffset.UtcNow;

        // 3. Busca chamada existente para a turma naquele dia
        // Filtragem por data é feita em memória para compatibilidade com SQLite/DateTimeOffset.
        var chamadasDaTurma = await _context.Chamadas
            .Include(c => c.RegistrosPresenca)
            .Where(c => c.TurmaId == request.TurmaId)
            .ToListAsync(cancellationToken);

        // Se houver duplicatas históricas, escolhe a mais recentemente criada (depois pela Id).
        var chamadaExistente = chamadasDaTurma
            .Where(c => c.DataChamada == dataHora.Date)
            .OrderByDescending(c => c.DataCriacao)
            .ThenBy(c => c.Id)
            .FirstOrDefault();

        // 4. Busca todos os alunos da lista para atualizar
        var alunosIds = request.Alunos.Select(a => a.AlunoId).ToList();
        var alunosDb = await _context.Alunos
            .Where(a => alunosIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        bool chamadaFoiAtualizada = false;
        int alertasGerados = 0;

        if (chamadaExistente is not null)
        {
            // ── Atualização de chamada existente ───────────────────────────────
            var prazoEdicao = chamadaExistente.DataCriacao.AddDays(7);
            if (DateTimeOffset.UtcNow > prazoEdicao)
            {
                throw new DomainException(
                    $"A chamada do dia {dataHora:dd/MM/yyyy} não pode mais ser alterada. " +
                    "O prazo de edição de 7 dias foi excedido.");
            }

            var registrosExistentes = chamadaExistente.RegistrosPresenca
                .ToDictionary(r => r.AlunoId, r => r);

            var alunosAfetados = new HashSet<Guid>();

            foreach (var registroDto in request.Alunos)
            {
                if (!alunosDb.TryGetValue(registroDto.AlunoId, out var aluno))
                {
                    _logger.LogWarning(
                        "Tentativa de atualizar presença para aluno inexistente: {AlunoId}",
                        registroDto.AlunoId);
                    continue;
                }

                if (!registrosExistentes.TryGetValue(registroDto.AlunoId, out var registroExistente))
                {
                    _logger.LogWarning(
                        "Aluno {AlunoId} não consta na chamada do dia {Data}. Não é permitido adicionar novos alunos em uma chamada existente.",
                        registroDto.AlunoId, dataHora.Date);
                    continue;
                }

                if (registroExistente.Status != registroDto.Status)
                {
                    registroExistente.AlterarStatus(registroDto.Status);
                    alunosAfetados.Add(aluno.Id);
                }
            }

            // Recalcula estatísticas dos alunos que tiveram o status alterado
            if (alunosAfetados.Count > 0)
            {
                await RecalcularEstatisticasDosAlunos(alunosAfetados, cancellationToken);

                // Conta alertas gerados pela recalculagem (antes do SaveChanges limpar os eventos)
                foreach (var alunoId in alunosAfetados)
                {
                    if (alunosDb.TryGetValue(alunoId, out var aluno) && aluno.DomainEvents.Count > 0)
                    {
                        alertasGerados++;
                    }
                }
            }

            chamadaFoiAtualizada = alunosAfetados.Count > 0;

            _logger.LogInformation(
                "[AUDITORIA] Chamada atualizada — ChamadaId={ChamadaId} TurmaId={TurmaId} Data={Data} AlunosAfetados={AlunosAfetados} AlertasGerados={Alertas}",
                chamadaExistente.Id, request.TurmaId, dataHora.Date, alunosAfetados.Count, alertasGerados);
        }
        else
        {
            // ── Criação de nova chamada ─────────────────────────────────────────
            var chamada = new Chamada(
                id: Guid.NewGuid(),
                dataHora: dataHora,
                turmaId: request.TurmaId,
                responsavelId: responsavelIdSeguro
            );

            _context.Chamadas.Add(chamada);

            foreach (var registroDto in request.Alunos)
            {
                if (!alunosDb.TryGetValue(registroDto.AlunoId, out var aluno))
                {
                    _logger.LogWarning(
                        "Tentativa de registrar presença para aluno inexistente: {AlunoId}",
                        registroDto.AlunoId);
                    continue;
                }

                // Atribui registro à Entidade Chamada
                chamada.RegistrarPresenca(aluno.Id, registroDto.Status);

                // Atualiza contadores na Entidade Aluno.
                aluno.RegistrarPresenca(registroDto.Status, chamada.DataHora.UtcDateTime);

                if (aluno.DomainEvents.Count > 0)
                {
                    alertasGerados++;
                }
            }

            _logger.LogInformation(
                "[AUDITORIA] Chamada realizada — TurmaId={TurmaId} Responsavel={ResponsavelId} Data={Data} TotalAlunos={Total} AlertasGerados={Alertas}",
                request.TurmaId, responsavelIdSeguro, dataHora.Date, request.Alunos.Count, alertasGerados);

            // 5. Salva Tudo Atomicamente
            await _context.SaveChangesAsync(cancellationToken);

            return new RealizarChamadaResult(chamada.Id, alertasGerados);
        }

        // 5. Salva Tudo Atomicamente (atualização)
        await _context.SaveChangesAsync(cancellationToken);

        return new RealizarChamadaResult(
            chamadaExistente!.Id,
            alertasGerados,
            ChamadaExistenteAtualizada: chamadaFoiAtualizada);
    }

    private async Task RecalcularEstatisticasDosAlunos(
        HashSet<Guid> alunosIds,
        CancellationToken cancellationToken)
    {
        if (alunosIds.Count == 0) return;

        var registros = await _context.RegistrosPresenca
            .Include(r => r.Chamada)
            .Where(r => alunosIds.Contains(r.AlunoId))
            .ToListAsync(cancellationToken);

        var registrosPorAluno = registros
            .GroupBy(r => r.AlunoId)
            .ToDictionary(g => g.Key, g => g.AsEnumerable());

        var alunos = await _context.Alunos
            .Where(a => alunosIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        foreach (var alunoId in alunosIds)
        {
            if (!alunos.TryGetValue(alunoId, out var aluno))
                continue;

            var historico = registrosPorAluno.TryGetValue(alunoId, out var regs)
                ? regs
                : Enumerable.Empty<RegistroPresenca>();

            var faltasConsecutivasAntes = aluno.FaltasConsecutivasAtuais;
            var atrasosTrimestreAntes = aluno.AtrasosNoTrimestre;

            aluno.RecalcularEstatisticas(historico);

            // Reconcilia alertas pendentes quando a correção faz os contadores
            // caírem abaixo dos limiares configurados.
            if ((faltasConsecutivasAntes >= 1 && aluno.FaltasConsecutivasAtuais == 0) ||
                (atrasosTrimestreAntes >= 3 && aluno.AtrasosNoTrimestre < 3))
            {
                aluno.ReconciliarAlertasPendentes();
            }
        }
    }
}
