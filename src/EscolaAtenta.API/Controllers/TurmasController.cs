using EscolaAtenta.Application.Turmas.Commands;
using EscolaAtenta.Application.Turmas.DTOs;
using EscolaAtenta.Application.Turmas.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EscolaAtenta.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
[EnableRateLimiting("GlobalPolicy")]
public class TurmasController : ControllerBase
{
    private readonly IMediator _mediator;

    public TurmasController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Cadastra uma nova Turma (Série / Classe).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TurmaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CriarTurma([FromBody] CriarTurmaCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetTurmas), new { id = result.Id }, result);
    }

    /// <summary>
    /// Lista todas as Turmas cadastradas em ordem alfabética.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TurmaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTurmas(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTurmasQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Atualiza uma Turma existente.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AtualizarTurma([FromRoute] Guid id, [FromBody] AtualizarTurmaCommand command, CancellationToken ct)
    {
        if (id != command.Id)
        {
            command = command with { Id = id };
        }
        await _mediator.Send(command, ct);
        return NoContent();
    }
}
