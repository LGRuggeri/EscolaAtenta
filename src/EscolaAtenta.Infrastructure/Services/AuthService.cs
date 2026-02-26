// Implementacao do servico de autenticacao
// USA BCrypt para hash de senhas e JWT para tokens de acesso
// 
// SEGURANCA (AppSec):
// - BCrypt: trabalho constante (cost factor 10) - resistente a rainbow tables e GPU attacks
// - Nao usa MD5/SHA1 - apenas algoritmos Designed for password hashing
// - JWT: expiracao curta (1h) + renovacao via refresh token (futuro)

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace EscolaAtenta.Infrastructure.Services;

/// <summary>
/// Implementacao do IAuthService usando BCrypt + JWT.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IConfiguration _configuration;
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expiryMinutes;

    public AuthService(IConfiguration configuration)
    {
        _configuration = configuration;
        
        // Le configuracoes do JWT - fallback para desenvolvimento
        var jwtSection = _configuration.GetSection("Jwt");
        _secretKey = jwtSection["SecretKey"] ?? "ChaveSecretaDeDesenvolvimentoMuitoLongaParaTestes123456!";
        _issuer = jwtSection["Issuer"] ?? "EscolaAtenta";
        _audience = jwtSection["Audience"] ?? "EscolaAtenta";
        _expiryMinutes = int.TryParse(jwtSection["ExpiryMinutes"], out var mins) ? mins : 60;
    }

    public LoginResult GerarToken(Usuario usuario)
    {
        // CLAIMS: informacoes sobre o usuario embeded no token
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, usuario.Email),
            new Claim(ClaimTypes.Role, usuario.Papel.ToString()), // OBRIGATÓRIO: Uso da URI completa nativa
            new Claim("role", usuario.Papel.ToString()), // Fallback explícito para JS/WASM
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Chave simetrica - em producao, usar RSA ou ECDSA com chave armazenada em vault
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiresAt = DateTime.UtcNow.AddMinutes(_expiryMinutes);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials
        );

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenString = tokenHandler.WriteToken(token);

        return new LoginResult(
            Token: tokenString,
            Email: usuario.Email,
            Papel: usuario.Papel.ToString(),
            ExpiresAt: expiresAt
        );
    }

    /// <summary>
    /// Valida senha usando BCrypt - comparacao de tempo constante.
    /// </summary>
    public bool ValidarSenha(string senha, string hashArmazenado)
    {
        if (string.IsNullOrEmpty(senha) || string.IsNullOrEmpty(hashArmazenado))
            return false;

        // BCrypt.Verify retorna true se a senha corresponde ao hash
        // Implementacao segura contra timing attacks
        return BCrypt.Net.BCrypt.Verify(senha, hashArmazenado);
    }

    /// <summary>
    /// Gera hash BCrypt com work factor 10 (padrao seguro).
    /// </summary>
    public string GerarHashSenha(string senha)
    {
        if (string.IsNullOrEmpty(senha))
            throw new ArgumentException("Senha nao pode ser vazia.", nameof(senha));

        // Work factor 10 = 2^10 iteracoes = ~100ms - balance entre seguranca e UX
        return BCrypt.Net.BCrypt.HashPassword(senha, workFactor: 10);
    }
}
