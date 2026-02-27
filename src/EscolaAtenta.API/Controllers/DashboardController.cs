// Controller do Dashboard da Supervisao
// Fornece endpoints para visualização de alunos com faltas
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using EscolaAtenta.Application.Alunos.Queries;

namespace EscolaAtenta.API.Controllers;

/// <summary>
/// Controller para Dashboard da Supervisao.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize] // Requer autenticação
public class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;

    public DashboardController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Obtém lista de alunos com faltas, ordenada por TotalFaltas DESC.
    /// </summary>
    /// <param name="turmaId">Filtro opcional por turma.</param>
    [HttpGet("alunos-com-faltas")]
    [ProducesResponseType(typeof(IReadOnlyList<AlunoComFaltasDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAlunosComFaltas([FromQuery] Guid? turmaId = null)
    {
        var query = new GetAlunosComFaltasQuery(turmaId);
        var resultados = await _mediator.Send(query);
        return Ok(resultados);
    }

    /// <summary>
    /// Identifica Turmas com Frequência Perfeita (100% de presença) em um determinado período.
    /// Exclui qualquer turma que teve registros de Falta ou Atraso no período indicado.
    /// </summary>
    [HttpGet("turmas-frequencia-perfeita")]
    [ProducesResponseType(typeof(IEnumerable<EscolaAtenta.Application.Dashboard.Dtos.TurmaFrequenciaPerfeitaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetTurmasFrequenciaPerfeita([FromQuery] DateTime dataInicio, [FromQuery] DateTime dataFim)
    {
        var query = new EscolaAtenta.Application.Dashboard.Queries.GetTurmasFrequenciaPerfeitaQuery(dataInicio, dataFim);
        var resultados = await _mediator.Send(query);
        return Ok(resultados);
    }
}
