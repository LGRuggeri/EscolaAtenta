using EscolaAtenta.Application.Alertas.Dtos;
using EscolaAtenta.Application.Alertas.Queries;
using EscolaAtenta.Application.Common;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Exceptions;
using EscolaAtenta.Domain.Interfaces;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EscolaAtenta.Application.Alertas.Handlers;

/// <summary>
/// Handler para consulta paginada de alertas de evasão.
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
    private readonly ICurrentUserService _currentUser;

    public GetAlertasHandler(AppDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<AlertaEvasaoDto>> Handle(
        GetAlertasQuery request,
        CancellationToken cancellationToken)
    {
        // IDOR: Administrador pode consultar qualquer turma; demais papéis precisam de vínculo
        HashSet<Guid>? turmasPermitidas = null;
        if (_currentUser.Papel != nameof(PapelUsuario.Administrador)
            && Guid.TryParse(_currentUser.UsuarioId, out var usuarioId))
        {
            turmasPermitidas = await _context.UsuarioTurmas
                .AsNoTracking()
                .Where(ut => ut.UsuarioId == usuarioId)
                .Select(ut => ut.TurmaId)
                .ToHashSetAsync(cancellationToken);
        }

        // ── Bounds guard: proteção contra valores inválidos de paginação ──────
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize   = Math.Clamp(request.PageSize, 1, 100);

        // ── Base query ────────────────────────────────────────────────────────
        // Filtra apenas alertas de evasão no painel padrão. Alertas de atraso são
        // legados (não são mais gerados), mas podem existir no banco. Exibir um
        // alerta de atraso como se fosse falta causaria ação errada da supervisão.
        var query = _context.AlertasEvasao
            .IgnoreQueryFilters() // Garante que Alunos/Turmas inativos apareçam no histórico
            .AsNoTracking()
            .Where(a => a.Tipo == TipoAlerta.Evasao);

        // IDOR: restringe alertas às turmas vinculadas ao usuário
        if (turmasPermitidas is not null)
        {
            query = query.Where(a => a.TurmaId.HasValue && turmasPermitidas.Contains(a.TurmaId.Value));
        }

        if (request.ApenasNaoResolvidos)
        {
            query = query.Where(a => !a.Resolvido);
        }

        if (request.Nivel.HasValue)
        {
            query = query.Where(a => a.Nivel == request.Nivel.Value);
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
        
        // SQLite não suporta ORDER BY em colunas DateTimeOffset via EF Core.
        // Carrega os registros da query (já filtrada) e ordena/pagina em memória.
        var todos = await query
            .Select(a => new
            {
                a.Id,
                AlunoNome = a.Aluno != null ? a.Aluno.Nome : "Desconhecido",
                TurmaNome = a.Turma != null ? a.Turma.Nome :
                           (a.Aluno != null && a.Aluno.Turma != null ? a.Aluno.Turma.Nome : "Turma Não Informada"),
                a.Nivel,
                a.Descricao,
                a.DataAlerta,
                a.Resolvido,
                a.ObservacaoResolucao,
                TipoNome = a.Tipo.ToString(),
                ResolvidoPorNome = a.ResolvidoPor != null ? a.ResolvidoPor.Email : null,
                a.DataResolucao,
                a.JustificativaResolucao,
                // Contador atual do aluno para mensagem precisa
                FaltasConsecutivasAtuais = a.Aluno != null ? a.Aluno.FaltasConsecutivasAtuais : 0,
            })
            .ToListAsync(cancellationToken);

        var dbResult = (!request.ApenasNaoResolvidos
            ? todos.OrderByDescending(a => a.DataResolucao).ThenByDescending(a => a.DataAlerta)
            : todos.OrderByDescending(a => a.DataAlerta))
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // ── Mapeamento em memória — APENAS na página atual ────────────────────
        // GetTituloAmigavel e FormatarDescricaoLimpa não são traduzíveis pelo EF.
        // Executados apenas nos registros da página, nunca em toda a tabela.
        var items = dbResult.Select(a => new AlertaEvasaoDto(
            a.Id,
            a.AlunoNome,
            a.TurmaNome,
            a.Nivel,
            FormatarMensagem(a.AlunoNome, a.TurmaNome, a.DataAlerta.LocalDateTime, a.FaltasConsecutivasAtuais),
            a.DataAlerta.UtcDateTime,
            a.Resolvido,
            a.ObservacaoResolucao,
            GetTituloAmigavel(a.Nivel),
            FormatarMensagem(a.AlunoNome, a.TurmaNome, a.DataAlerta.LocalDateTime, a.FaltasConsecutivasAtuais),
            a.TipoNome,
            a.ResolvidoPorNome,
            a.DataResolucao?.UtcDateTime,
            a.JustificativaResolucao
        )).ToList();

        return PagedResult<AlertaEvasaoDto>.Create(items, totalCount, pageNumber, pageSize);
    }

    /// <summary>
    /// Formata a descrição removendo IDs e estruturas internas — retorna texto
    /// legível para o usuário final de supervisão.
    /// </summary>
    private static string FormatarMensagem(
        string alunoNome,
        string turmaNome,
        DateTime dataInfracao,
        int faltasConsecutivas)
    {
        return $"{alunoNome} ({turmaNome}) está com {faltasConsecutivas} falta(s) consecutiva(s). " +
               $"Última falta: {dataInfracao:dd/MM/yyyy HH:mm}";
    }

    /// <summary>
    /// Retorna título amigável baseado no nível do alerta.
    /// Exibição apenas — não é dado de negócio.
    /// </summary>
    private static string GetTituloAmigavel(NivelAlertaFalta nivel)
    {
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
