using EscolaAtenta.Domain.Common;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Exceptions;

namespace EscolaAtenta.Domain.Entities;

/// <summary>
/// Configurações globais da escola/tenant local.
/// 
/// Hoje armazena apenas a forma de divisão do ano letivo (bimestral, trimestral
/// ou semestral), mas pode evoluir para calendário escolar, dias letivos, etc.
/// </summary>
public class ConfiguracaoEscola : EntityBase
{
    // Construtor privado para uso exclusivo do EF Core
    private ConfiguracaoEscola() { }

    public ConfiguracaoEscola(Guid id, TipoPeriodoLetivo tipoPeriodoLetivo)
        : base(id)
    {
        TipoPeriodoLetivo = tipoPeriodoLetivo;
    }

    public TipoPeriodoLetivo TipoPeriodoLetivo { get; private set; }

    /// <summary>
    /// Altera a forma de divisão do ano letivo.
    /// </summary>
    public void AlterarTipoPeriodoLetivo(TipoPeriodoLetivo tipoPeriodoLetivo)
    {
        TipoPeriodoLetivo = tipoPeriodoLetivo;
    }
}
