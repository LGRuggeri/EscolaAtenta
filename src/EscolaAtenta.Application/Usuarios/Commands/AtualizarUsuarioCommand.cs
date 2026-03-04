using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Usuarios.Commands;

public record AtualizarUsuarioCommand(Guid Id, string Nome, PapelUsuario Papel) : IRequest;

public class AtualizarUsuarioHandler : IRequestHandler<AtualizarUsuarioCommand>
{
    private readonly AppDbContext _context;

    public AtualizarUsuarioHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task Handle(AtualizarUsuarioCommand request, CancellationToken cancellationToken)
    {
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Usuário com ID {request.Id} não encontrado.");

        usuario.AtualizarPerfil(request.Nome, request.Papel);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
