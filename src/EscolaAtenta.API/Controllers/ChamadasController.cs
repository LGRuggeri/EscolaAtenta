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
    /// A data pode ser enviada em qualquer formato ISO 8601 suportado por DateTime.Parse (ex: 2026-01-15T00:00:00Z).
    /// </summary>
    [HttpGet("turma/{turmaId:guid}/dia/{data}")]
    [ProducesResponseType(typeof(ChamadaPorDiaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterChamadaPorDia(Guid turmaId, string data, CancellationToken ct)
    {
        if (!DateTime.TryParse(data, out var dataParsed))
        {
            return BadRequest(new { mensagem = $"Data inválida: '{data}'." });
        }

        var result = await _mediator.Send(new ObterChamadaPorTurmaEDiaQuery(turmaId, dataParsed), ct);

        if (result is null)
            return NotFound(new { mensagem = $"Nenhuma chamada encontrada para a turma no dia {dataParsed:dd/MM/yyyy}." });

        return Ok(result);
    }
}
