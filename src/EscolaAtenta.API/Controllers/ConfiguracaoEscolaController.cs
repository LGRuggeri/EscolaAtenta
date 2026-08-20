using EscolaAtenta.Application.Escola.Commands;
using EscolaAtenta.Application.Escola.DTOs;
using EscolaAtenta.Application.Escola.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EscolaAtenta.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
[EnableRateLimiting("GlobalPolicy")]
public class ConfiguracaoEscolaController : ControllerBase
{
    private readonly IMediator _mediator;

    public ConfiguracaoEscolaController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Retorna a configuração global da escola.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ConfiguracaoEscolaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await _mediator.Send(new ObterConfiguracaoEscolaQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Retorna os períodos letivos já iniciados para um ano letivo,
    /// de acordo com o tipo de período configurado na escola.
    /// </summary>
    [HttpGet("periodos-disponiveis")]
    [ProducesResponseType(typeof(PeriodosLetivosDisponiveisDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPeriodosDisponiveis([FromQuery] int anoLetivo, CancellationToken ct)
    {
        var result = await _mediator.Send(new ObterPeriodosLetivosDisponiveisQuery(anoLetivo), ct);
        return Ok(result);
    }

    /// <summary>
    /// Atualiza a configuração global da escola.
    /// </summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Atualizar([FromBody] AtualizarConfiguracaoEscolaCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return NoContent();
    }
}
