namespace EscolaAtenta.Domain.Enums;

/// <summary>
/// Tipo de divisão do ano letivo usado pela escola.
/// Mantido como enum legado para compatibilidade com clientes antigos
/// que ainda enviam periodoLetivo no relatório por período.
/// </summary>
public enum TipoPeriodoLetivo
{
    Bimestre = 1,
    Trimestre = 2,
    Semestre = 3
}
