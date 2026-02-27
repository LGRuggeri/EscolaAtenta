using EscolaAtenta.Application.Alertas.Commands;
using EscolaAtenta.Application.Alertas.Dtos;
using EscolaAtenta.Application.Alertas.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EscolaAtenta.API.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class AlertasController : ControllerBase
{
    private readonly IMediator _mediator;

    public AlertasController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AlertaEvasaoDto>>> Get([FromQuery] bool apenasNaoResolvidos = true)
    {
        var query = new GetAlertasQuery(apenasNaoResolvidos);
        var alertas = await _mediator.Send(query);
        return Ok(alertas);
    }

    [Authorize(Roles = "Supervisao,Administrador")]
    [HttpPatch("{id}/resolver")]
    public async Task<IActionResult> ResolverAlerta(Guid id, [FromBody] ResolverAlertaCommand command)
    {
        command.AlertaId = id;
        var result = await _mediator.Send(command);
        if (!result) return NotFound("Alerta não encontrado.");
        return NoContent();
    }
}
