namespace EscolaAtenta.Domain.Exceptions;

/// <summary>
/// Exceção base para violações de regras de negócio do domínio.
/// 
/// Decisão: Criar hierarquia de exceções de domínio separada das exceções
/// de infraestrutura. O GlobalExceptionHandler na API mapeia DomainException
/// para HTTP 422 (Unprocessable Entity) com Problem Details RFC 7807,
/// enquanto exceções de infraestrutura resultam em HTTP 500.
/// 
/// Não herda de ApplicationException para evitar confusão com a camada
/// Application — herda diretamente de Exception.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }

    public DomainException(string message, Exception innerException)
        : base(message, innerException) { }
}
