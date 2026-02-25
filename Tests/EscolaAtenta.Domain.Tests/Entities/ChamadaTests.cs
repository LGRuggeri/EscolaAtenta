using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Events;
using EscolaAtenta.Domain.Exceptions;
using FluentAssertions;

namespace EscolaAtenta.Domain.Tests.Entities;

/// <summary>
/// Testes unitários para a entidade Chamada.
/// 
/// Estratégia: Testar cada invariante e método de negócio isoladamente.
/// Sem dependência de banco de dados ou infraestrutura — domínio puro.
/// </summary>
public class ChamadaTests
{
    // ── Fixtures ───────────────────────────────────────────────────────────────
    private static readonly Guid ChamadaId = Guid.NewGuid();
    private static readonly Guid TurmaId = Guid.NewGuid();
    private static readonly Guid ResponsavelId = Guid.NewGuid();
    private static readonly Guid AlunoId1 = Guid.NewGuid();
    private static readonly Guid AlunoId2 = Guid.NewGuid();

    private static Chamada CriarChamadaValida() =>
        new(ChamadaId, DateTimeOffset.UtcNow, TurmaId, ResponsavelId);

    // ── Testes de Criação ──────────────────────────────────────────────────────

    [Fact]
    public void Criar_ComDadosValidos_DeveCriarChamada()
    {
        // Arrange & Act
        var chamada = CriarChamadaValida();

        // Assert
        chamada.Id.Should().Be(ChamadaId);
        chamada.TurmaId.Should().Be(TurmaId);
        chamada.ResponsavelId.Should().Be(ResponsavelId);
        chamada.RegistrosPresenca.Should().BeEmpty();
    }

    [Fact]
    public void Criar_ComTurmaIdVazio_DeveLancarDomainException()
    {
        // Arrange & Act
        var acao = () => new Chamada(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.Empty, ResponsavelId);

        // Assert
        acao.Should().Throw<DomainException>()
            .WithMessage("*turma*");
    }

    [Fact]
    public void Criar_ComResponsavelIdVazio_DeveLancarDomainException()
    {
        // Arrange & Act
        var acao = () => new Chamada(Guid.NewGuid(), DateTimeOffset.UtcNow, TurmaId, Guid.Empty);

        // Assert
        acao.Should().Throw<DomainException>()
            .WithMessage("*responsável*");
    }

    // ── Testes de RegistrarPresenca ────────────────────────────────────────────

    [Fact]
    public void RegistrarPresenca_ComDadosValidos_DeveAdicionarRegistro()
    {
        // Arrange
        var chamada = CriarChamadaValida();

        // Act
        var registro = chamada.RegistrarPresenca(AlunoId1, StatusPresenca.Presente);

        // Assert
        chamada.RegistrosPresenca.Should().HaveCount(1);
        registro.AlunoId.Should().Be(AlunoId1);
        registro.Status.Should().Be(StatusPresenca.Presente);
        registro.ChamadaId.Should().Be(ChamadaId);
    }

    [Fact]
    public void RegistrarPresenca_ComFalta_DeveDispararPresencaRegistradaEvent()
    {
        // Arrange
        var chamada = CriarChamadaValida();

        // Act
        chamada.RegistrarPresenca(AlunoId1, StatusPresenca.Falta);

        // Assert — verifica que o Domain Event foi adicionado
        chamada.DomainEvents.Should().HaveCount(1);
        chamada.DomainEvents.First().Should().BeOfType<PresencaRegistradaEvent>();

        var evento = (PresencaRegistradaEvent)chamada.DomainEvents.First();
        evento.AlunoId.Should().Be(AlunoId1);
        evento.Status.Should().Be(StatusPresenca.Falta);
        evento.TurmaId.Should().Be(TurmaId);
    }

    [Fact]
    public void RegistrarPresenca_MesmoAlunoNaMesmaChamada_DeveLancarDomainException()
    {
        // Arrange
        var chamada = CriarChamadaValida();
        chamada.RegistrarPresenca(AlunoId1, StatusPresenca.Presente);

        // Act — tenta registrar o mesmo aluno novamente
        var acao = () => chamada.RegistrarPresenca(AlunoId1, StatusPresenca.Falta);

        // Assert — invariante de duplicidade deve ser violada
        acao.Should().Throw<DomainException>()
            .WithMessage($"*{AlunoId1}*");
    }

    [Fact]
    public void RegistrarPresenca_AlunosDiferentes_DevePermitirMultiplosRegistros()
    {
        // Arrange
        var chamada = CriarChamadaValida();

        // Act
        chamada.RegistrarPresenca(AlunoId1, StatusPresenca.Presente);
        chamada.RegistrarPresenca(AlunoId2, StatusPresenca.Falta);

        // Assert
        chamada.RegistrosPresenca.Should().HaveCount(2);
    }

    [Fact]
    public void RegistrarPresenca_ComAlunoIdVazio_DeveLancarDomainException()
    {
        // Arrange
        var chamada = CriarChamadaValida();

        // Act
        var acao = () => chamada.RegistrarPresenca(Guid.Empty, StatusPresenca.Presente);

        // Assert
        acao.Should().Throw<DomainException>();
    }

    [Fact]
    public void RegistrarPresenca_NaoDevePermitirMutacaoExternaDaColecao()
    {
        // Arrange
        var chamada = CriarChamadaValida();

        // Assert — IReadOnlyCollection não deve ser castável para List
        chamada.RegistrosPresenca.Should().BeAssignableTo<IReadOnlyCollection<RegistroPresenca>>();
        chamada.RegistrosPresenca.Should().NotBeAssignableTo<List<RegistroPresenca>>();
    }
}
