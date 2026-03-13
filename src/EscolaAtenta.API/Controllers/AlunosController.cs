using EscolaAtenta.Application.Alunos.Commands;
using EscolaAtenta.Application.Alunos.DTOs;
using EscolaAtenta.Application.Alunos.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EscolaAtenta.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
[EnableRateLimiting("GlobalPolicy")]
public class AlunosController : ControllerBase
{
    private readonly IMediator _mediator;

    public AlunosController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Cadastra um novo Aluno vinculado a uma Turma.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AlunoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CriarAluno([FromBody] CriarAlunoCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetAlunosPorTurma), new { turmaId = result.TurmaId }, result);
    }

    /// <summary>
    /// Lista todos os Alunos de uma determinada Turma.
    /// </summary>
    [HttpGet("turma/{turmaId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<AlunoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAlunosPorTurma([FromRoute] Guid turmaId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAlunosPorTurmaQuery(turmaId), ct);
        return Ok(result);
    }

    /// <summary>
    /// Atualiza um Aluno existente.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AtualizarAluno([FromRoute] Guid id, [FromBody] AtualizarAlunoCommand command, CancellationToken ct)
    {
        if (id != command.Id)
        {
            command = command with { Id = id };
        }
        await _mediator.Send(command, ct);
        return NoContent();
    }

    /// <summary>
    /// Retorna o histórico cronológico de presenças, faltas e atrasos de um Aluno.
    /// </summary>
    [HttpGet("{id}/historico-presencas")]
    [ProducesResponseType(typeof(IEnumerable<HistoricoPresencaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistoricoPresencas(
        [FromRoute] string id,
        [FromQuery] int dias = 7,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetHistoricoPresencasAlunoQuery(id, dias), ct);
        return Ok(result);
    }
}
