using EscolaAtenta.Domain.Enums;

namespace EscolaAtenta.Domain.Interfaces;

/// <summary>
/// Fornece a configuração de período letivo da escola.
/// 
/// A configuração foi removida do banco (entidade ConfiguracaoEscola),
/// mas clientes legados ainda enviam periodoLetivo no relatório.
/// Esta abstração permite que o Application reconstrua o intervalo
/// correto sem conhecer a origem da configuração (appsettings, etc.).
/// </summary>
public interface IPeriodoLetivoProvider
{
    TipoPeriodoLetivo ObterTipoPeriodoLetivo();
}
