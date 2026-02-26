// Enum para níveis de alerta de falta
// Usado no Dashboard da Supervisao para indicadores visuais
namespace EscolaAtenta.Domain.Enums;

/// <summary>
/// Níveis de alerta baseados em faltas consecutivas.
/// </summary>
public enum NivelAlertaFalta
{
    /// <summary>
    /// Nenhuma falta registrada.
    /// </summary>
    Nenhum = 0,

    /// <summary>
    /// 1 falta consecutivos - aviso leve.
/// </summary>
    Aviso = 1,

    /// <summary>
    /// 2 faltas consecutivas - atenção.
/// </summary>
    Atencao = 2,

    /// <summary>
    /// 3+ faltas consecutivas - alerta crítico para a Supervisão.
/// </summary>
    Critico = 3
}
