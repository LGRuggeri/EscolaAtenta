using EscolaAtenta.Domain.Enums;
using MediatR;

namespace EscolaAtenta.Application.Chamadas.Commands;

public record RegistroAlunoDto(Guid AlunoId, StatusPresenca Status);

public record RealizarChamadaCommand(
    Guid TurmaId,
    Guid ResponsavelId,
    List<RegistroAlunoDto> Alunos,
    DateTimeOffset? Data = null
) : IRequest<RealizarChamadaResult>;

public record RealizarChamadaResult(Guid ChamadaId, int AlertasGerados, bool ChamadaExistenteAtualizada = false);
