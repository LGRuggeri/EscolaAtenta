using EscolaAtenta.Application.Alertas.Dtos;
using EscolaAtenta.Application.Alertas.Queries;
using EscolaAtenta.Application.Common;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Alertas.Handlers;

/// <summary>
/// Handler para consulta paginada de alertas de evasão/atraso.
///
/// Padrão Read Model: Projeta os dados diretamente no banco (SELECT)
/// evitando carregar entidades completas na memória.
///
/// Estratégia de paginação:
/// 1. COUNT separado antes do Skip/Take — evita carregar todos os registros
///    apenas para contar. O EF Core traduz para SELECT COUNT(*) + SELECT ... LIMIT.
/// 2. Validação de bounds: PageNumber mínimo = 1, PageSize clampado entre 1 e 100.
///    Isso previne clientes maliciosos de solicitar PageSize = 1_000_000.
/// 3. O mapeamento em memória (GetTituloAmigavel) ocorre APÓS o ToListAsync()
///    apenas na página atual — nunca em todos os registros do banco.
/// </summary>
public class GetAlertasHandler : IRequestHandler<GetAlertasQuery, PagedResult<AlertaEvasaoDto>>
{
    private readonly AppDbContext _context;

    public GetAlertasHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<AlertaEvasaoDto>> Handle(
        GetAlertasQuery request,
        CancellationToken cancellationToken)
    {
        // ── Bounds guard: proteção contra valores inválidos de paginação ──────
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize   = Math.Clamp(request.PageSize, 1, 100);

        // ── Base query ────────────────────────────────────────────────────────
        var query = _context.AlertasEvasao
            .IgnoreQueryFilters() // Garante que Alunos/Turmas inativos apareçam no histórico
            .AsNoTracking();

        if (request.ApenasNaoResolvidos)
        {
            query = query.Where(a => !a.Resolvido);
        }

        // ── COUNT total — query separada sem Skip/Take ────────────────────────
        // O EF Core emite SELECT COUNT(*) FROM AlertasEvasao WHERE ...
        // Precedendo o SELECT de dados. Isso é necessário para o Front-end
        // calcular hasNextPage e a barra de progresso do scroll infinito.
        var totalCount = await query.CountAsync(cancellationToken);

        if (totalCount == 0)
        {
            return PagedResult<AlertaEvasaoDto>.Empty(pageNumber, pageSize);
        }

        // ── Projeção paginada ─────────────────────────────────────────────────
        // OrderBy ANTES do Skip/Take é obrigatório (EF Core lança sem ele).
        // Projeção direta no banco — o EF faz o JOIN e traz apenas as colunas
        // necessárias para o DTO. Não carrega entidades Aluno/Turma completas.
        var dbResult = await query
            .OrderByDescending(a => a.DataAlerta)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                AlunoNome = a.Aluno != null ? a.Aluno.Nome : "Desconhecido",
                // Fallback histórico: se o alerta antigo não tinha TurmaId preenchido fisicamente,
                // buscamos a Turma atual através do relacionamento do Aluno.
                TurmaNome = a.Turma != null ? a.Turma.Nome :
                           (a.Aluno != null && a.Aluno.Turma != null ? a.Aluno.Turma.Nome : "Turma Não Informada"),
                a.Nivel,
                a.Descricao,
                a.DataAlerta,
                a.Resolvido,
                a.ObservacaoResolucao,
                TipoNome = a.Tipo.ToString() // "Evasao" | "Atraso"
            })
            .ToListAsync(cancellationToken);

        // ── Mapeamento em memória — APENAS na página atual ────────────────────
        // GetTituloAmigavel e FormatarDescricaoLimpa não são traduzíveis pelo EF.
        // Executados apenas nos registros da página, nunca em toda a tabela.
        var items = dbResult.Select(a => new AlertaEvasaoDto(
            a.Id,
            a.AlunoNome,
            a.TurmaNome,
            a.Nivel,
            FormatarDescricaoLimpa(a.Descricao, a.AlunoNome, a.TurmaNome, a.DataAlerta.LocalDateTime),
            a.DataAlerta.UtcDateTime,
            a.Resolvido,
            a.ObservacaoResolucao,
            GetTituloAmigavel(a.Nivel, a.TipoNome),
            FormatarDescricaoLimpa(a.Descricao, a.AlunoNome, a.TurmaNome, a.DataAlerta.LocalDateTime),
            a.TipoNome
        )).ToList();

        return PagedResult<AlertaEvasaoDto>.Create(items, totalCount, pageNumber, pageSize);
    }

    /// <summary>
    /// Formata a descrição removendo IDs e estruturas internas — retorna texto
    /// legível para o usuário final de supervisão.
    /// </summary>
    private static string FormatarDescricaoLimpa(
        string descricaoOriginal,
        string alunoNome,
        string turmaNome,
        DateTime dataInfracao)
    {
        string tipoInfracao = descricaoOriginal.Contains("atrasos", StringComparison.OrdinalIgnoreCase)
            ? "atrasos excedidos"
            : "faltas consecutivas";

        return $"O aluno {alunoNome} ({turmaNome}) atingiu o limite de {tipoInfracao}. " +
               $"Última infração: {dataInfracao:dd/MM/yyyy HH:mm}";
    }

    /// <summary>
    /// Retorna título amigável baseado no nível e tipo do alerta.
    /// Exibição apenas — não é dado de negócio.
    /// </summary>
    private static string GetTituloAmigavel(NivelAlertaFalta nivel, string tipo)
    {
        if (tipo == "Atraso")
        {
            return nivel switch
            {
                NivelAlertaFalta.Intermediario => "⚠️ Atrasos Reincidentes",
                _ => "🕑 Aviso de Atrasos"
            };
        }

        return nivel switch
        {
            NivelAlertaFalta.Vermelho      => "🚨 Alto Risco de Evasão",
            NivelAlertaFalta.Preto         => "🛑 Risco Crítico - Ação Legal",
            NivelAlertaFalta.Intermediario => "⚠️ Alerta Intermediário",
            NivelAlertaFalta.Aviso         => "👀 Aviso de Faltas",
            _                              => "Alerta Escolar"
        };
    }
}
