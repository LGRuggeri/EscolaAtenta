using EscolaAtenta.Application.Escola.DTOs;
using EscolaAtenta.Application.Escola.Queries;
using EscolaAtenta.Domain.Common;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Escola.Handlers;

public class ObterPeriodosLetivosDisponiveisHandler
    : IRequestHandler<ObterPeriodosLetivosDisponiveisQuery, PeriodosLetivosDisponiveisDto>
{
    private readonly AppDbContext _context;

    public ObterPeriodosLetivosDisponiveisHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PeriodosLetivosDisponiveisDto> Handle(
        ObterPeriodosLetivosDisponiveisQuery request,
        CancellationToken cancellationToken)
    {
        var configuracao = await _context.ConfiguracoesEscola
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        var tipoPeriodo = configuracao?.TipoPeriodoLetivo ?? TipoPeriodoLetivo.Trimestre;

        var periodos = CalendarioEscolar.ListarPeriodosAteData(
            DateTime.UtcNow, tipoPeriodo, request.AnoLetivo);

        var dtos = periodos
            .Select(p => new PeriodoLetivoDisponivelDto(p, ObterDescricao(tipoPeriodo, p)))
            .ToList();

        return new PeriodosLetivosDisponiveisDto(tipoPeriodo, dtos.AsReadOnly());
    }

    private static string ObterDescricao(TipoPeriodoLetivo tipoPeriodo, int numero)
    {
        var ordinal = numero switch
        {
            1 => "1º",
            2 => "2º",
            3 => "3º",
            4 => "4º",
            5 => "5º",
            _ => $"{numero}º"
        };

        var nome = tipoPeriodo switch
        {
            TipoPeriodoLetivo.Bimestre => "Bimestre",
            TipoPeriodoLetivo.Trimestre => "Trimestre",
            TipoPeriodoLetivo.Semestre => "Semestre",
            _ => "Período"
        };

        return $"{ordinal} {nome}";
    }
}
