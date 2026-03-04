using EscolaAtenta.Application.Usuarios.DTOs;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Usuarios.Queries;

public record GetUsuarioByIdQuery(Guid Id) : IRequest<UsuarioDetalheDto?>;

public class GetUsuarioByIdQueryHandler
    : IRequestHandler<GetUsuarioByIdQuery, UsuarioDetalheDto?>
{
    private readonly AppDbContext _context;

    public GetUsuarioByIdQueryHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<UsuarioDetalheDto?> Handle(
        GetUsuarioByIdQuery request,
        CancellationToken cancellationToken)
    {
        return await _context.Usuarios
            .AsNoTracking()
            .Where(u => u.Id == request.Id)
            .Select(u => new UsuarioDetalheDto
            {
                Id = u.Id,
                Nome = u.Nome,
                Email = u.Email,
                Papel = u.Papel.ToString(),
                Ativo = u.Ativo
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
