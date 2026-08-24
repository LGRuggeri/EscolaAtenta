using EscolaAtenta.Application.Turmas.DTOs;
using EscolaAtenta.Application.Turmas.Queries;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Exceptions;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EscolaAtenta.Application.Turmas.Handlers;

public class RelatorioTurmaHandler : IRequestHandler<RelatorioTurmaQuery, RelatorioTurmaDto>
{
    private readonly AppDbContext _context;
    private readonly ILogger<RelatorioTurmaHandler> _logger;

    public RelatorioTurmaHandler(AppDbContext context, ILogger<RelatorioTurmaHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<RelatorioTurmaDto> Handle(RelatorioTurmaQuery request, CancellationToken cancellationToken)
    {
        if (request.DataInicio > request.DataFim)
            throw new DomainException("A data de início deve ser anterior ou igual à data de fim.");

        // Usa intervalo half-open [inicio, fim) para incluir todo o dia final,
        // mesmo chamadas com subsegundos próximos à meia-noite.
        var inicio = new DateTimeOffset(request.DataInicio.Date, TimeSpan.Zero);
        var fim = new DateTimeOffset(request.DataFim.Date.AddDays(1), TimeSpan.Zero);

        var turmaId = await ResolverTurmaIdAsync(request.TurmaId, cancellationToken);

        var turma = await _context.Turmas.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == turmaId, cancellationToken);

        if (turma == null)
            throw new KeyNotFoundException($"Turma com ID '{request.TurmaId}' não encontrada.");

        // Alunos matriculados na turma durante o período
        var alunosMatriculados = await _context.AlunosTurmasHistorico
            .AsNoTracking()
            .Where(h =>
                h.TurmaId == turmaId &&
                h.DataInicio < fim &&
                (!h.DataFim.HasValue || h.DataFim.Value >= inicio))
            .Select(h => h.AlunoId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Fallback: se o histórico estiver vazio (turmas/alunos criados antes da
        // introdução do histórico ou migração incompleta), considera os alunos
        // atuais da turma para não zerar o relatório.
        if (alunosMatriculados.Count == 0)
        {
            alunosMatriculados = await _context.Alunos
                .AsNoTracking()
                .Where(a => a.TurmaId == turmaId)
                .Select(a => a.Id)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        // Inclui alunos inativos (soft-deleted) para manter consistência histórica:
        // se o aluno estava ativo durante o período do relatório, seus registros
        // de presença devem contar para os totais da turma.
        var alunos = await _context.Alunos
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => alunosMatriculados.Contains(a.Id))
            .Select(a => new { a.Id, a.Nome, a.Matricula })
            .ToListAsync(cancellationToken);

        // Registros de presença da turma no período
        var registros = await _context.RegistrosPresenca
            .AsNoTracking()
            .Where(r => r.Chamada.TurmaId == turmaId &&
                        r.Chamada.DataHora >= inicio &&
                        r.Chamada.DataHora < fim)
            .Select(r => new { r.AlunoId, r.Status })
            .ToListAsync(cancellationToken);

        var alunosDto = new List<RelatorioTurmaAlunoDto>();
        int totalPresentes = 0;
        int totalFaltas = 0;
        int totalFaltasJustificadas = 0;
        int totalAtrasos = 0;
        int totalRegistros = registros.Count;

        foreach (var aluno in alunos.OrderBy(a => a.Nome))
        {
            var doAluno = registros.Where(r => r.AlunoId == aluno.Id).ToList();

            int presentes = doAluno.Count(r => r.Status == StatusPresenca.Presente);
            int faltas = doAluno.Count(r => r.Status == StatusPresenca.Falta || r.Status == StatusPresenca.Ausente);
            int faltasJustificadas = doAluno.Count(r => r.Status == StatusPresenca.FaltaJustificada);
            int atrasos = doAluno.Count(r => r.Status == StatusPresenca.Atraso);

            totalPresentes += presentes;
            totalFaltas += faltas;
            totalFaltasJustificadas += faltasJustificadas;
            totalAtrasos += atrasos;

            int totalDoAluno = doAluno.Count;
            double percentualPresenca = totalDoAluno == 0
                ? 0
                : Math.Round((double)presentes / totalDoAluno * 100, 2);

            alunosDto.Add(new RelatorioTurmaAlunoDto(
                aluno.Id,
                aluno.Nome,
                aluno.Matricula,
                presentes,
                faltas,
                faltasJustificadas,
                atrasos,
                percentualPresenca));
        }

        double percentualTurma = totalRegistros == 0
            ? 0
            : Math.Round((double)totalPresentes / totalRegistros * 100, 2);

        var resumo = new RelatorioTurmaResumoDto(
            alunos.Count,
            totalPresentes,
            totalFaltas,
            totalFaltasJustificadas,
            totalAtrasos,
            percentualTurma);

        var dataInicioRelatorio = request.DataInicio.Date;
        var dataFimRelatorio = request.DataFim.Date;

        _logger.LogInformation(
            "[AUDITORIA] Relatório de turma gerado — TurmaId={TurmaId} Periodo={PeriodoInicio:yyyy-MM-dd} a {PeriodoFim:yyyy-MM-dd}",
            request.TurmaId,
            dataInicioRelatorio,
            dataFimRelatorio);

        return new RelatorioTurmaDto(
            turma.Id,
            turma.Nome,
            turma.Turno,
            dataInicioRelatorio,
            dataFimRelatorio,
            alunosDto.AsReadOnly(),
            resumo);
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
