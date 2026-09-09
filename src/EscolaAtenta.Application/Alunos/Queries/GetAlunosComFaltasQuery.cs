using EscolaAtenta.Domain.Interfaces;
using EscolaAtenta.Application.Common;
// Query para obter alunos com informações de faltas
// Usada pelo Dashboard da Diretoria para visualização de alertas
//
// Retorna lista ordenada por TotalFaltas DESC (maior para menor)
// Permite filtrar por TurmaId
using MediatR;
using Microsoft.EntityFrameworkCore;
using EscolaAtenta.Infrastructure.Data;

namespace EscolaAtenta.Application.Alunos.Queries;

/// <summary>
/// Query para buscar alunos com dados de faltas.
/// </summary>
public record GetAlunosComFaltasQuery(Guid? TurmaId = null) : IRequest<IReadOnlyList<AlunoComFaltasDto>>;

/// <summary>
/// DTO de resposta com dados do aluno e suas faltas.
/// </summary>
public record AlunoComFaltasDto(
    Guid Id,
    string Nome,
    string Matricula,
    Guid TurmaId,
    string NomeTurma,
    int FaltasConsecutivasAtuais,
    int TotalFaltas,
    string NivelAlerta   // "Nenhum" | "Aviso" | "Atencao" | "Critico"
);

/// <summary>
/// Handler da query GetAlunosComFaltasQuery.
/// </summary>
public class GetAlunosComFaltasHandler : IRequestHandler<GetAlunosComFaltasQuery, IReadOnlyList<AlunoComFaltasDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly AppDbContext _dbContext;

    public GetAlunosComFaltasHandler(AppDbContext dbContext, ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<AlunoComFaltasDto>> Handle(
        GetAlunosComFaltasQuery request,
        CancellationToken cancellationToken)
    {
        var usuarioId = AutorizacaoUsuario.ExigirIdentidade(_currentUser);
        var administrador = _currentUser.Papel == "Administrador";
        var turmasPermitidas = _dbContext.UsuarioTurmas.Where(ut => ut.UsuarioId == usuarioId).Select(ut => ut.TurmaId);
        // Query base - traz apenas alunos ativos
        var query = _dbContext.Alunos.Where(a => administrador || turmasPermitidas.Contains(a.TurmaId))
            .Include(a => a.Turma)
            .Where(a => a.Ativo)
            .AsQueryable();

        // Filtro opcional por turma
        if (request.TurmaId.HasValue && request.TurmaId != Guid.Empty)
        {
            query = query.Where(a => a.TurmaId == request.TurmaId.Value);
        }

        // Projeção para DTO com ordenação por TotalFaltas DESC
        var alunos = await query
            .OrderByDescending(a => a.TotalFaltas)
            .ThenBy(a => a.Nome)
            .ToListAsync(cancellationToken);

        var resultados = alunos.Select(a => new AlunoComFaltasDto(
            a.Id,
            a.Nome,
            a.Matricula,
            a.TurmaId,
            a.Turma.Nome,
            a.FaltasConsecutivasAtuais,
            a.TotalFaltas,
            a.FaltasConsecutivasAtuais switch
            {
                0 => "Nenhum",
                1 => "Aviso",
                2 => "Atencao",
                _ => "Critico"
            }
        )).ToList();

        return resultados;
    }
}
