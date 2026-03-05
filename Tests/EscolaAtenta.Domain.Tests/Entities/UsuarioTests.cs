using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace EscolaAtenta.Domain.Tests.Entities;

public class UsuarioTests
{
    [Fact]
    public void Constructor_WithValidData_ShouldCreateUsuario()
    {
        // Arrange & Act
        var emailOriginal = "  TeStE@Exemplo.com  ";
        var usuario = new Usuario("Maria", emailOriginal, "hash_senha_secreta", PapelUsuario.Monitor);

        // Assert
        usuario.Nome.Should().Be("Maria");
        usuario.Email.Should().Be("teste@exemplo.com"); // Verifica normalizaçao
        usuario.HashSenha.Should().Be("hash_senha_secreta");
        usuario.Papel.Should().Be(PapelUsuario.Monitor);
        usuario.Ativo.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WithEmptyEmail_ShouldThrowArgumentException(string? invalidEmail)
    {
        // Act
        Action act = () => new Usuario("Joao", invalidEmail, "hash", PapelUsuario.Monitor);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("Email e obrigatorio. (Parameter 'email')");
    }

    [Fact]
    public void Constructor_WithInvalidEmailFormat_ShouldThrowArgumentException()
    {
        // Act
        Action act = () => new Usuario("Joao", "email_sem_arroba.com", "hash", PapelUsuario.Monitor);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("Email invalido. (Parameter 'email')");
    }

    [Fact]
    public void AlterarSenha_WithValidPassword_ShouldUpdateHashSenha()
    {
        // Arrange
        var usuario = new Usuario("Admin", "admin@escola.com", "hash_velho", PapelUsuario.Administrador);
        var novoHash = "hash_novo_seguro";

        // Act
        usuario.AlterarSenha(novoHash);

        // Assert
        usuario.HashSenha.Should().Be(novoHash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AlterarSenha_WithEmptyPassword_ShouldThrowArgumentException(string? invalidPassword)
    {
        // Arrange
        var usuario = new Usuario("Admin", "admin@escola.com", "hash", PapelUsuario.Administrador);

        // Act
        Action act = () => usuario.AlterarSenha(invalidPassword);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("Nova senha e obrigatoria. (Parameter 'novoHashSenha')");
    }

    [Fact]
    public void Desativar_WhenActive_ShouldSetAtivoFalseAndPopulateAuditFields()
    {
        // Arrange
        var usuario = new Usuario("Joao", "joao@escola.com", "hash", PapelUsuario.Monitor);
        var resposavelExclusao = "admin_master";

        // Act
        usuario.Desativar(resposavelExclusao);

        // Assert
        usuario.Ativo.Should().BeFalse();
        usuario.DataExclusao.Should().NotBeNull();
        usuario.UsuarioExclusao.Should().Be(resposavelExclusao);
    }

    [Fact]
    public void Reativar_WhenInactive_ShouldRestoreToActiveAndClearAuditFields()
    {
        // Arrange
        var usuario = new Usuario("Joao", "joao@escola.com", "hash", PapelUsuario.Monitor);
        usuario.Desativar("admin");

        // Act
        usuario.Reativar();

        // Assert
        usuario.Ativo.Should().BeTrue();
        usuario.DataExclusao.Should().BeNull();
        usuario.UsuarioExclusao.Should().BeNull();
    }
}
