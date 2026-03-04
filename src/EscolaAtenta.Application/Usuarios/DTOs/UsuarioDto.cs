namespace EscolaAtenta.Application.Usuarios.DTOs;

/// <summary>
/// Read Model do Usuário para o painel administrativo de listagem.
/// Papel é exposto como string para facilitar exibição no front-end.
/// Ativo é exposto para o front-end diferenciar usuários desativados.
/// </summary>
public record UsuarioDto
{
    public Guid Id { get; init; }
    public string Nome { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Papel { get; init; } = string.Empty;
    public bool Ativo { get; init; }
}
