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

    // ── Testes de VerificarLimiteFaltas ─────────────────────────────────────
    // OBS: Com o agregado auto-protegido, RegistrarFalta() chama internamente
    // VerificarLimiteFaltas() a cada mutação. Os testes abaixo verificam o
    // comportamento final correto sem necessitar chamada explícita.

    [Fact]
    public void RegistrarFalta_QuandoAtinge3FaltasConsecutivas_DeveConterEventoVermelho()
    {
        // Arrange
        var aluno = CriarAlunoValido();

        // Act — 3 faltas consecutivas disparam 3 eventos (1-Aviso, 2-Intermediário, 3-Vermelho)
        aluno.RegistrarPresenca(Domain.Enums.StatusPresenca.Falta, DateTime.UtcNow);
        aluno.RegistrarPresenca(Domain.Enums.StatusPresenca.Falta, DateTime.UtcNow);
        aluno.RegistrarPresenca(Domain.Enums.StatusPresenca.Falta, DateTime.UtcNow);

        // Assert — deve existir exatamente 3 eventos (um por cada threshold atingido: 1,2,3)
        aluno.DomainEvents.Should().HaveCount(3);
        aluno.DomainEvents.Should().AllBeOfType<LimiteFaltasAtingidoEvent>();

        var ultimoEvento = aluno.DomainEvents
            .OfType<LimiteFaltasAtingidoEvent>()
            .Last();

        ultimoEvento.AlunoId.Should().Be(AlunoId);
        ultimoEvento.TotalFaltas.Should().Be(3);
        ultimoEvento.Nivel.Should().Be(Domain.Enums.NivelAlertaFalta.Vermelho);
        ultimoEvento.NomeAluno.Should().Be("João da Silva");
    }

    [Fact]
    public void RegistrarFalta_ComApenasUmaFalta_DeveConterEventoAviso()
    {
        // Arrange
        var aluno = CriarAlunoValido();

        // Act — 1 falta consecutiva é threshold 1 (Aviso)
        aluno.RegistrarPresenca(Domain.Enums.StatusPresenca.Falta, DateTime.UtcNow);

        // Assert
        aluno.DomainEvents.Should().HaveCount(1);
        var evento = (LimiteFaltasAtingidoEvent)aluno.DomainEvents.First();
        evento.Nivel.Should().Be(Domain.Enums.NivelAlertaFalta.Aviso);
    }

    [Fact]
    public void RegistrarPresenca_AposSecuencia_DeveZerarEventosDeFaltas()
    {
        // Arrange: 3 faltas geram 3 eventos
        var aluno = CriarAlunoValido();
        aluno.RegistrarPresenca(Domain.Enums.StatusPresenca.Falta, DateTime.UtcNow);
        aluno.RegistrarPresenca(Domain.Enums.StatusPresenca.Falta, DateTime.UtcNow);
        aluno.RegistrarPresenca(Domain.Enums.StatusPresenca.Falta, DateTime.UtcNow);
        aluno.ClearDomainEvents();

        // Act: Presença zera as faltas consecutivas
        aluno.RegistrarPresenca(Domain.Enums.StatusPresenca.Presente, DateTime.UtcNow);

        // Assert: Nenhum novo evento (Presente não dispara alerta)
        aluno.DomainEvents.Should().BeEmpty();
        aluno.FaltasConsecutivasAtuais.Should().Be(0);
    }

    [Fact]
    public void RegistrarFalta_QuandoNaoAtingeNenhumThreshold_NaoDeveDispararEvento()
    {
        // Arrange: Nenhum valor de 0 faltas inicia evento
        var aluno = CriarAlunoValido();

        // Act — Sem faltas
        // Assert — Nenhum evento gerado
        aluno.DomainEvents.Should().BeEmpty();
    }

    // ── Testes de VerificarLimiteAtrasos ─────────────────────────────────────
    // Thresholds explícitos: 3 atrasos = Aviso, 6 atrasos = Intermediário.
    // Atrasos entre os thresholds NEM acima de 6 (no trimestre) não disparam novo evento
    // -- a lógica de escalada está nos Handlers de Application, não no Domínio.

    [Fact]
    public void RegistrarAtraso_ComMenosDe3Atrasos_NaoDeveDispararEvento()
    {
        // Arrange
        var aluno = CriarAlunoValido();

        // Act: 2 atrasos — abaixo do primeiro threshold
        aluno.RegistrarAtraso(DateTime.UtcNow);
        aluno.RegistrarAtraso(DateTime.UtcNow);

        // Assert
        aluno.DomainEvents.Should().BeEmpty();
        aluno.AtrasosNoTrimestre.Should().Be(2);
    }

    [Fact]
    public void RegistrarAtraso_ComExatamente3Atrasos_DeveDispararLimiteAtrasosAtingidoEvent_NivelAviso()
    {
        // Arrange
        var aluno = CriarAlunoValido();

        // Act: exatamente 3 atrasos — primeiro threshold
        aluno.RegistrarAtraso(DateTime.UtcNow);
        aluno.RegistrarAtraso(DateTime.UtcNow);
        aluno.RegistrarAtraso(DateTime.UtcNow);

        // Assert
        aluno.DomainEvents.Should().HaveCount(1);
        var evento = aluno.DomainEvents.OfType<LimiteAtrasosAtingidoEvent>().Single();
        evento.AlunoId.Should().Be(AlunoId);
        evento.TotalAtrasos.Should().Be(3);
        evento.Nivel.Should().Be(Domain.Enums.NivelAlertaFalta.Aviso);
        evento.NomeAluno.Should().Be("João da Silva");
    }

    [Fact]
    public void RegistrarAtraso_ComExatamente6Atrasos_DeveDispararEvento_NivelIntermediario()
    {
        // Arrange
        var aluno = CriarAlunoValido();

        // Act: chegar ao 6º atraso
        for (int i = 0; i < 5; i++)
            aluno.RegistrarAtraso(DateTime.UtcNow);
        aluno.ClearDomainEvents(); // Limpa o evento gerado no 3º atraso

        aluno.RegistrarAtraso(DateTime.UtcNow); // 6º atraso

        // Assert
        aluno.DomainEvents.Should().HaveCount(1);
        var evento = aluno.DomainEvents.OfType<LimiteAtrasosAtingidoEvent>().Single();
        evento.TotalAtrasos.Should().Be(6);
        evento.Nivel.Should().Be(Domain.Enums.NivelAlertaFalta.Intermediario);
    }

    [Fact]
    public void RegistrarAtraso_Entre3E6_NaoDeveDispararNovoEvento()
    {
        // Arrange: já em 3 atrasos
        var aluno = CriarAlunoValido();
        for (int i = 0; i < 3; i++)
            aluno.RegistrarAtraso(DateTime.UtcNow);
        aluno.ClearDomainEvents();

        // Act: 4º e 5º atraso não são thresholds
        aluno.RegistrarAtraso(DateTime.UtcNow);
        aluno.RegistrarAtraso(DateTime.UtcNow);

        // Assert: silencioso entre os thresholds
        aluno.DomainEvents.Should().BeEmpty();
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

    // ── Testes de VerificarEReiniciarCicloTrimestral ─────────────────────────

    [Fact]
    public void VerificarEReiniciarCicloTrimestral_AntesDe90Dias_NaoDeveResetarContadores()
    {
        // Arrange: aluno com faltas e atrasos acumulados
        var aluno = CriarAlunoValido();
        var agora = DateTime.UtcNow;
        aluno.RegistrarFalta(agora);
        aluno.RegistrarFalta(agora);
        aluno.RegistrarAtraso(agora);
        aluno.ClearDomainEvents();

        // Act: avança 89 dias (dentro do ciclo)
        aluno.VerificarEReiniciarCicloTrimestral(agora.AddDays(89));

        // Assert: contadores devem permanecer inalterados
        aluno.FaltasConsecutivasAtuais.Should().Be(2);
        aluno.TotalFaltas.Should().Be(2);
        aluno.AtrasosNoTrimestre.Should().Be(1);
    }

    [Fact]
    public void VerificarEReiniciarCicloTrimestral_Apos90Dias_DeveResetarContadores()
    {
        // Arrange: aluno com faltas e atrasos acumulados
        var aluno = CriarAlunoValido();
        var agora = DateTime.UtcNow;
        aluno.RegistrarFalta(agora);
        aluno.RegistrarFalta(agora);
        aluno.RegistrarAtraso(agora);
        aluno.ClearDomainEvents();

        // Act: avança 90 dias (novo ciclo)
        aluno.VerificarEReiniciarCicloTrimestral(agora.AddDays(90));

        // Assert: contadores de trimestre devem zerar
        aluno.FaltasConsecutivasAtuais.Should().Be(0);
        aluno.FaltasNoTrimestre.Should().Be(0);
        aluno.AtrasosNoTrimestre.Should().Be(0);
        // TotalFaltas é histórico — NÃO zera
        aluno.TotalFaltas.Should().Be(2);
    }
}
