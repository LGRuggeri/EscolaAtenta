using EscolaAtenta.Application.Alunos.DTOs;
using EscolaAtenta.Application.Alunos.Queries;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EscolaAtenta.Application.Alunos.Handlers;

public class GetHistoricoPresencasAlunoQueryHandler : IRequestHandler<GetHistoricoPresencasAlunoQuery, IEnumerable<HistoricoPresencaDto>>
{
    private readonly AppDbContext _context;

    public GetHistoricoPresencasAlunoQueryHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<HistoricoPresencaDto>> Handle(GetHistoricoPresencasAlunoQuery request, CancellationToken cancellationToken)
    {
        var registros = await _context.RegistrosPresenca
            .Include(r => r.Chamada)
            .Where(r => r.AlunoId == request.AlunoId)
            .OrderByDescending(r => r.Chamada.DataHora)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return registros.Select(r => new HistoricoPresencaDto(
            DataDaChamada: r.Chamada.DataHora.UtcDateTime,
            Status: r.Status.ToString(),
            Justificativa: null // Future-proofing: the current entity doesn't have Justificativa mapped in DB
        ));
    }
}
