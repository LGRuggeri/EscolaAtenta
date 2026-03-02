using EscolaAtenta.Application.Alertas.Dtos;
using EscolaAtenta.Application.Alertas.Queries;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Alertas.Handlers;

/// <summary>
/// Handler para consulta de alertas de evasão.
/// 
/// Padrão Read Model: Projeta os dados diretamente no banco (SELECT) 
/// evitando carregar entidades completas na memória do servidor.
/// </summary>
public class GetAlertasHandler : IRequestHandler<GetAlertasQuery, IEnumerable<AlertaEvasaoDto>>
{
    private readonly AppDbContext _context;

    public GetAlertasHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<AlertaEvasaoDto>> Handle(GetAlertasQuery request, CancellationToken cancellationToken)
    {
        var query = _context.AlertasEvasao
            .IgnoreQueryFilters() // Para garantir que Alunos e Turmas Inativos apareçam nos registros de alerta
            .AsNoTracking();

        if (request.ApenasNaoResolvidos)
        {
            query = query.Where(a => !a.Resolvido);
        }

        // Projeção direta no banco - o EF faz o JOIN e traz apenas as strings necessárias
        // Não carrega entidades Aluno/Turma completas na memória
        var dbResult = await query
            .OrderByDescending(a => a.DataAlerta)
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
                a.ObservacaoResolucao
            })
            .ToListAsync(cancellationToken);

        // O método GetTituloAmigavel não pode ser traduzido pelo Entity Framework (SQL),
        // portanto, deve ser executado em memória nas entidades já carregadas.
        return dbResult.Select(a => new AlertaEvasaoDto(
            a.Id,
            a.AlunoNome,
            a.TurmaNome,
            a.Nivel,
            FormatarDescricaoLimpa(a.Descricao, a.AlunoNome, a.TurmaNome, a.DataAlerta.LocalDateTime),
            a.DataAlerta.UtcDateTime,
            a.Resolvido,
            a.ObservacaoResolucao,
            GetTituloAmigavel(a.Nivel),
            FormatarDescricaoLimpa(a.Descricao, a.AlunoNome, a.TurmaNome, a.DataAlerta.LocalDateTime)
        ));
    }

    /// <summary>
    /// Limpa a descrição gravada com formato antigo (que continha IDs e limite)
    /// Retornando exatamente conforme o layout requerido pelo front-end para este release.
    /// </summary>
    private static string FormatarDescricaoLimpa(string descricaoOriginal, string alunoNome, string turmaNome, DateTime dataInfracao)
    {
        // Se a descrição for algo focado em atrasos (Regra Nova), mantemos a indicação de atraso,
        // mas sem o ID complexo
        string tipoInfracao = descricaoOriginal.Contains("atrasos", StringComparison.OrdinalIgnoreCase) 
            ? "atrasos excedidos" 
            : "faltas consecutivas";

        return $"O aluno {alunoNome} ({turmaNome}) atingiu o limite de {tipoInfracao}. Última infração: {dataInfracao:dd/MM/yyyy HH:mm}";
    }

    /// <summary>
    /// Retorna título amigável estático baseado no nível do alerta.
    /// Este é um metadata de exibição, não dados de negócio.
    /// </summary>
    private static string GetTituloAmigavel(NivelAlertaFalta nivel) => nivel switch
    {
        NivelAlertaFalta.Vermelho => "🚨 Alto Risco de Evasão",
        NivelAlertaFalta.Preto => "🛑 Risco Crítico - Ação Legal",
        NivelAlertaFalta.Intermediario => "⚠️ Alerta Intermediário",
        NivelAlertaFalta.Aviso => "👀 Aviso de Faltas",
        _ => "Alerta Escolar"
    };
}
