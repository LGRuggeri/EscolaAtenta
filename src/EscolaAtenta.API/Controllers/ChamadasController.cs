using EscolaAtenta.Application.Chamadas.Commands;
using EscolaAtenta.Application.Chamadas.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EscolaAtenta.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
[EnableRateLimiting("GlobalPolicy")]
public class ChamadasController : ControllerBase
{
    private readonly IMediator _mediator;

    public ChamadasController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Realiza a chamada em lote para uma Turma.
    /// Se já existir chamada para o dia informado e o prazo de 7 dias não tiver expirado,
    /// os registros existentes serão atualizados.
    /// </summary>
    [HttpPost("realizar")]
    [ProducesResponseType(typeof(RealizarChamadaResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RealizarChamada([FromBody] RealizarChamadaCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// Consulta a chamada de uma turma em uma data específica.
    /// Retorna os registros de presença e indica se a chamada ainda pode ser editada.
    /// </summary>
    [HttpGet("turma/{turmaId:guid}/dia/{data:datetime}")]
    [ProducesResponseType(typeof(ChamadaPorDiaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterChamadaPorDia(Guid turmaId, DateTime data, CancellationToken ct)
    {
        var result = await _mediator.Send(new ObterChamadaPorTurmaEDiaQuery(turmaId, data), ct);

        if (result is null)
            return NotFound(new { mensagem = $"Nenhuma chamada encontrada para a turma no dia {data:dd/MM/yyyy}." });

        return Ok(result);
    }
}
