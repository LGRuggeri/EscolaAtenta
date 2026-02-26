using MediatR;
using EscolaAtenta.Application.Turmas.DTOs;

namespace EscolaAtenta.Application.Turmas.Commands;

public record CriarTurmaCommand(string Nome, string Turno, int AnoLetivo) : IRequest<TurmaDto>;
