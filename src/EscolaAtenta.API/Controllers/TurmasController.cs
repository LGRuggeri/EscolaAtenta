using EscolaAtenta.Application.Turmas.Commands;
using EscolaAtenta.Application.Turmas.DTOs;
using EscolaAtenta.Application.Turmas.Queries;
using EscolaAtenta.Domain.Interfaces;
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
    private readonly IPeriodoLetivoProvider _periodoLetivoProvider;

    public TurmasController(IMediator mediator, IPeriodoLetivoProvider periodoLetivoProvider)
    {
        _mediator = mediator;
        _periodoLetivoProvider = periodoLetivoProvider;
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

    /// <summary>
    /// Migra todos os alunos ativos de uma turma origem para uma turma destino.
    /// </summary>
    [HttpPost("{id:guid}/migrar")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(MigrarTurmaResultadoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MigrarTurma(
        [FromRoute] Guid id,
        [FromBody] MigrarTurmaCommand command,
        CancellationToken ct)
    {
        if (id != command.TurmaOrigemId)
        {
            command = command with { TurmaOrigemId = id };
        }

        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// Retorna o relatório de frequência da turma.
    ///
    /// Contrato atual: informe dataInicio e dataFim.
    /// Contrato legado (compatibilidade OTA com apps não atualizados):
    /// informe anoLetivo e, opcionalmente, periodoLetivo (interpretado como trimestre).
    /// </summary>
    [HttpGet("{id}/relatorio")]
    [Authorize(Roles = "Supervisao,Administrador")]
    [ProducesResponseType(typeof(RelatorioTurmaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetRelatorioTurma(
        [FromRoute] string id,
        [FromQuery] DateTime? dataInicio = null,
        [FromQuery] DateTime? dataFim = null,
        [FromQuery] int? anoLetivo = null,
        [FromQuery] int? periodoLetivo = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest(new { erro = "Informe o identificador da turma." });

        RelatorioTurmaQuery query;

        if (dataInicio.HasValue && dataFim.HasValue)
        {
            query = new RelatorioTurmaQuery(id, dataInicio.Value, dataFim.Value);
        }
        else if (anoLetivo.HasValue)
        {
            var tipoPeriodo = _periodoLetivoProvider.ObterTipoPeriodoLetivo();
            query = RelatorioTurmaQuery.DePeriodoLetivo(id, anoLetivo.Value, tipoPeriodo, periodoLetivo);
        }
        else
        {
            return BadRequest(new { erro = "Informe dataInicio/dataFim ou anoLetivo (com periodoLetivo opcional)." });
        }

        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }
}
