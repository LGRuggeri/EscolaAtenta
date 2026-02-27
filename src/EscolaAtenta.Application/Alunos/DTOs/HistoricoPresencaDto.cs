using System;

namespace EscolaAtenta.Application.Alunos.DTOs;

public record HistoricoPresencaDto(
    DateTime DataDaChamada, 
    string Status, 
    string? Justificativa // Prepared for future use if Justifications are added to the DB model. Currently will be empty.
);
