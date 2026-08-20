using EscolaAtenta.Domain.Enums;

namespace EscolaAtenta.Application.Alertas.Dtos;

/// <summary>
/// DTO de leitura para alertas de evasão escolar.
///
/// Campo Tipo: retornado como string ("Evasao") para não forçar
/// a representação do enum no contrato REST.
/// </summary>
public record AlertaEvasaoDto(
    Guid Id,
    string NomeAluno,
    string NomeTurma,
    NivelAlertaFalta Nivel,
    string Descricao,
    DateTime DataAlerta,
    bool Resolvido,
    string? ObservacaoResolucao,
    string TituloAmigavel,
    string MensagemAcao,
    string Tipo, // "Evasao" | "Atraso"
    string? ResolvidoPorNome = null,
    DateTime? DataResolucao = null,
    string? JustificativaResolucao = null
);
