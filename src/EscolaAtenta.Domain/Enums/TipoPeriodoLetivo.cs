namespace EscolaAtenta.Domain.Enums;

/// <summary>
/// Representa a divisão do ano letivo adotada pela escola.
/// 
/// Decisão: valores inteiros explícitos para garantir estabilidade do schema.
/// Nunca reordenar ou remover valores — apenas adicionar novos.
/// </summary>
public enum TipoPeriodoLetivo
{
    Bimestre = 0,
    Trimestre = 1,
    Semestre = 2
}
