// Interface para servico de autenticacao - definida no Domain para ser usada por qualquer camada
// A implementacao fica na Infrastructure (BCrypt + JWT)
using EscolaAtenta.Domain.Entities;

namespace EscolaAtenta.Domain.Interfaces;

/// <summary>
/// Resultado do login bem-sucedido
/// </summary>
public record LoginResult(
    string Token,
    string Email,
    string Papel,
    DateTimeOffset ExpiresAt
);

/// <summary>
/// Interface para servico de autenticacao.
/// Implementacao fica na Infrastructure (BCrypt para hash, JWT para token).
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Gera um token JWT para o usuario autenticado.
    /// </summary>
    LoginResult GerarToken(Usuario usuario);

    /// <summary>
    /// Valida a senha contra o hash armazenado.
    /// Usa BCrypt para comparacao segura (resistente a timing attacks).
    /// </summary>
    bool ValidarSenha(string senha, string hashArmazenado);

    /// <summary>
    /// Gera o hash de uma senha usando BCrypt.
    /// </summary>
    string GerarHashSenha(string senha);
}
