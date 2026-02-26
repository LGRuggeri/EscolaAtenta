using EscolaAtenta.Domain.Enums;
using MediatR;

namespace EscolaAtenta.Application.Chamadas.Commands;

public record RegistroAlunoDto(Guid AlunoId, StatusPresenca Status);

public record RealizarChamadaCommand(
    Guid TurmaId,
    Guid ResponsavelId,
    List<RegistroAlunoDto> Alunos
) : IRequest<RealizarChamadaResult>;

public record RealizarChamadaResult(Guid ChamadaId, int AlertasGerados);
