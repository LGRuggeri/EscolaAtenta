using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
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

    // ── Testes de RecalcularEstatisticas ───────────────────────────────────────

    [Fact]
    public void RecalcularEstatisticas_AposCorrecaoDeFaltaParaPresente_DeveZerarConsecutivas()
    {
        // Arrange
        var aluno = CriarAlunoValido();
        var data = DateTime.UtcNow;

        // Simula histórico: uma falta seguida de uma presença corrigida
        var chamada1 = new Chamada(Guid.NewGuid(), new DateTimeOffset(data.AddDays(-2)), TurmaId, Guid.NewGuid());
        var registro1 = chamada1.RegistrarPresenca(aluno.Id, StatusPresenca.Falta);
        aluno.RegistrarPresenca(StatusPresenca.Falta, data.AddDays(-2));
        aluno.ClearDomainEvents();

        var chamada2 = new Chamada(Guid.NewGuid(), new DateTimeOffset(data.AddDays(-1)), TurmaId, Guid.NewGuid());
        var registro2 = chamada2.RegistrarPresenca(aluno.Id, StatusPresenca.Presente);
        aluno.RegistrarPresenca(StatusPresenca.Presente, data.AddDays(-1));
        aluno.ClearDomainEvents();

        aluno.FaltasConsecutivasAtuais.Should().Be(0);
        aluno.TotalFaltas.Should().Be(1);

        // Simula correção do primeiro registro de Falta para Presente
        registro1.AlterarStatus(StatusPresenca.Presente);

        // Act
        aluno.RecalcularEstatisticas(new[] { registro1, registro2 });

        // Assert
        aluno.FaltasConsecutivasAtuais.Should().Be(0);
        aluno.TotalFaltas.Should().Be(0, "com ambos os dias presentes, não há faltas");
    }

    [Fact]
    public void RecalcularEstatisticas_ComEventosPendentes_DeveLimparEventosAntesDeRecomputar()
    {
        // Arrange
        var aluno = CriarAlunoValido();
        var data = DateTime.UtcNow;

        // Gera eventos de faltas consecutivas (simulando um batch anterior que já os enfileirou)
        aluno.RegistrarPresenca(StatusPresenca.Falta, data.AddDays(-2));
        aluno.RegistrarPresenca(StatusPresenca.Falta, data.AddDays(-1));
        aluno.RegistrarPresenca(StatusPresenca.Falta, data);
        aluno.DomainEvents.Should().NotBeEmpty("faltas consecutivas devem ter gerado eventos");

        // Agora recalcula considerando apenas presenças: os eventos antigos devem ser limpos
        // e nenhum novo evento deve ser gerado.
        var chamada = new Chamada(Guid.NewGuid(), new DateTimeOffset(data), TurmaId, Guid.NewGuid());
        var registro = chamada.RegistrarPresenca(aluno.Id, StatusPresenca.Presente);

        // Act
        aluno.RecalcularEstatisticas(new[] { registro });

        // Assert: eventos prévios foram removidos e, como houve apenas presença, não há novos eventos
        aluno.DomainEvents.Should().BeEmpty();
        aluno.FaltasConsecutivasAtuais.Should().Be(0);
    }

    [Fact]
    public void RecalcularEstatisticasEReconciliar_ComVariasFaltasConsecutivas_DeveEmitirApenasEventoFinal()
    {
        // Arrange
        var aluno = CriarAlunoValido();
        var data = DateTime.UtcNow;

        var registros = new List<RegistroPresenca>();
        for (int i = 0; i < 5; i++)
        {
            var chamada = new Chamada(Guid.NewGuid(), new DateTimeOffset(data.AddDays(i)), TurmaId, Guid.NewGuid());
            registros.Add(chamada.RegistrarPresenca(aluno.Id, StatusPresenca.Falta));
        }

        // Act
        aluno.RecalcularEstatisticas(registros);
        aluno.ReconciliarAlertasPendentes();

        // Assert: apenas um evento final de threshold (nível Preto para 5 faltas),
        // sem eventos intermediários de Aviso (1), Intermediário (2) ou Vermelho (3).
        aluno.DomainEvents.OfType<LimiteFaltasAtingidoEvent>().Should().ContainSingle();
        var evento = aluno.DomainEvents.OfType<LimiteFaltasAtingidoEvent>().Single();
        evento.Nivel.Should().Be(NivelAlertaFalta.Preto);
        evento.TotalFaltas.Should().Be(5);
    }

    [Fact]
    public void RecalcularEstatisticasEReconciliar_ComVariosAtrasos_DeveEmitirApenasEventoFinal()
    {
        // Arrange
        var aluno = CriarAlunoValido();
        var data = DateTime.UtcNow;

        var registros = new List<RegistroPresenca>();
        for (int i = 0; i < 6; i++)
        {
            var chamada = new Chamada(Guid.NewGuid(), new DateTimeOffset(data.AddDays(i)), TurmaId, Guid.NewGuid());
            registros.Add(chamada.RegistrarPresenca(aluno.Id, StatusPresenca.Atraso));
        }

        // Act
        aluno.RecalcularEstatisticas(registros);
        aluno.ReconciliarAlertasPendentes();

        // Assert: apenas um evento final de threshold (nível Intermediário para 6 atrasos),
        // sem evento intermediário de Aviso (3). Eventos de normalização de outro
        // tipo (faltas zeradas) podem coexistir.
        aluno.DomainEvents.OfType<LimiteAtrasosAtingidoEvent>().Should().ContainSingle();
        var evento = aluno.DomainEvents.OfType<LimiteAtrasosAtingidoEvent>().Single();
        evento.Nivel.Should().Be(NivelAlertaFalta.Intermediario);
        evento.TotalAtrasos.Should().Be(6);
    }

    [Fact]
    public void RecalcularEstatisticas_ComCicloTrimestralAtivo_DevePreservarFronteiraERestringirReplay()
    {
        // Arrange
        var aluno = CriarAlunoValido();
        var data = DateTime.UtcNow;

        // Simula que o aluno já tem um ciclo iniciado há 30 dias (força via reflection)
        var inicioCiclo = data.AddDays(-30).Date;
        var prop = typeof(Aluno).GetProperty(nameof(Aluno.DataInicioTrimestre));
        prop!.SetValue(aluno, inicioCiclo);

        // Histórico: uma falta antiga (antes do ciclo) e uma falta dentro do ciclo
        var chamadaAntiga = new Chamada(Guid.NewGuid(), new DateTimeOffset(data.AddDays(-100)), TurmaId, Guid.NewGuid());
        var registroAntigo = chamadaAntiga.RegistrarPresenca(aluno.Id, StatusPresenca.Falta);

        var chamadaAtual = new Chamada(Guid.NewGuid(), new DateTimeOffset(data.AddDays(-10)), TurmaId, Guid.NewGuid());
        var registroAtual = chamadaAtual.RegistrarPresenca(aluno.Id, StatusPresenca.Falta);

        // Act
        aluno.RecalcularEstatisticas(new[] { registroAntigo, registroAtual });

        // Assert
        aluno.DataInicioTrimestre.Should().Be(inicioCiclo, "o ciclo ativo não deve ser movido para trás");
        aluno.TotalFaltas.Should().Be(2, "total histórico conta todas as faltas");
        aluno.FaltasNoTrimestre.Should().Be(1, "apenas a falta dentro do ciclo conta");
        aluno.FaltasConsecutivasAtuais.Should().Be(1, "apenas registros dentro do ciclo contam para consecutivas");
    }

    [Fact]
    public void RecalcularEstatisticas_SemCicloAtivo_DeveUsarPrimeiraPresencaComoInicio()
    {
        // Arrange
        var aluno = CriarAlunoValido();
        var data = DateTime.UtcNow;

        var chamada = new Chamada(Guid.NewGuid(), new DateTimeOffset(data.AddDays(-5)), TurmaId, Guid.NewGuid());
        var registro = chamada.RegistrarPresenca(aluno.Id, StatusPresenca.Falta);

        // Act
        aluno.RecalcularEstatisticas(new[] { registro });

        // Assert
        aluno.DataInicioTrimestre.Date.Should().Be(registro.Chamada.DataHora.UtcDateTime.Date);
        aluno.FaltasNoTrimestre.Should().Be(1);
        aluno.FaltasConsecutivasAtuais.Should().Be(1);
    }

    [Fact]
    public void ReconciliarAlertasPendentes_AposCorrecaoDeFaltas_DeveEmitirEventoDeNormalizacaoDeFaltas()
    {
        // Arrange
        var aluno = CriarAlunoValido();
        var data = DateTime.UtcNow;

        // Simula histórico anterior com 3 faltas que geraram alerta Vermelho
        aluno.RegistrarPresenca(StatusPresenca.Falta, data.AddDays(-3));
        aluno.RegistrarPresenca(StatusPresenca.Falta, data.AddDays(-2));
        aluno.RegistrarPresenca(StatusPresenca.Falta, data.AddDays(-1));
        aluno.ClearDomainEvents();

        // Agora recalcula considerando apenas presenças (correção do histórico)
        var registros = new List<RegistroPresenca>();
        for (int i = 0; i < 3; i++)
        {
            var chamada = new Chamada(Guid.NewGuid(), new DateTimeOffset(data.AddDays(i - 3)), TurmaId, Guid.NewGuid());
            registros.Add(chamada.RegistrarPresenca(aluno.Id, StatusPresenca.Presente));
        }

        // Act
        aluno.RecalcularEstatisticas(registros);
        aluno.ReconciliarAlertasPendentes();

        // Assert: deve emitir o evento de normalização de faltas consecutivas
        aluno.DomainEvents.Should().ContainSingle(e => e is FaltasConsecutivasNormalizadasEvent);
        var evento = aluno.DomainEvents.OfType<FaltasConsecutivasNormalizadasEvent>().Single();
        evento.AlunoId.Should().Be(AlunoId);
        evento.FaltasConsecutivasAtuais.Should().Be(0);
    }

    [Fact]
    public void ReconciliarAlertasPendentes_AposCorrecaoDeAtrasos_DeveEmitirEventoDeNormalizacaoDeAtrasos()
    {
        // Arrange
        var aluno = CriarAlunoValido();
        var data = DateTime.UtcNow;

        // Simula histórico anterior com 6 atrasos que geraram alerta Intermediário
        for (int i = 0; i < 6; i++)
            aluno.RegistrarPresenca(StatusPresenca.Atraso, data.AddDays(i - 6));
        aluno.ClearDomainEvents();

        // Agora recalcula considerando apenas presenças (correção do histórico)
        var registros = new List<RegistroPresenca>();
        for (int i = 0; i < 6; i++)
        {
            var chamada = new Chamada(Guid.NewGuid(), new DateTimeOffset(data.AddDays(i - 6)), TurmaId, Guid.NewGuid());
            registros.Add(chamada.RegistrarPresenca(aluno.Id, StatusPresenca.Presente));
        }

        // Act
        aluno.RecalcularEstatisticas(registros);
        aluno.ReconciliarAlertasPendentes();

        // Assert: deve emitir o evento de normalização de atrasos
        aluno.DomainEvents.Should().ContainSingle(e => e is AtrasosTrimestreNormalizadosEvent);
        var evento = aluno.DomainEvents.OfType<AtrasosTrimestreNormalizadosEvent>().Single();
        evento.AlunoId.Should().Be(AlunoId);
        evento.AtrasosNoTrimestre.Should().Be(0);
    }

    [Fact]
    public void ReconciliarAlertasPendentes_QuandoContadoresAcimaDosLimiares_DeveEmitirEventosDeThreshold()
    {
        // Arrange
        var aluno = CriarAlunoValido();
        var data = DateTime.UtcNow;

        var registros = new List<RegistroPresenca>();
        for (int i = 0; i < 3; i++)
        {
            var chamada = new Chamada(Guid.NewGuid(), new DateTimeOffset(data.AddDays(i)), TurmaId, Guid.NewGuid());
            registros.Add(chamada.RegistrarPresenca(aluno.Id, StatusPresenca.Falta));
        }
        for (int i = 0; i < 3; i++)
        {
            var chamada = new Chamada(Guid.NewGuid(), new DateTimeOffset(data.AddDays(i + 4)), TurmaId, Guid.NewGuid());
            registros.Add(chamada.RegistrarPresenca(aluno.Id, StatusPresenca.Atraso));
        }

        aluno.RecalcularEstatisticas(registros);
        aluno.ClearDomainEvents();

        // Act
        aluno.ReconciliarAlertasPendentes();

        // Assert: deve emitir eventos de threshold para o estado final (3 faltas = Vermelho, 3 atrasos = Aviso)
        aluno.DomainEvents.Should().ContainSingle(e => e is LimiteFaltasAtingidoEvent);
        aluno.DomainEvents.Should().ContainSingle(e => e is LimiteAtrasosAtingidoEvent);
    }

    [Fact]
    public void ReconciliarAlertasPendentes_QuandoFaltasCaemParaThresholdInferior_DeveRebaixarAlerta()
    {
        // Arrange
        var aluno = CriarAlunoValido();
        var data = DateTime.UtcNow;

        // Simula histórico anterior com 5 faltas (Preto)
        var registrosPreto = new List<RegistroPresenca>();
        for (int i = 0; i < 5; i++)
        {
            var chamada = new Chamada(Guid.NewGuid(), new DateTimeOffset(data.AddDays(i)), TurmaId, Guid.NewGuid());
            registrosPreto.Add(chamada.RegistrarPresenca(aluno.Id, StatusPresenca.Falta));
        }
        aluno.RecalcularEstatisticas(registrosPreto);
        aluno.ClearDomainEvents();

        // Agora corrige a falta mais antiga para Presente, deixando 4 faltas consecutivas (Vermelho)
        var registrosVermelho = new List<RegistroPresenca>(registrosPreto);
        registrosVermelho[0] = new Chamada(Guid.NewGuid(), new DateTimeOffset(data.AddDays(-1)), TurmaId, Guid.NewGuid())
            .RegistrarPresenca(aluno.Id, StatusPresenca.Presente);

        // Act
        aluno.RecalcularEstatisticas(registrosVermelho);
        aluno.ReconciliarAlertasPendentes();

        // Assert: deve emitir evento de threshold com nível Vermelho (rebaixamento)
        aluno.DomainEvents.Should().ContainSingle(e => e is LimiteFaltasAtingidoEvent);
        var evento = aluno.DomainEvents.OfType<LimiteFaltasAtingidoEvent>().Single();
        evento.Nivel.Should().Be(NivelAlertaFalta.Vermelho);
    }
}
