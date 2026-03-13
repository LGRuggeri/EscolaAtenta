namespace EscolaAtenta.Domain.Entities;

/// <summary>
/// Token de renovação de sessão — permite ao app renovar o JWT expirado
/// sem forçar novo login. Válido por 30 dias, uso único (rotação automática).
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset ExpiraEm { get; set; }
    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;
    public bool Revogado { get; set; } = false;

    public Usuario Usuario { get; set; } = null!;

    public bool EstaValido() => !Revogado && ExpiraEm > DateTimeOffset.UtcNow;
}
