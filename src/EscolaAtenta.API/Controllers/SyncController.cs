using EscolaAtenta.Application.Chamadas.Commands;
using EscolaAtenta.Application.Chamadas.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EscolaAtenta.API.Controllers;

/// <summary>
/// Endpoint de sincronização offline-first (WatermelonDB ↔ PostgreSQL).
/// Pull: baixa turmas e alunos atualizados para o celular.
/// Push: recebe registros de presença criados offline e persiste no banco.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
[EnableRateLimiting("GlobalPolicy")]
public class SyncController : ControllerBase
{
    private readonly IMediator _mediator;

    public SyncController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Retorna o delta de Turmas e Alunos no formato WatermelonDB Sync Protocol.
    /// Se lastPulledAt = 0 ou ausente: retorna tudo (primeiro login).
    /// Caso contrário: retorna apenas o que mudou desde o timestamp.
    /// </summary>
    [HttpGet("pull")]
    [ProducesResponseType(typeof(SyncPullResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Pull([FromQuery] long? lastPulledAt, CancellationToken ct)
    {
        var result = await _mediator.Send(new SyncPullQuery(lastPulledAt), ct);
        return Ok(result);
    }

    /// <summary>
    /// Recebe registros de presença criados/atualizados offline e os sincroniza.
    /// Idempotente: registros já sincronizados são ignorados silenciosamente.
    /// </summary>
    [HttpPost("push")]
    [ProducesResponseType(typeof(SyncPushResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Push([FromBody] SyncPushCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }
}
