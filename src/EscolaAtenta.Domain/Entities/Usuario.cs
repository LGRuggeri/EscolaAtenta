// Entidade Usuario - representa o acesso ao sistema
// Segue o padrao Rich Domain Model: a entidade protege suas invariantes
// 
// SEGURANCA (AppSec):
// - A senha NAO e armazenada nesta entidade, apenas o hash bcrypt
// - O hash e gerado e validado pelo IAuthService (nao na entidade)
// - A entidade implementa ISoftDeletable para exclusao logica

using EscolaAtenta.Domain.Common;
using EscolaAtenta.Domain.Enums;

namespace EscolaAtenta.Domain.Entities;

/// <summary>
/// Usuario do sistema com acesso autenticado.
/// </summary>
public class Usuario : EntityBase, ISoftDeletable
{
    // Construtor com parametros obrigatorios - força o uso de factory methods ou repositorio
    public Usuario(string email, string hashSenha, PapelUsuario papel)
    {
        // Validacao de email via construtor - garantindo invariante
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email e obrigatorio.", nameof(email));
        
        if (!email.Contains('@'))
            throw new ArgumentException("Email invalido.", nameof(email));
        
        // Validacao do papel
        if (!Enum.IsDefined(typeof(PapelUsuario), papel))
            throw new ArgumentException("Papel invalido.", nameof(papel));

        Email = email.ToLowerInvariant().Trim(); // Normalizacao: lowercase + trim
        HashSenha = hashSenha ?? throw new ArgumentNullException(nameof(hashSenha));
        Papel = papel;
        Ativo = true; // Por padrao, usuario ativo
    }

    // Construtor protegido para EF Core
    protected Usuario() { }

    // ── Propriedades de Identity ─────────────────────────────────────────────────
    public string Email { get; private set; } = string.Empty;
    
    // hash bcrypt de 60 caracteres - NUNCA armazenar senha em texto
    public string HashSenha { get; private set; } = string.Empty;
    
    public PapelUsuario Papel { get; private set; }

    // ── Soft Delete (ISoftDeletable) ────────────────────────────────────────────
    public bool Ativo { get; private set; }
    public DateTimeOffset? DataExclusao { get; private set; }
    public string? UsuarioExclusao { get; private set; }

    // ── Metodos de Negocio ─────────────────────────────────────────────────────

    /// <summary>
    /// Altera a senha do usuario.
    /// </summary>
    public void AlterarSenha(string novoHashSenha)
    {
        if (string.IsNullOrWhiteSpace(novoHashSenha))
            throw new ArgumentException("Nova senha e obrigatoria.", nameof(novoHashSenha));
        
        HashSenha = novoHashSenha;
    }

    /// <summary>
    /// Desativa o usuario (soft delete) - impede login sem remover dados.
    /// </summary>
    public void Desativar(string usuarioResponsavel)
    {
        if (!Ativo) return; // Ja desativado
        
        Ativo = false;
        DataExclusao = DateTimeOffset.UtcNow;
        UsuarioExclusao = usuarioResponsavel ?? "sistema";
    }

    /// <summary>
    /// Reativa um usuario desativado.
    /// </summary>
    public void Reativar()
    {
        Ativo = true;
        DataExclusao = null;
        UsuarioExclusao = null;
    }

    /// <summary>
    /// Verifica se o usuario pode acessar o sistema.
    /// </summary>
    public bool PodeAcessar() => Ativo;
}
