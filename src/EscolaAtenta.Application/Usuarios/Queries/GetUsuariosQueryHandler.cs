using EscolaAtenta.Application.Common;
using EscolaAtenta.Application.Usuarios.DTOs;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Usuarios.Queries;

/// <summary>
/// Handler de Read Model para listagem paginada de usuários.
///
/// Decisões de design:
/// 1. AsNoTracking() — somente leitura, sem overhead de change tracking.
/// 2. EF.Functions.Like para SearchTerm — traduzido para LIKE no PostgreSQL,
///    filtrando tanto em Nome quanto em Email na mesma condição OR.
/// 3. Não filtra por Ativo — traz todos os usuários para que o front-end
///    possa exibir quem foi desativado (campo Ativo no DTO).
/// 4. COUNT separado antes do Skip/Take — necessário para HasNextPage.
/// 5. .Select() projeta direto para UsuarioDto — sem materializar entidade completa.
/// 6. Ordenação por Nome ascendente para listagem alfabética.
/// </summary>
public class GetUsuariosQueryHandler
    : IRequestHandler<GetUsuariosQuery, PagedResult<UsuarioDto>>
{
    private readonly AppDbContext _context;

    public GetUsuariosQueryHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<UsuarioDto>> Handle(
        GetUsuariosQuery request,
        CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = _context.Usuarios.AsNoTracking().AsQueryable();

        // Filtro por SearchTerm — busca parcial em Nome ou Email
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = $"%{request.SearchTerm}%";
            query = query.Where(u =>
                EF.Functions.Like(u.Nome, term) ||
                EF.Functions.Like(u.Email, term));
        }

        // Filtro por Papel
        if (request.Papel.HasValue)
        {
            query = query.Where(u => u.Papel == request.Papel.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        if (totalCount == 0)
        {
            return PagedResult<UsuarioDto>.Empty(pageNumber, pageSize);
        }

        var itens = await query
            .OrderBy(u => u.Nome)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UsuarioDto
            {
                Id = u.Id,
                Nome = u.Nome,
                Email = u.Email,
                Papel = u.Papel.ToString(),
                Ativo = u.Ativo
            })
            .ToListAsync(cancellationToken);

        return PagedResult<UsuarioDto>.Create(itens, totalCount, pageNumber, pageSize);
    }
}
