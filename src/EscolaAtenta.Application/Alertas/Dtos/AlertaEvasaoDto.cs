using EscolaAtenta.Domain.Enums;

namespace EscolaAtenta.Application.Alertas.Dtos;

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
    string MensagemAcao
);
