using EscolaAtenta.Domain.Interfaces;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Usuarios.Commands;

public record AlternarStatusUsuarioCommand(Guid Id) : IRequest;

public class AlternarStatusUsuarioHandler : IRequestHandler<AlternarStatusUsuarioCommand>
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AlternarStatusUsuarioHandler(AppDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(AlternarStatusUsuarioCommand request, CancellationToken cancellationToken)
    {
        var usuario = await _context.Usuarios
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Usuário com ID {request.Id} não encontrado.");

        if (usuario.Ativo)
            usuario.Desativar(_currentUser.UsuarioId);
        else
            usuario.Reativar();

        await _context.SaveChangesAsync(cancellationToken);
    }
}
