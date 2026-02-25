using EscolaAtenta.Domain.Enums;
using MediatR;

namespace EscolaAtenta.Application.Chamadas.Commands;

/// <summary>
/// Command para registrar a presença de um aluno em uma chamada.
/// 
/// Decisão: Usar MediatR apenas para o core business domain (registro de presença
/// e geração de alertas). Cadastros simples (CRUD de Turma, Aluno) não precisam
/// de MediatR — controllers podem chamar o DbContext diretamente via repositório.
/// 
/// O command é imutável (record) para garantir thread-safety e facilitar testes.
/// </summary>
public sealed record RegistrarPresencaCommand(
    Guid ChamadaId,
    Guid AlunoId,
    StatusPresenca Status
) : IRequest<RegistrarPresencaResult>;

/// <summary>
/// Resultado do registro de presença.
/// </summary>
public sealed record RegistrarPresencaResult(
    Guid RegistroPresencaId,
    bool AlertaGerado
);
