using EscolaAtenta.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace EscolaAtenta.Domain.Tests.Exceptions;

public class CredenciaisInvalidasExceptionTests
{
    [Fact]
    public void Constructor_WithoutParameters_ShouldSetDefaultMessage()
    {
        // Act
        var exception = new CredenciaisInvalidasException();

        // Assert
        exception.Message.Should().Be("Credenciais inválidas.");
    }

    [Fact]
    public void Constructor_WithMessage_ShouldSetCustomMessage()
    {
        // Arrange
        var customMessage = "E-mail não encontrado na base de dados.";

        // Act
        var exception = new CredenciaisInvalidasException(customMessage);

        // Assert
        exception.Message.Should().Be(customMessage);
    }
}
