// Exceção para credenciais inválidas - sem detalhes sobre qual campo está errado
// Isso previne enumeração de usuários (AppSec)
namespace EscolaAtenta.Domain.Exceptions;

public class CredenciaisInvalidasException : Exception
{
    public CredenciaisInvalidasException() 
        : base("Credenciais inválidas.") { }

    public CredenciaisInvalidasException(string message) 
        : base(message) { }
}
