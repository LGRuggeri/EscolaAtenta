namespace EscolaAtenta.Domain.Enums;

/// <summary>
/// Status de presença de um aluno em uma chamada.
/// 
/// Decisão: Valores inteiros explícitos para garantir estabilidade do schema
/// do banco de dados. Nunca reordenar ou remover valores — apenas adicionar novos.
/// 
/// Justificada: Falta com justificativa oficial (atestado médico, etc.).
/// Pode ou não contar para o limite de faltas dependendo da política da escola.
/// </summary>
public enum StatusPresenca
{
    Presente = 0,
    Falta = 1,

    /// <summary>
    /// Falta com justificativa oficial apresentada.
    /// A contagem para evasão é configurável por política da escola.
    /// </summary>
    FaltaJustificada = 2,

    /// <summary>
    /// Ausente sem justificativa (similar à falta)
    /// </summary>
    Ausente = 3,

    /// <summary>
    /// Atraso do aluno
    /// </summary>
    Atraso = 4
}
