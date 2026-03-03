using EscolaAtenta.Application.Alertas.Commands;
using EscolaAtenta.Application.Alertas.Dtos;
using EscolaAtenta.Application.Alertas.Queries;
using EscolaAtenta.Application.Common;
using EscolaAtenta.Domain.Enums;
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
    private readonly ILogger<AlertasController> _logger;

    public AlertasController(IMediator mediator, ILogger<AlertasController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Lista alertas de evasão/atraso com paginação server-side.
    ///
    /// Parâmetros:
    /// - pageNumber: página a retornar (1-indexed, default=1)
    /// - pageSize: itens por página (default=20, max=100 — clampado pelo Handler)
    /// - tipo: opcional — filtra por TipoAlerta (Evasao | Atraso)
    /// - nivel: opcional — subfiltro de NivelAlertaFalta. Ignorado pelo backend se tipo ≠ Evasao.
    ///
    /// Resposta: PagedResult{AlertaEvasaoDto} com TotalCount, TotalPages, 
    /// HasNextPage e HasPreviousPage para Infinite Scroll no cliente.
    ///
    /// Status codes:
    /// - 200 OK: sucesso (pode retornar lista vazia com TotalCount=0)
    /// - 401 Unauthorized: token ausente ou expirado
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AlertaEvasaoDto>>> Get(
        [FromQuery] bool apenasNaoResolvidos = true,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] TipoAlerta? tipo = null,
        [FromQuery] NivelAlertaFalta? nivel = null)
    {
        // Logging estruturado: rastreabilidade para monitoramento do novo filtro de nível
        _logger.LogInformation(
            "GET /alertas — ApenasNaoResolvidos={ApenasNaoResolvidos} Tipo={Tipo} Nivel={Nivel} Page={PageNumber}/{PageSize}",
            apenasNaoResolvidos, tipo, nivel, pageNumber, pageSize);

        var query = new GetAlertasQuery(apenasNaoResolvidos, pageNumber, pageSize)
        {
            Tipo = tipo,
            Nivel = nivel,
        };

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
