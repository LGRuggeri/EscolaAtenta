using EscolaAtenta.Application.Escola.DTOs;
using EscolaAtenta.Application.Escola.Queries;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Escola.Handlers;

public class ObterConfiguracaoEscolaHandler : IRequestHandler<ObterConfiguracaoEscolaQuery, ConfiguracaoEscolaDto>
{
    private readonly AppDbContext _context;

    public ObterConfiguracaoEscolaHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ConfiguracaoEscolaDto> Handle(ObterConfiguracaoEscolaQuery request, CancellationToken cancellationToken)
    {
        var configuracao = await _context.ConfiguracoesEscola
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        // Garante uma configuração padrão mesmo se o seeder ainda não tiver rodado
        if (configuracao == null)
        {
            return new ConfiguracaoEscolaDto(Guid.Empty, TipoPeriodoLetivo.Trimestre);
        }

        return new ConfiguracaoEscolaDto(configuracao.Id, configuracao.TipoPeriodoLetivo);
    }
}
