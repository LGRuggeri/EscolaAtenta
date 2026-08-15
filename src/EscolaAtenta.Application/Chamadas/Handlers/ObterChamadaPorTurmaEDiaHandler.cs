using EscolaAtenta.Application.Chamadas.Queries;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Chamadas.Handlers;

public class ObterChamadaPorTurmaEDiaHandler : IRequestHandler<ObterChamadaPorTurmaEDiaQuery, ChamadaPorDiaDto?>
{
    private readonly AppDbContext _context;

    public ObterChamadaPorTurmaEDiaHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ChamadaPorDiaDto?> Handle(
        ObterChamadaPorTurmaEDiaQuery request,
        CancellationToken cancellationToken)
    {
        var chamada = (await _context.Chamadas
            .AsNoTracking()
            .Include(c => c.RegistrosPresenca)
            .ThenInclude(r => r.Aluno)
            .Where(c => c.TurmaId == request.TurmaId)
            .ToListAsync(cancellationToken))
            .FirstOrDefault(c => c.DataHora.Date == request.Data.Date);

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
