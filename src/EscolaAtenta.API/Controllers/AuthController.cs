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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MediatR;

namespace EscolaAtenta.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("AuthPolicy")] // Proteção contra brute force: 5 req/min por IP
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IMediator mediator, ILogger<AuthController> logger)
    {
        _mediator = mediator;
        _logger = logger;
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
}

/// <summary>
/// Request de login.
/// </summary>
public record LoginRequest(string Email, string Senha);
