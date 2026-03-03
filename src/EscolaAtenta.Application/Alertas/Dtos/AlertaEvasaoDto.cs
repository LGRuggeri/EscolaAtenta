using EscolaAtenta.Domain.Enums;

namespace EscolaAtenta.Application.Alertas.Dtos;

/// <summary>
/// DTO de leitura para alertas escolares (evasão e atraso).
/// 
/// Campo Tipo: retornado como string ("Evasao" | "Atraso") para não forçar
/// a representação do enum no contrato REST — o frontend pode exibir
/// ícones/cores distintos baseado nesse campo.
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
    string Tipo // "Evasao" | "Atraso"
);
