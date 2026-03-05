using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace EscolaAtenta.Domain.Tests.Entities;

public class TurmaTests
{
    [Fact]
    public void Constructor_WithValidData_ShouldCreateTurmaAndPassValidations()
    {
        // Arrange
        var id = Guid.NewGuid();
        var nome = "Turma A";
        var turno = "Matutino";
        var ano = 2026;

        // Act
        var turma = new Turma(id, nome, turno, ano);

        // Assert
        turma.Id.Should().Be(id);
        turma.Nome.Should().Be(nome);
        turma.Turno.Should().Be(turno);
        turma.AnoLetivo.Should().Be(ano);
        turma.Ativo.Should().BeTrue();
    }

    [Fact]
    public void ValidarNome_WithExceedingLength_ShouldThrowDomainException()
    {
        // Arrange
        var nomeGigante = new string('A', 201); // 201 caracteres

        // Act
        Action act = () => new Turma(Guid.NewGuid(), nomeGigante, "Vespertino", 2026);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("O nome da turma não pode ter mais de 200 caracteres.");
    }

    [Fact]
    public void Desativar_WhenAlreadyInactive_ShouldThrowDomainException()
    {
        // Arrange
        var turma = new Turma(Guid.NewGuid(), "Turma B", "Noturno", 2026);
        turma.Desativar("admin_usuario"); // Desativa a primeira vez

        // Act
        Action act = () => turma.Desativar("outro_admin"); // Tenta desativar novamente

        // Assert
        act.Should().Throw<DomainException>().WithMessage("A turma já está inativa.");
    }
}
