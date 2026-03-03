using EscolaAtenta.Application.Alertas.Commands;
using EscolaAtenta.Application.Alertas.Dtos;
using EscolaAtenta.Application.Alertas.Queries;
using EscolaAtenta.Application.Common;
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

    /// <summary>
    /// Lista alertas de evasão/atraso com paginação server-side.
    ///
    /// Parâmetros de paginação:
    /// - pageNumber: página a retornar (1-indexed, default=1)
    /// - pageSize: itens por página (default=20, max=100 — clampado pelo Handler)
    ///
    /// Resposta: PagedResult{AlertaEvasaoDto} com TotalCount, TotalPages, 
    /// HasNextPage e HasPreviousPage para suporte a Infinite Scroll no cliente.
    ///
    /// Status codes:
    /// - 200 OK: sucesso (pode retornar lista vazia com TotalCount=0)
    /// - 401 Unauthorized: token ausente ou expirado
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AlertaEvasaoDto>>> Get(
        [FromQuery] bool apenasNaoResolvidos = true,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new GetAlertasQuery(apenasNaoResolvidos, pageNumber, pageSize);
        var resultado = await _mediator.Send(query);
        return Ok(resultado);
    }

    /// <summary>
    /// Resolve (fecha) um alerta de evasão ou atraso.
    /// Restrito para Supervisão e Administrador.
    ///
    /// Status codes:
    /// - 204 No Content: resolvido com sucesso
    /// - 401 Unauthorized: token ausente
    /// - 403 Forbidden: papel Monitor tentando resolver
    /// - 404 Not Found: alertaId não existe
    /// </summary>
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
