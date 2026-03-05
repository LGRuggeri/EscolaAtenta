using EscolaAtenta.Application.Common;
using EscolaAtenta.Application.Usuarios.Commands;
using EscolaAtenta.Application.Usuarios.DTOs;
using EscolaAtenta.Application.Usuarios.Queries;
using EscolaAtenta.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EscolaAtenta.API.Controllers;

/// <summary>
/// Controller exclusivo para gerenciamento de contas do sistema.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "Administrador")] // APENAS DEUS! digo, apenas Administrador.
[EnableRateLimiting("GlobalPolicy")]
public class UsuariosController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsuariosController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lista usuários do sistema com paginação e filtros opcionais.
    /// Exclusivo de Administradores.
    ///
    /// Filtros via query string:
    /// - searchTerm: busca parcial em Nome ou Email (LIKE)
    /// - papel: filtra por PapelUsuario (Monitor=1, Supervisao=2, Administrador=3)
    ///
    /// Paginação: pageNumber (1-indexed, default=1), pageSize (default=20, max=100).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UsuarioDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<UsuarioDto>>> ListarUsuarios(
        [FromQuery] string? searchTerm = null,
        [FromQuery] PapelUsuario? papel = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetUsuariosQuery(pageNumber, pageSize)
        {
            SearchTerm = searchTerm,
            Papel = papel
        };

        var resultado = await _mediator.Send(query, ct);
        return Ok(resultado);
    }

    /// <summary>
    /// Retorna detalhes de um usuário específico por ID.
    /// Exclusivo de Administradores.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UsuarioDetalheDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterUsuario(Guid id, CancellationToken ct)
    {
        var resultado = await _mediator.Send(new GetUsuarioByIdQuery(id), ct);

        if (resultado is null)
            return NotFound(new { detail = $"Usuário com ID {id} não encontrado." });

        return Ok(resultado);
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

    /// <summary>
    /// Atualiza Nome e Papel de um usuário existente.
    /// E-mail não é alterável para preservar a identidade.
    /// Exclusivo de Administradores.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AtualizarUsuario(
        Guid id,
        [FromBody] AtualizarUsuarioRequest request,
        CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new AtualizarUsuarioCommand(id, request.Nome, request.Papel), ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { detail = $"Usuário com ID {id} não encontrado." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    /// <summary>
    /// Alterna o status (Ativo/Inativo) de um usuário — soft delete.
    /// Se ativo, desativa. Se inativo, reativa.
    /// Exclusivo de Administradores.
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AlternarStatusUsuario(Guid id, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new AlternarStatusUsuarioCommand(id), ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { detail = $"Usuário com ID {id} não encontrado." });
        }
    }
}

/// <summary>
/// Request DTO para o PUT de atualização — separa o contrato da API do Command interno.
/// </summary>
public record AtualizarUsuarioRequest(string Nome, PapelUsuario Papel);
