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
    private readonly ISqliteWriteLockProvider _lockProvider;

    public RealizarChamadaHandler(
        AppDbContext context,
        ICurrentUserService currentUser,
        ILogger<RealizarChamadaHandler> logger,
        ISqliteWriteLockProvider lockProvider)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
        _lockProvider = lockProvider;
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

        // P2: não permite datas futuras — isso criaria presenças antecipadas e
        // poderia mover o ciclo trimestral para frente, corrompendo contadores.
        if (dataHora.Date > DateTimeOffset.UtcNow.Date)
            throw new DomainException("A data da chamada não pode ser posterior ao dia atual.");

        await _lockProvider.WaitAsync(cancellationToken);
        try
        {
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

                // P2: usa a membresia da chamada salva para buscar alunos, não a turma atual.
                // Um aluno transferido após a chamada ainda deve ter seu registro histórico editável.
                var alunosIdsDaChamada = registrosExistentes.Keys.ToList();
                var alunosDb = await _context.Alunos
                    .Where(a => alunosIdsDaChamada.Contains(a.Id))
                    .ToDictionaryAsync(a => a.Id, cancellationToken);

                var alunosAfetados = new HashSet<Guid>();

                foreach (var registroDto in request.Alunos)
                {
                    if (!registrosExistentes.TryGetValue(registroDto.AlunoId, out var registroExistente))
                    {
                        _logger.LogWarning(
                            "Aluno {AlunoId} não consta na chamada do dia {Data}. Não é permitido adicionar novos alunos em uma chamada existente.",
                            registroDto.AlunoId, dataHora.Date);
                        continue;
                    }

                    if (!alunosDb.TryGetValue(registroDto.AlunoId, out var aluno))
                    {
                        _logger.LogWarning(
                            "Tentativa de atualizar presença para aluno inexistente: {AlunoId}",
                            registroDto.AlunoId);
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
                // 4. Busca todos os alunos da lista para atualizar.
                // Filtra por TurmaId para evitar registrar presença de aluno de outra turma.
                var alunosIds = request.Alunos.Select(a => a.AlunoId).ToList();
                var alunosDb = await _context.Alunos
                    .Where(a => alunosIds.Contains(a.Id) && a.TurmaId == request.TurmaId)
                    .ToDictionaryAsync(a => a.Id, cancellationToken);
                var chamada = new Chamada(
                    id: Guid.NewGuid(),
                    dataHora: dataHora,
                    turmaId: request.TurmaId,
                    responsavelId: responsavelIdSeguro
                );

                _context.Chamadas.Add(chamada);

                var alunosAfetados = new HashSet<Guid>();

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
                    alunosAfetados.Add(aluno.Id);
                }

                // Recalcula estatísticas dos alunos afetados, garantindo que chamadas
                // retroativas sejam processadas cronologicamente e incluam os novos registros.
                if (alunosAfetados.Count > 0)
                {
                    await RecalcularEstatisticasDosAlunos(alunosAfetados, cancellationToken);

                    foreach (var alunoId in alunosAfetados)
                    {
                        if (alunosDb.TryGetValue(alunoId, out var aluno) && aluno.DomainEvents.Count > 0)
                        {
                            alertasGerados++;
                        }
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
        finally
        {
            _lockProvider.Release();
        }
    }

    private async Task RecalcularEstatisticasDosAlunos(
        HashSet<Guid> alunosIds,
        CancellationToken cancellationToken)
    {
        if (alunosIds.Count == 0) return;

        // Carrega registros já persistidos + os adicionados no ChangeTracker
        // para garantir que chamadas retroativas e novas inclusões sejam
        // consideradas no recálculo cronológico dos contadores.
        var registrosPersistidos = await _context.RegistrosPresenca
            .Include(r => r.Chamada)
            .Where(r => alunosIds.Contains(r.AlunoId))
            .ToListAsync(cancellationToken);

        var registrosAdicionados = _context.ChangeTracker.Entries<RegistroPresenca>()
            .Where(e => e.State == EntityState.Added && alunosIds.Contains(e.Entity.AlunoId))
            .Select(e => e.Entity)
            .ToList();

        // Garante que registros pendentes tenham a navegação Chamada carregada
        foreach (var registro in registrosAdicionados)
        {
            if (registro.Chamada is null)
            {
                await _context.Entry(registro)
                    .Reference(r => r.Chamada)
                    .LoadAsync(cancellationToken);
            }
        }

        var registros = registrosPersistidos
            .Concat(registrosAdicionados)
            .ToList();

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

            aluno.RecalcularEstatisticas(historico);

            // Reconcilia alertas pendentes com os contadores finais, rebaixando
            // o nível quando o contador cai para um threshold inferior, resolvendo
            // quando cai abaixo de todos os thresholds e criando/escalando quando
            // atinge um threshold.
            aluno.ReconciliarAlertasPendentes();
        }
    }
}
