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
        var query = _context.AlertasEvasao.AsQueryable();

        if (request.ApenasNaoResolvidos)
        {
            query = query.Where(a => !a.Resolvido);
        }

        var alertas = await query
            .OrderByDescending(a => a.DataAlerta)
            .ToListAsync(cancellationToken);

        return alertas.Select(a => new AlertaEvasaoDto(
            a.Id,
            a.AlunoId,
            a.TurmaId,
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
                EscolaAtenta.Domain.Enums.NivelAlertaFalta.Vermelho => "O aluno atingiu 3 ausências/atrasos seguidos. Ação: Entrar em contato com os pais ou responsáveis imediatamente.",
                EscolaAtenta.Domain.Enums.NivelAlertaFalta.Preto => "O aluno atingiu 5 ausências. Ação exigida: Acionar o Conselho Tutelar.",
                _ => a.Descricao // Fallback para a descricao/motivo original
            }
        ));
    }
}
