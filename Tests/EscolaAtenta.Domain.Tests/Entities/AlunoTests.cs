using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Events;
using EscolaAtenta.Domain.Exceptions;
using FluentAssertions;

namespace EscolaAtenta.Domain.Tests.Entities;

public class AlunoTests
{
    private static readonly Guid AlunoId = Guid.NewGuid();
    private static readonly Guid TurmaId = Guid.NewGuid();

    private static Aluno CriarAlunoValido() =>
        new(AlunoId, "João da Silva", "2024001", TurmaId);

    // ── Testes de Criação ──────────────────────────────────────────────────────

    [Fact]
    public void Criar_ComDadosValidos_DeveCriarAluno()
    {
        // Arrange & Act
        var aluno = CriarAlunoValido();

        // Assert
        aluno.Id.Should().Be(AlunoId);
        aluno.Nome.Should().Be("João da Silva");
        aluno.Matricula.Should().Be("2024001");
        aluno.TurmaId.Should().Be(TurmaId);
        aluno.Ativo.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Criar_ComNomeInvalido_DeveLancarDomainException(string? nome)
    {
        // Act
        var acao = () => new Aluno(Guid.NewGuid(), nome!, "2024001", TurmaId);

        // Assert
        acao.Should().Throw<DomainException>()
            .WithMessage("*nome*");
    }

    [Fact]
    public void Criar_ComNomeMuitoLongo_DeveLancarDomainException()
    {
        // Arrange
        var nomeLongo = new string('A', 201);

        // Act
        var acao = () => new Aluno(Guid.NewGuid(), nomeLongo, "2024001", TurmaId);

        // Assert
        acao.Should().Throw<DomainException>()
            .WithMessage("*200*");
    }

    [Fact]
    public void Criar_ComTurmaIdVazio_DeveLancarDomainException()
    {
        // Act
        var acao = () => new Aluno(Guid.NewGuid(), "João", "2024001", Guid.Empty);

        // Assert
        acao.Should().Throw<DomainException>()
            .WithMessage("*turma*");
    }

    // ── Testes de VerificarLimiteFaltas ───────────────────────────────────────

    [Fact]
    public void VerificarLimiteFaltas_QuandoAtingeLimite_DeveDispararDomainEvent()
    {
        // Arrange
        var aluno = CriarAlunoValido();
        const int limite = 5;

        // Act — total de faltas igual ao limite
        aluno.VerificarLimiteFaltas(totalFaltas: 5, limiteConfigurado: limite);

        // Assert
        aluno.DomainEvents.Should().HaveCount(1);
        aluno.DomainEvents.First().Should().BeOfType<LimiteFaltasAtingidoEvent>();

        var evento = (LimiteFaltasAtingidoEvent)aluno.DomainEvents.First();
        evento.AlunoId.Should().Be(AlunoId);
        evento.TotalFaltas.Should().Be(5);
        evento.LimiteConfigurado.Should().Be(5);
        evento.NomeAluno.Should().Be("João da Silva");
    }

    [Fact]
    public void VerificarLimiteFaltas_QuandoAbaixoDoLimite_NaoDeveDispararDomainEvent()
    {
        // Arrange
        var aluno = CriarAlunoValido();

        // Act — total de faltas abaixo do limite
        aluno.VerificarLimiteFaltas(totalFaltas: 3, limiteConfigurado: 5);

        // Assert
        aluno.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void VerificarLimiteFaltas_QuandoAcimaDoLimite_NaoDeveDispararDomainEvent()
    {
        // Arrange — aluno já passou do limite (alerta já foi gerado anteriormente)
        var aluno = CriarAlunoValido();

        // Act — total de faltas acima do limite (não dispara novamente)
        aluno.VerificarLimiteFaltas(totalFaltas: 7, limiteConfigurado: 5);

        // Assert — evento só é disparado quando atinge exatamente o limite
        aluno.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void VerificarLimiteFaltas_ComTotalNegativo_DeveLancarDomainException()
    {
        // Arrange
        var aluno = CriarAlunoValido();

        // Act
        var acao = () => aluno.VerificarLimiteFaltas(totalFaltas: -1, limiteConfigurado: 5);

        // Assert
        acao.Should().Throw<DomainException>()
            .WithMessage("*negativo*");
    }

    [Fact]
    public void VerificarLimiteFaltas_ComLimiteZero_DeveLancarDomainException()
    {
        // Arrange
        var aluno = CriarAlunoValido();

        // Act
        var acao = () => aluno.VerificarLimiteFaltas(totalFaltas: 3, limiteConfigurado: 0);

        // Assert
        acao.Should().Throw<DomainException>()
            .WithMessage("*maior que zero*");
    }

    // ── Testes de Soft Delete ──────────────────────────────────────────────────

    [Fact]
    public void Desativar_AlunoAtivo_DeveDesativarComAuditoria()
    {
        // Arrange
        var aluno = CriarAlunoValido();

        // Act
        aluno.Desativar("admin@escola.com");

        // Assert
        aluno.Ativo.Should().BeFalse();
        aluno.DataExclusao.Should().NotBeNull();
        aluno.UsuarioExclusao.Should().Be("admin@escola.com");
    }

    [Fact]
    public void Desativar_AlunoJaInativo_DeveLancarDomainException()
    {
        // Arrange
        var aluno = CriarAlunoValido();
        aluno.Desativar("admin@escola.com");

        // Act — tenta desativar novamente
        var acao = () => aluno.Desativar("outro@escola.com");

        // Assert
        acao.Should().Throw<DomainException>()
            .WithMessage("*inativo*");
    }

    // ── Testes de Atualizar ────────────────────────────────────────────────────

    [Fact]
    public void Atualizar_ComDadosValidos_DeveAtualizarNomeEMatricula()
    {
        // Arrange
        var aluno = CriarAlunoValido();

        // Act
        aluno.Atualizar("Maria Souza", "2024002");

        // Assert
        aluno.Nome.Should().Be("Maria Souza");
        aluno.Matricula.Should().Be("2024002");
    }

    // ── Testes de TransferirTurma ──────────────────────────────────────────────

    [Fact]
    public void TransferirTurma_ParaTurmaValida_DeveAtualizarTurmaId()
    {
        // Arrange
        var aluno = CriarAlunoValido();
        var novaTurmaId = Guid.NewGuid();

        // Act
        aluno.TransferirTurma(novaTurmaId);

        // Assert
        aluno.TurmaId.Should().Be(novaTurmaId);
    }

    [Fact]
    public void TransferirTurma_ParaMesmaTurma_DeveLancarDomainException()
    {
        // Arrange
        var aluno = CriarAlunoValido();

        // Act
        var acao = () => aluno.TransferirTurma(TurmaId);

        // Assert
        acao.Should().Throw<DomainException>()
            .WithMessage("*já pertence*");
    }
}
