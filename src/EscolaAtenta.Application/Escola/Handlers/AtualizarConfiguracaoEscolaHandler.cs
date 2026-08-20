using EscolaAtenta.Application.Escola.Commands;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Exceptions;
using EscolaAtenta.Domain.Interfaces;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Escola.Handlers;

public class AtualizarConfiguracaoEscolaHandler : IRequestHandler<AtualizarConfiguracaoEscolaCommand>
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AtualizarConfiguracaoEscolaHandler(AppDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(AtualizarConfiguracaoEscolaCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Papel != nameof(PapelUsuario.Administrador))
            throw new UnauthorizedAccessException("Apenas administradores podem alterar a configuração da escola.");

        var configuracao = await _context.ConfiguracoesEscola.FirstOrDefaultAsync(cancellationToken);

        if (configuracao == null)
        {
            throw new DomainException("Configuração da escola não encontrada. Execute o seed do banco de dados.");
        }

        configuracao.AlterarTipoPeriodoLetivo(request.TipoPeriodoLetivo);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
