namespace EscolaAtenta.Domain.Interfaces;

/// <summary>
/// Contrato para obtenção do usuário autenticado no contexto da requisição atual.
/// 
/// Decisão: A interface fica no Domain para que o AppDbContext (Infrastructure)
/// possa depender dela sem criar dependência circular. A implementação concreta
/// (CurrentUserService) fica na Infrastructure e acessa o HttpContext.
/// 
/// Em testes unitários, pode ser substituída por um mock que retorna um usuário fixo.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Identificador do usuário autenticado.
    /// Retorna "sistema" se não houver usuário autenticado (ex: jobs em background).
    /// </summary>
    string UsuarioId { get; }

    /// <summary>
    /// Papel do usuário autenticado (ex: "Monitor", "Supervisao", "Administrador").
    /// Retorna string.Empty se não houver usuário autenticado.
    /// </summary>
    string Papel { get; }

    /// <summary>
    /// Indica se há um usuário autenticado na requisição atual.
    /// </summary>
    bool EstaAutenticado { get; }
}
