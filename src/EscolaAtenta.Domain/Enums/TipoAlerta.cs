namespace EscolaAtenta.Domain.Enums;

/// <summary>
/// Classifica o tipo de alerta gerado pelo sistema.
/// 
/// Hoje o sistema gera apenas alertas de evasão (faltas consecutivas excessivas).
/// O valor 2 (Atraso) é mantido apenas para leitura compatível de alertas antigos;
/// novos alertas de atraso não são mais emitidos.
/// </summary>
public enum TipoAlerta
{
    Evasao = 1, // faltas consecutivas excessivas
    Atraso = 2  // legado: não gera novos alertas
}
