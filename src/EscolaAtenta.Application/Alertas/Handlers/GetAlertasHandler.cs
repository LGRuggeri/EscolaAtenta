using EscolaAtenta.Application.Alertas.Dtos;
using EscolaAtenta.Application.Alertas.Queries;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Alertas.Handlers;

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
            .Include(a => a.Aluno)
            .Include(a => a.Turma)
            .AsNoTracking();

        if (request.ApenasNaoResolvidos)
        {
            query = query.Where(a => !a.Resolvido);
        }

        var alertas = await query
            .OrderByDescending(a => a.DataAlerta)
            .ToListAsync(cancellationToken);

        return alertas.Select(a => new AlertaEvasaoDto(
            a.Id,
            a.Aluno?.Nome ?? "Desconhecido",
            a.Turma?.Nome ?? "Turma Não Informada",
            a.Nivel,
            a.Descricao,
            a.DataAlerta.UtcDateTime,
            a.Resolvido,
            a.ObservacaoResolucao,
            a.Nivel switch
            {
                EscolaAtenta.Domain.Enums.NivelAlertaFalta.Vermelho => "🚨 Alto Risco de Evasão",
                EscolaAtenta.Domain.Enums.NivelAlertaFalta.Preto => "🛑 Risco Crítico - Ação Legal",
                EscolaAtenta.Domain.Enums.NivelAlertaFalta.Intermediario => "⚠️ Alerta Intermediário",
                EscolaAtenta.Domain.Enums.NivelAlertaFalta.Aviso => "👀 Aviso de Faltas",
                _ => "Alerta Escolar"
            },
            a.Nivel switch
            {
                // Dynamic interpolation based on standard system rules mapped to 'Nivel'
                EscolaAtenta.Domain.Enums.NivelAlertaFalta.Vermelho => $"O aluno(a) {a.Aluno?.Nome} acumulou {a.Aluno?.FaltasConsecutivasAtuais} faltas consecutivas ou 3 atrasos. Ação imediata exigida: Contatar família.",
                EscolaAtenta.Domain.Enums.NivelAlertaFalta.Preto => $"Atenção máxima: {a.Aluno?.Nome} ultrapassou o limite tolerável. Acionar Conselho Tutelar imediatamente.",
                _ => a.Descricao // Fallback
            }
        ));
    }
}
