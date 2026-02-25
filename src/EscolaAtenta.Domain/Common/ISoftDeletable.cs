namespace EscolaAtenta.Domain.Common;

/// <summary>
/// Contrato para entidades que suportam exclusão lógica (Soft Delete).
/// 
/// Decisão: Usar campo "Ativo" em vez de "IsDeleted" para que o valor padrão
/// (false para bool) não cause exclusão acidental. Um novo registro nasce com
/// Ativo = true, que é semanticamente correto e seguro.
/// 
/// O EF Core aplicará Global Query Filters baseados nesta interface,
/// garantindo que registros inativos nunca apareçam em queries normais.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>
    /// Indica se o registro está ativo. Registros inativos são tratados
    /// como excluídos logicamente e filtrados automaticamente pelo EF Core.
    /// </summary>
    bool Ativo { get; }

    /// <summary>
    /// Data e hora da exclusão lógica. Null enquanto o registro estiver ativo.
    /// </summary>
    DateTimeOffset? DataExclusao { get; }

    /// <summary>
    /// Identificador do usuário que realizou a exclusão lógica.
    /// </summary>
    string? UsuarioExclusao { get; }
}
