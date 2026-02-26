using EscolaAtenta.Application.Chamadas.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EscolaAtenta.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ChamadasController : ControllerBase
{
    private readonly IMediator _mediator;

    public ChamadasController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Realiza a chamada em lote para uma Turma.
    /// </summary>
    [HttpPost("realizar")]
    [ProducesResponseType(typeof(RealizarChamadaResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RealizarChamada([FromBody] RealizarChamadaCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }
}
