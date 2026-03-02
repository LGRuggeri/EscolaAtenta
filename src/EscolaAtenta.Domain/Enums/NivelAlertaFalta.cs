// Enum para níveis de alerta de falta
// Usado no Dashboard da Supervisao para indicadores visuais
namespace EscolaAtenta.Domain.Enums;

/// <summary>
/// Níveis de alerta baseados em faltas consecutivas.
/// Valores discretos: 0, 1, 2, 3, 5 (não existe 4)
/// </summary>
public enum NivelAlertaFalta
{
    Excelencia = 0, // Turma sem faltas no período
    Aviso = 1, // Ex: 1 falta
    Intermediario = 2, // Ex: 2 faltas
    Vermelho = 3, // Comunicado aos Pais (3-4 faltas)
    // Valor 4 também é mapeado para Vermelho no domínio
    Preto = 5 // Conselho Tutelar (5+ faltas) - MÁXIMO PERMITIDO
}

/// <summary>
/// Extensões para o enum NivelAlertaFalta com regras de domínio e validações.
/// </summary>
public static class NivelAlertaFaltaExtensions
{
    /// <summary>
    /// Valor máximo permitido para nível de alerta (Preto = 5).
    /// </summary>
    public const int NIVEL_MAXIMO = 5;

    /// <summary>
    /// Garante que o nível não ultrapasse o máximo permitido (Preto).
    /// Qualquer valor acima de 5 é truncado para 5.
    /// </summary>
    /// <param name="nivel">Nível atual</param>
    /// <returns>Nível validado (nunca ultrapassa Preto)</returns>
    public static NivelAlertaFalta GarantirLimiteMaximo(this NivelAlertaFalta nivel)
    {
        var valorNumerico = (int)nivel;
        
        if (valorNumerico > NIVEL_MAXIMO)
        {
            return NivelAlertaFalta.Preto;
        }
        
        return nivel;
    }

    /// <summary>
    /// Converte um valor numérico inteiro para NivelAlertaFalta de forma segura.
    /// Valores não mapeados são truncados para o nível máximo (Preto).
    /// </summary>
    /// <param name="faltasConsecutivas">Número de faltas consecutivas</param>
    /// <returns>Nível de alerta correspondente</returns>
    public static NivelAlertaFalta DeFaltasConsecutivas(int faltasConsecutivas)
    {
        return faltasConsecutivas switch
        {
            0 => NivelAlertaFalta.Excelencia,
            1 => NivelAlertaFalta.Aviso,
            2 => NivelAlertaFalta.Intermediario,
            3 => NivelAlertaFalta.Vermelho,
            4 => NivelAlertaFalta.Vermelho, // 4 faltas = Vermelho (mesmo que 3)
            _ => NivelAlertaFalta.Preto     // 5+ faltas = Preto (máximo)
        };
    }

    /// <summary>
    /// Verifica se o nível representa severidade crítica (Preto).
    /// </summary>
    public static bool IsSeveridadeMaxima(this NivelAlertaFalta nivel)
    {
        return nivel == NivelAlertaFalta.Preto;
    }
}
