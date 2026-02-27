using EscolaAtenta.Application.Chamadas.Commands;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Exceptions;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EscolaAtenta.Application.Chamadas.Handlers;

public class RealizarChamadaHandler : IRequestHandler<RealizarChamadaCommand, RealizarChamadaResult>
{
    private readonly AppDbContext _context;
    private readonly ILogger<RealizarChamadaHandler> _logger;

    public RealizarChamadaHandler(AppDbContext context, ILogger<RealizarChamadaHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<RealizarChamadaResult> Handle(RealizarChamadaCommand request, CancellationToken cancellationToken)
    {
        // 1. Verifica se a Turma existe
        var turmaExiste = await _context.Turmas.AnyAsync(t => t.Id == request.TurmaId, cancellationToken);
        if (!turmaExiste)
            throw new DomainException($"A turma informada '{request.TurmaId}' não existe.");

        // 2. Cria a nova Chamada
        var chamada = new Chamada(
            id: Guid.NewGuid(),
            dataHora: DateTimeOffset.UtcNow,
            turmaId: request.TurmaId,
            responsavelId: request.ResponsavelId
        );

        _context.Chamadas.Add(chamada);

        // 3. Busca todos os alunos da lista para atualizar
        var alunosIds = request.Alunos.Select(a => a.AlunoId).ToList();
        var alunosDb = await _context.Alunos
            .Where(a => alunosIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        int alertasGerados = 0;

        // 4. Mapeia as presenças e computa status na Entidade chamada e Aluno
        foreach (var registroDto in request.Alunos)
        {
            if (!alunosDb.TryGetValue(registroDto.AlunoId, out var aluno))
            {
                _logger.LogWarning("Tentativa de registrar presença para aluno inexistente: {AlunoId}", registroDto.AlunoId);
                continue;
            }

            // Atribui registro à Entidade Chamada
            chamada.RegistrarPresenca(aluno.Id, registroDto.Status);

            // Atualiza contadores na Entidade Aluno
            aluno.RegistrarPresenca(registroDto.Status, chamada.DataHora.UtcDateTime);

            // Valida Limite de Faltas - Adiciona Domain Event (AlertaEvasao) caso seja 3 faltas.
            aluno.VerificarLimiteFaltas();

            if (aluno.DomainEvents.Any())
            {
                alertasGerados++;
            }

            // ── Verifica Atrasos Reincidentes (Novas Regras de Evasão) ─────────
            if (registroDto.Status == EscolaAtenta.Domain.Enums.StatusPresenca.Atraso)
            {
                if (aluno.AtrasosNoTrimestre == 3)
                {
                    var alerta = AlertaEvasao.CriarAlertaAluno(
                        alunoId: aluno.Id,
                        turmaId: aluno.TurmaId,
                        nivel: EscolaAtenta.Domain.Enums.NivelAlertaFalta.Vermelho,
                        motivo: "Aluno atingiu 3 atrasos no trimestre. Comunicar aos pais."
                    );
                    _context.AlertasEvasao.Add(alerta);
                    alertasGerados++;
                }
                else if (aluno.AtrasosNoTrimestre == 5)
                {
                    var alerta = AlertaEvasao.CriarAlertaAluno(
                        alunoId: aluno.Id,
                        turmaId: aluno.TurmaId,
                        nivel: EscolaAtenta.Domain.Enums.NivelAlertaFalta.Preto,
                        motivo: "Aluno atingiu 5 atrasos no trimestre. Acionar Conselho Tutelar."
                    );
                    _context.AlertasEvasao.Add(alerta);
                    alertasGerados++;
                }
            }
        }

        // 5. Salva Tudo Atomicamente
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Lote de chamada realizado: Turma={TurmaId}, TotalAlunos={Total}, AlertasGerados={Alertas}",
            request.TurmaId, request.Alunos.Count, alertasGerados);

        return new RealizarChamadaResult(chamada.Id, alertasGerados);
    }
}
