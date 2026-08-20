namespace EscolaAtenta.Domain.Enums;

/// <summary>
/// Classifica o tipo de alerta gerado pelo sistema.
/// 
/// Hoje o sistema gera apenas alertas de evasão (faltas consecutivas excessivas).
/// O valor 1 é mantido para compatibilidade com registros pré-existentes.
/// </summary>
public enum TipoAlerta
{
    Evasao = 1 // faltas consecutivas excessivas
}
