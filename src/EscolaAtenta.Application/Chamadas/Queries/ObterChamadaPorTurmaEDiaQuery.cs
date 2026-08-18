using MediatR;

namespace EscolaAtenta.Application.Chamadas.Queries;

/// <summary>
/// Consulta uma chamada existente para uma turma em uma data específica,
/// retornando os registros de presença e se ainda é permitido editá-la.
/// </summary>
public record ObterChamadaPorTurmaEDiaQuery(string TurmaId, DateTime Data) : IRequest<ChamadaPorDiaDto?>;

public class ChamadaPorDiaDto
{
    public Guid ChamadaId { get; set; }
    public DateTimeOffset DataHora { get; set; }
    public Guid ResponsavelId { get; set; }
    public bool PodeEditar { get; set; }
    public List<RegistroPresencaPorDiaDto> Registros { get; set; } = [];
}

public class RegistroPresencaPorDiaDto
{
    public string AlunoId { get; set; } = string.Empty;
    public string NomeAluno { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
