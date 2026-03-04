namespace EscolaAtenta.Application.Alertas.Dtos;

/// <summary>
/// DTO de leitura dedicado à tela de Auditoria de Alertas.
///
/// Separado do AlertaEvasaoDto intencionalmente: o contrato de auditoria
/// expõe campos de resolução (ResolvidoPor por nome, MotivoResolucao)
/// que não fazem sentido na listagem de alertas ativos.
///
/// DataResolucao é não-nula aqui pois o Read Model filtra apenas
/// alertas onde Resolvido == true — invariante garantida pelo Handler.
/// </summary>
public record AuditoriaAlertaDto
{
    public Guid Id { get; init; }
    public string NomeAluno { get; init; } = string.Empty;
    public string TipoAlerta { get; init; } = string.Empty; // "Evasao" | "Atraso"
    public DateTimeOffset DataResolucao { get; init; }
    public string ResolvidoPor { get; init; } = string.Empty; // E-mail/nome do usuário resolvedor
    public string MotivoResolucao { get; init; } = string.Empty;
    public string NivelAlerta { get; init; } = string.Empty; // "Vermelho", "Preto", etc.
    public DateTimeOffset DataAlerta { get; init; }
}
