namespace EscolaAtenta.Application.Usuarios.DTOs;

/// <summary>
/// Read Model de detalhe de um Usuario para o painel administrativo.
/// </summary>
public record UsuarioDetalheDto
{
    public Guid Id { get; init; }
    public string Nome { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Papel { get; init; } = string.Empty;
    public bool Ativo { get; init; }
}
