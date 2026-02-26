using EscolaAtenta.Infrastructure.Services;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Exceptions;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Usuarios.Commands;

public record CriarUsuarioCommand(string Nome, string Email, PapelUsuario Papel) : IRequest<UsuarioCriadoResult>;

public record UsuarioCriadoResult(Guid Id, string Email, string SenhaInicial);

public class CriarUsuarioHandler : IRequestHandler<CriarUsuarioCommand, UsuarioCriadoResult>
{
    private readonly AppDbContext _context;

    public CriarUsuarioHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<UsuarioCriadoResult> Handle(CriarUsuarioCommand request, CancellationToken cancellationToken)
    {
        // Valida se o email já existe para garantir unicidade
        var emailExiste = await _context.Usuarios
            .AnyAsync(u => u.Email.ToLower() == request.Email.ToLower(), cancellationToken);

        if (emailExiste)
        {
            throw new InvalidOperationException($"O e-mail {request.Email} já está em uso.");
        }

        // Gera a senha forte para este novo usuário
        var senhaAleatoria = PasswordGenerator.Generate();
        var hash = BCrypt.Net.BCrypt.HashPassword(senhaAleatoria);

        var usuario = new Usuario(request.Email, hash, request.Papel);
        
        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync(cancellationToken);

        // A senha gerada só deve ser enviada/retornada UMA vez nesta resposta
        return new UsuarioCriadoResult(usuario.Id, usuario.Email, senhaAleatoria);
    }
}
