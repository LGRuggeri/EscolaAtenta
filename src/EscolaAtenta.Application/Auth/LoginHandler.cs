// Handler do comando de Login via MediatR
// 
// FLUXO:
// 1. Busca usuario pelo email (case-insensitive)
// 2. Se nao encontrar => CredenciaisInvalidasException (mensagem genérica)
// 3. Se encontrado mas inativo => CredenciaisInvalidasException
// 4. Valida senha contra hash BCrypt
// 5. Se invalido => CredenciaisInvalidasException
// 6. Se valido => Gera token JWT e retorna LoginResponse
//
// SEGURANCA (AppSec):
// - SEMPRE retorna a mesma mensagem para usuario nao encontrado OU senha incorreta
// - Isso previne enumeração de usuários via timing attacks

using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Exceptions;
using EscolaAtenta.Domain.Interfaces;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Auth;

public class LoginHandler : IRequestHandler<LoginCommand, LoginResponse>
{
    private readonly AppDbContext _dbContext;
    private readonly IAuthService _authService;

    public LoginHandler(AppDbContext dbContext, IAuthService authService)
    {
        _dbContext = dbContext;
        _authService = authService;
    }

    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // Normaliza o email para busca
        var emailNormalizado = request.Email.ToLowerInvariant().Trim();

        // Busca usuario - SO traz usuarios ativos (Global Query Filter)
        // O Global Query Filter de ISoftDeletable ja filtra Ativo = true
        var usuario = await _dbContext.Usuarios
            .FirstOrDefaultAsync(u => u.Email == emailNormalizado, cancellationToken);

        // SEGURANCA: mensagem generica para prevenir enumeração de usuários
        // Nao revela se o email existe ou nao
        if (usuario == null || !usuario.PodeAcessar())
        {
            throw new CredenciaisInvalidasException();
        }

        // Valida a senha contra o hash BCrypt armazenado
        if (!_authService.ValidarSenha(request.Senha, usuario.HashSenha))
        {
            throw new CredenciaisInvalidasException();
        }

        // Credenciais validas - gera o token JWT
        var loginResult = _authService.GerarToken(usuario);

        return new LoginResponse(
            Token: loginResult.Token,
            Email: loginResult.Email,
            Papel: loginResult.Papel,
            ExpiresAt: loginResult.ExpiresAt
        );
    }
}
