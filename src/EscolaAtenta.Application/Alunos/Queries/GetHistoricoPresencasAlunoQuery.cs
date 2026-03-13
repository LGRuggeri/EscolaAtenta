using EscolaAtenta.Application.Alunos.DTOs;
using MediatR;
using System;
using System.Collections.Generic;

namespace EscolaAtenta.Application.Alunos.Queries;

/// <summary>
/// AlunoIdOuExterno pode ser o GUID real do banco ou o ID local do WatermelonDB.
/// O handler resolve via SyncLog quando necessário.
/// </summary>
public record GetHistoricoPresencasAlunoQuery(string AlunoIdOuExterno, int Dias = 7) : IRequest<IEnumerable<HistoricoPresencaDto>>;
