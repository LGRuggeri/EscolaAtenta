// Enum para níveis de alerta de falta
// Usado no Dashboard da Supervisao para indicadores visuais
namespace EscolaAtenta.Domain.Enums;

/// <summary>
/// Níveis de alerta baseados em faltas consecutivas.
/// </summary>
public enum NivelAlertaFalta
{
    Excelencia = 0, // Turma sem faltas no período
    Aviso = 1, // Ex: 2 atrasos
    Intermediario = 2,
    Vermelho = 3, // Comunicado aos Pais (Ex: 3 atrasos)
    Preto = 5 // Conselho Tutelar (Ex: 5 atrasos)
}
