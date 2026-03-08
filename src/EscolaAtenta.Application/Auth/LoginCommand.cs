// Comando de Login - via MediatR
// Recebe email e senha, retorna token JWT se bem-sucedido
// 
// SEGURANCA (AppSec):
// - NUNCA expor se o erro foi no email ou senha (prevenir enumeração de usuários)
// - Usar mensagem genérica: "Credenciais inválidas"

using MediatR;

namespace EscolaAtenta.Application.Auth;

/// <summary>
/// Command para login de usuario.
/// </summary>
public record LoginCommand(string Email, string Senha) : IRequest<LoginResponse>;

/// <summary>
/// Resposta do login bem-sucedido.
/// </summary>
public record LoginResponse(
    string Token,
    string Email,
    string Papel,
    DateTimeOffset ExpiresAt,
    bool DeveAlterarSenha = false
);
