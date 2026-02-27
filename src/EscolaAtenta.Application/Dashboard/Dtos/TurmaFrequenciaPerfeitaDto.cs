using System;

namespace EscolaAtenta.Application.Dashboard.Dtos;

public record TurmaFrequenciaPerfeitaDto(
    Guid TurmaId,
    string NomeTurma,
    int QuantidadeAulasMinistradas
);
