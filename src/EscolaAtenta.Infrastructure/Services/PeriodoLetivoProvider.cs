using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace EscolaAtenta.Infrastructure.Services;

/// <summary>
/// Lê o tipo de período letivo do appsettings.json.
/// Valor padrão: Trimestre (2), para manter compatibilidade com escolas
/// que nunca configuraram explicitamente.
/// </summary>
public class PeriodoLetivoProvider : IPeriodoLetivoProvider
{
    private readonly IConfiguration _configuration;

    public PeriodoLetivoProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public TipoPeriodoLetivo ObterTipoPeriodoLetivo()
    {
        var valor = _configuration["RegrasNegocio:TipoPeriodoLetivo"];
        if (int.TryParse(valor, out var tipoInt)
            && Enum.IsDefined(typeof(TipoPeriodoLetivo), tipoInt))
        {
            return (TipoPeriodoLetivo)tipoInt;
        }

        return TipoPeriodoLetivo.Trimestre;
    }
}
