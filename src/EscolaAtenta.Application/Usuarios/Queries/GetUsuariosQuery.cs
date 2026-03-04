using EscolaAtenta.Application.Common;
using EscolaAtenta.Application.Usuarios.DTOs;
using EscolaAtenta.Domain.Enums;
using MediatR;

namespace EscolaAtenta.Application.Usuarios.Queries;

/// <summary>
/// Query de Read Model para listagem paginada de usuários.
///
/// Filtros opcionais:
/// - SearchTerm: LIKE parcial em Nome ou Email (case-insensitive no PostgreSQL)
/// - Papel: filtra por PapelUsuario específico
///
/// Paginação: PageNumber (1-indexed), PageSize (default=20, hard cap=100 no Handler).
/// </summary>
public record GetUsuariosQuery(
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<PagedResult<UsuarioDto>>
{
    public string? SearchTerm { get; init; }
    public PapelUsuario? Papel { get; init; }
}
