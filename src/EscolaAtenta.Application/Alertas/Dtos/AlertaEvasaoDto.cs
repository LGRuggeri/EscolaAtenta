using EscolaAtenta.Domain.Enums;

namespace EscolaAtenta.Application.Alertas.Dtos;

public record AlertaEvasaoDto(
    Guid Id,
    Guid? AlunoId,
    Guid? TurmaId,
    NivelAlertaFalta Nivel,
    string Descricao,
    DateTime DataAlerta,
    bool Resolvido,
    string? ObservacaoResolucao
);
