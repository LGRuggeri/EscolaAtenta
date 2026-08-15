using EscolaAtenta.Application.Chamadas.Queries;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Exceptions;
using EscolaAtenta.Domain.Interfaces;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Chamadas.Handlers;

public class ObterChamadaPorTurmaEDiaHandler : IRequestHandler<ObterChamadaPorTurmaEDiaQuery, ChamadaPorDiaDto?>
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ObterChamadaPorTurmaEDiaHandler(AppDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ChamadaPorDiaDto?> Handle(
        ObterChamadaPorTurmaEDiaQuery request,
        CancellationToken cancellationToken)
    {
        // IDOR: Administrador pode consultar qualquer turma; demais papéis precisam de vínculo
        if (_currentUser.Papel != nameof(PapelUsuario.Administrador)
            && Guid.TryParse(_currentUser.UsuarioId, out var ownerCheck)
            && !await _context.UsuarioTurmas.AnyAsync(
                ut => ut.TurmaId == request.TurmaId && ut.UsuarioId == ownerCheck, cancellationToken))
        {
            throw new DomainException("Você não tem permissão para consultar chamada desta turma.");
        }

        var chamada = (await _context.Chamadas
            .AsNoTracking()
            .Include(c => c.RegistrosPresenca)
            .ThenInclude(r => r.Aluno)
            .Where(c => c.TurmaId == request.TurmaId)
            .ToListAsync(cancellationToken))
            .Where(c => c.DataChamada == request.Data.Date)
            .OrderByDescending(c => c.DataCriacao)
            .ThenBy(c => c.Id)
            .FirstOrDefault();

        if (chamada is null)
            return null;

        var prazoEdicao = chamada.DataCriacao.AddDays(7);
        var podeEditar = DateTimeOffset.UtcNow <= prazoEdicao;

        return new ChamadaPorDiaDto
        {
            ChamadaId = chamada.Id,
            DataHora = chamada.DataHora,
            ResponsavelId = chamada.ResponsavelId,
            PodeEditar = podeEditar,
            Registros = chamada.RegistrosPresenca
                .Select(r => new RegistroPresencaPorDiaDto
                {
                    AlunoId = r.AlunoId,
                    NomeAluno = r.Aluno?.Nome ?? string.Empty,
                    Status = r.Status.ToString()
                })
                .ToList()
        };
    }
}
