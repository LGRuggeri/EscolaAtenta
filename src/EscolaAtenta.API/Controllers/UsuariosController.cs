using EscolaAtenta.Application.Usuarios.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EscolaAtenta.API.Controllers;

/// <summary>
/// Controller exclusivo para gerenciamento de contas do sistema.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "Administrador")] // APENAS DEUS! digo, apenas Administrador.
public class UsuariosController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsuariosController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Cria um novo usuário no sistema atribuindo Senha Inicial forte e segura.
    /// Exclusivo de Administradores.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(UsuarioCriadoResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CriarUsuario([FromBody] CriarUsuarioCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Email))
        {
            return BadRequest(new { detail = "E-mail é obrigatório." });
        }

        try
        {
            var resultado = await _mediator.Send(command, ct);
            return StatusCode(StatusCodes.Status201Created, resultado);
        }
        catch (InvalidOperationException ex)
        {
            // O e-mail já existe
            return BadRequest(new { detail = ex.Message });
        }
    }
}
