using EscolaAtenta.Application.Alertas.Dtos;
using EscolaAtenta.Application.Alertas.Queries;
using EscolaAtenta.Application.Common;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Alertas.Handlers;

/// <summary>
/// Handler de Read Model para auditoria de alertas resolvidos.
///
/// Decisões de design:
/// 1. AsNoTracking() — somente leitura, nunca precisa rastrear mudanças.
/// 2. Where(Resolvido == true) aplicado IMEDIATAMENTE na query base —
///    aproveita o índice IX_AlertasEvasao_Auditoria (Resolvido, DataResolucao, Tipo).
/// 3. EF.Functions.Like para NomeAluno — traduzido para LIKE no PostgreSQL,
///    evitando trazer todos os registros para filtrar em memória.
/// 4. COUNT separado antes do Skip/Take — EF emite SELECT COUNT(*) + SELECT ...
///    sem carregar todos os registros na memória apenas para paginar.
/// 5. .Select() projeta diretamente para AuditoriaAlertaDto — sem N+1,
///    sem materializar a entidade completa AlertaEvasao na memória.
/// 6. DataFim ajustado para o último tick do dia selecionado — cobre 23:59:59.999.
/// </summary>
public class GetAuditoriaAlertasQueryHandler
    : IRequestHandler<GetAuditoriaAlertasQuery, PagedResult<AuditoriaAlertaDto>>
{
    private readonly AppDbContext _context;

    public GetAuditoriaAlertasQueryHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<AuditoriaAlertaDto>> Handle(
        GetAuditoriaAlertasQuery request,
        CancellationToken cancellationToken)
    {
        // ── Bounds guard: proteção contra valores de paginação inválidos ou maliciosos ──
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize   = Math.Clamp(request.PageSize, 1, 100);

        // ── Base query — AsNoTracking + filtro imediato por Resolvido == true ──────────
        // O filtro Resolvido=true na query base maximiza o aproveitamento do
        // índice composto IX_AlertasEvasao_Auditoria(Resolvido, DataResolucao, Tipo).
        var query = _context.AlertasEvasao
            .AsNoTracking()
            .Where(a => a.Resolvido);

        // ── Filtros opcionais — aplicados antes do COUNT para máxima performance ───────

        if (!string.IsNullOrWhiteSpace(request.NomeAluno))
        {
            // EF.Functions.Like garante tradução correta para SQL LIKE no PostgreSQL.
            // Não use .Contains() — pode gerar ILIKE ou comportamento inesperado.
            query = query.Where(a => a.Aluno != null &&
                EF.Functions.Like(a.Aluno.Nome, $"%{request.NomeAluno}%"));
        }

        if (request.Tipo.HasValue)
        {
            query = query.Where(a => a.Tipo == request.Tipo.Value);
        }

        if (request.DataInicio.HasValue)
        {
            // Converte para DateTimeOffset para compatibilidade com a coluna
            var inicio = new DateTimeOffset(request.DataInicio.Value, TimeSpan.Zero);
            query = query.Where(a => a.DataResolucao >= inicio);
        }

        if (request.DataFim.HasValue)
        {
            // Ajuste crucial: engloba todas as horas até o último tick do dia selecionado.
            // Evita perder registros feitos às 23:59:xx do dia final.
            var fimAjustado = new DateTimeOffset(
                request.DataFim.Value.Date.AddDays(1).AddTicks(-1), TimeSpan.Zero);
            query = query.Where(a => a.DataResolucao <= fimAjustado);
        }

        // ── COUNT total — query separada, antes do Skip/Take ─────────────────────────
        // EF emite SELECT COUNT(*) FROM AlertasEvasao WHERE Resolvido=true AND ...
        // Necessário para o front-end calcular TotalPages / HasNextPage.
        var totalCount = await query.CountAsync(cancellationToken);

        if (totalCount == 0)
        {
            return PagedResult<AuditoriaAlertaDto>.Empty(pageNumber, pageSize);
        }

        // ── Projeção paginada — .Select() direto para DTO antes do ToListAsync() ──────
        // Garante que o EF emita apenas as colunas necessárias (sem SELECT *).
        // Resolve o relacionamento ResolvidoPor.Email em SQL (sem N+1).
        var itens = await query
            .OrderByDescending(a => a.DataResolucao)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditoriaAlertaDto
            {
                Id             = a.Id,
                NomeAluno      = a.Aluno != null ? a.Aluno.Nome : "Desconhecido",
                TipoAlerta     = a.Tipo.ToString(),           // "Evasao" | "Atraso"
                DataResolucao  = a.DataResolucao ?? DateTimeOffset.MinValue,
                ResolvidoPor   = a.ResolvidoPor != null
                                    ? (a.ResolvidoPor.Email ?? "Sistema")
                                    : "Sistema",
                MotivoResolucao = a.JustificativaResolucao ?? string.Empty,
                NivelAlerta    = a.Nivel.ToString(),          // "Vermelho", "Preto", etc.
                DataAlerta     = a.DataAlerta
            })
            .ToListAsync(cancellationToken);

        return PagedResult<AuditoriaAlertaDto>.Create(itens, totalCount, pageNumber, pageSize);
    }
}
