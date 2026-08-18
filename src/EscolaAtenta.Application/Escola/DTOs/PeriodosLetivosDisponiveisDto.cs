using EscolaAtenta.Domain.Enums;

namespace EscolaAtenta.Application.Escola.DTOs;

public record PeriodosLetivosDisponiveisDto(
    TipoPeriodoLetivo TipoPeriodoLetivo,
    IReadOnlyList<PeriodoLetivoDisponivelDto> Periodos);
