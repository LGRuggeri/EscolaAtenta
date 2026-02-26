using MediatR;
using EscolaAtenta.Application.Alunos.DTOs;

namespace EscolaAtenta.Application.Alunos.Commands;

public record CriarAlunoCommand(string Nome, string? Matricula, Guid TurmaId) : IRequest<AlunoDto>;
