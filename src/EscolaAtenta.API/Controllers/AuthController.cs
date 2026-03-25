// Controller de Autenticacao - API v1
// 
// ENDPOINTS:
// - POST /api/v1/auth/login - Login com email e senha
//
// SEGURANCA (AppSec):
// - CredenciaisInvalidasException retorna 401 Unauthorized com mensagem generica
// - NUNCA revela se o email existe ou nao (prevencao de enumeração)
// - Rate Limiting (AuthPolicy): 5 req/min por IP para mitigar brute force

using EscolaAtenta.Application.Auth;
using EscolaAtenta.Domain.Exceptions;
using EscolaAtenta.Domain.Interfaces;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MediatR;

namespace EscolaAtenta.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("AuthPolicy")] // Proteção contra brute force: 5 req/min por IP
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AuthController> _logger;

    private readonly AppDbContext _dbContext;
    private readonly IAuthService _authService;
    private readonly ICurrentUserService _currentUser;

    public AuthController(IMediator mediator, ILogger<AuthController> logger, AppDbContext dbContext, IAuthService authService, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _logger = logger;
        _dbContext = dbContext;
        _authService = authService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Login de usuario - retorna JWT token se bem-sucedido.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        // Validacao basica do request
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Senha))
        {
            return BadRequest(new { detail = "Email e senha sao obrigatorios." });
        }

        try
        {
            var command = new LoginCommand(request.Email, request.Senha);
            var result = await _mediator.Send(command, ct);

            _logger.LogInformation("Login bem-sucedido para {Email}", result.Email);

            return Ok(result);
        }
        catch (CredenciaisInvalidasException)
        {
            // SEGURANCA: mensagem generica para prevenir enumeração de usuários
            // Nao revela se o email existe ou nao
            _logger.LogWarning("Tentativa de login falhada para {Email}", request.Email);
            
            return Unauthorized(new { 
                type = "https://tools.ietf.org/html/rfc7807",
                title = "Unauthorized",
                status = 401,
                detail = "Credenciais invalidas."
            });
        }
    }

    /// <summary>
    /// Troca de senha — obrigatoria no primeiro login ou quando solicitado.
    /// Requer autenticacao (JWT valido).
    /// </summary>
    [HttpPut("trocar-senha")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> TrocarSenha([FromBody] TrocarSenhaRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.NovaSenha) || request.NovaSenha.Length < 8)
            return BadRequest(new { detail = "A nova senha deve ter pelo menos 8 caracteres." });

        if (!_currentUser.EstaAutenticado || !Guid.TryParse(_currentUser.UsuarioId, out var usuarioId))
            return Unauthorized();

        var usuario = await _dbContext.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct);
        if (usuario == null) return Unauthorized();

        var novoHash = _authService.GerarHashSenha(request.NovaSenha);
        usuario.AlterarSenha(novoHash);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Senha alterada com sucesso para {Email}", usuario.Email);
        return NoContent();
    }

    /// <summary>
    /// Renova o JWT usando um Refresh Token válido.
    /// Implementa rotação: o refresh token antigo é revogado e um novo é emitido.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var refreshToken = await _dbContext.RefreshTokens
            .Include(rt => rt.Usuario)
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken, ct);

        if (refreshToken == null || !refreshToken.EstaValido() || !refreshToken.Usuario.PodeAcessar())
            return Unauthorized(new { detail = "Refresh token inválido ou expirado." });

        // Rotação: revoga o token atual e emite um novo
        refreshToken.Revogado = true;

        var novoRefreshToken = new EscolaAtenta.Domain.Entities.RefreshToken
        {
            UsuarioId = refreshToken.UsuarioId,
            Token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64)),
            ExpiraEm = DateTimeOffset.UtcNow.AddDays(30)
        };
        _dbContext.RefreshTokens.Add(novoRefreshToken);
        await _dbContext.SaveChangesAsync(ct);

        var loginResult = _authService.GerarToken(refreshToken.Usuario);

        return Ok(new LoginResponse(
            Token: loginResult.Token,
            Email: loginResult.Email,
            Papel: loginResult.Papel,
            ExpiresAt: loginResult.ExpiresAt,
            RefreshToken: novoRefreshToken.Token
        ));
    }
}

/// <summary>
/// Request de login.
/// </summary>
public record LoginRequest(string Email, string Senha);

public record TrocarSenhaRequest(string NovaSenha);

public record RefreshRequest(string RefreshToken);
