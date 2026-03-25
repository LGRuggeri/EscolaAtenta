using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Exceptions;

namespace EscolaAtenta.Domain.Tests.Entities;

public class AlertaEvasaoTests
{
    private static readonly Guid AlunoId = Guid.NewGuid();
    private static readonly Guid TurmaId = Guid.NewGuid();

    // ── CriarAlertaAluno ─────────────────────────────────────────────────────

    [Fact]
    public void CriarAlertaAluno_DeveRetornarAlertaNaoResolvido()
    {
        var alerta = AlertaEvasao.CriarAlertaAluno(AlunoId, TurmaId, NivelAlertaFalta.Aviso, "1 falta");

        alerta.AlunoId.Should().Be(AlunoId);
        alerta.TurmaId.Should().Be(TurmaId);
        alerta.Nivel.Should().Be(NivelAlertaFalta.Aviso);
        alerta.Descricao.Should().Be("1 falta");
        alerta.Resolvido.Should().BeFalse();
        alerta.Tipo.Should().Be(TipoAlerta.Evasao);
    }

    [Fact]
    public void CriarAlertaAluno_NivelAcimaDoMaximo_DeveTruncarParaPreto()
    {
        var alerta = AlertaEvasao.CriarAlertaAluno(AlunoId, TurmaId, (NivelAlertaFalta)99, "Excede máximo");

        alerta.Nivel.Should().Be(NivelAlertaFalta.Preto);
    }

    // ── CriarAlertaAtraso ────────────────────────────────────────────────────

    [Fact]
    public void CriarAlertaAtraso_DeveRetornarTipoAtraso()
    {
        var alerta = AlertaEvasao.CriarAlertaAtraso(AlunoId, TurmaId, NivelAlertaFalta.Aviso, "3 atrasos");

        alerta.Tipo.Should().Be(TipoAlerta.Atraso);
        alerta.Resolvido.Should().BeFalse();
    }

    // ── CriarAlertaTurma ─────────────────────────────────────────────────────

    [Fact]
    public void CriarAlertaTurma_DeveNascerResolvidoComNivelExcelencia()
    {
        var alerta = AlertaEvasao.CriarAlertaTurma(TurmaId, "Turma sem faltas");

        alerta.TurmaId.Should().Be(TurmaId);
        alerta.AlunoId.Should().BeNull();
        alerta.Nivel.Should().Be(NivelAlertaFalta.Excelencia);
        alerta.Resolvido.Should().BeTrue();
    }

    // ── MarcarComoResolvido ──────────────────────────────────────────────────

    [Fact]
    public void MarcarComoResolvido_AlertaNaoResolvido_DeveResolverComDados()
    {
        var alerta = AlertaEvasao.CriarAlertaAluno(AlunoId, TurmaId, NivelAlertaFalta.Vermelho, "3 faltas");
        var usuarioId = Guid.NewGuid();

        alerta.MarcarComoResolvido(usuarioId, "Aluno retornou às aulas");

        alerta.Resolvido.Should().BeTrue();
        alerta.ResolvidoPorId.Should().Be(usuarioId);
        alerta.ObservacaoResolucao.Should().Be("Aluno retornou às aulas");
        alerta.JustificativaResolucao.Should().Be("Aluno retornou às aulas");
        alerta.DataResolucao.Should().NotBeNull();
    }

    [Fact]
    public void MarcarComoResolvido_AlertaJaResolvido_DeveLancarDomainException()
    {
        var alerta = AlertaEvasao.CriarAlertaAluno(AlunoId, TurmaId, NivelAlertaFalta.Aviso, "Motivo");
        alerta.MarcarComoResolvido(Guid.NewGuid(), "Resolvido");

        var acao = () => alerta.MarcarComoResolvido(Guid.NewGuid(), "Dupla resolução");

        acao.Should().Throw<DomainException>().WithMessage("*já foi resolvido*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void MarcarComoResolvido_SemJustificativa_DeveLancarDomainException(string? justificativa)
    {
        var alerta = AlertaEvasao.CriarAlertaAluno(AlunoId, TurmaId, NivelAlertaFalta.Aviso, "Motivo");

        var acao = () => alerta.MarcarComoResolvido(Guid.NewGuid(), justificativa!);

        acao.Should().Throw<DomainException>().WithMessage("*observação*obrigatória*");
    }

    // ── AtualizarNivel ───────────────────────────────────────────────────────

    [Fact]
    public void AtualizarNivel_AlertaNaoResolvido_DeveEscalarNivel()
    {
        var alerta = AlertaEvasao.CriarAlertaAluno(AlunoId, TurmaId, NivelAlertaFalta.Aviso, "1 falta");

        alerta.AtualizarNivel(NivelAlertaFalta.Vermelho, "3 faltas agora");

        alerta.Nivel.Should().Be(NivelAlertaFalta.Vermelho);
        alerta.Descricao.Should().Be("3 faltas agora");
    }

    [Fact]
    public void AtualizarNivel_AlertaResolvido_DeveLancarDomainException()
    {
        var alerta = AlertaEvasao.CriarAlertaAluno(AlunoId, TurmaId, NivelAlertaFalta.Aviso, "Motivo");
        alerta.MarcarComoResolvido(Guid.NewGuid(), "Resolvido");

        var acao = () => alerta.AtualizarNivel(NivelAlertaFalta.Vermelho, "Escalada");

        acao.Should().Throw<DomainException>().WithMessage("*já resolvido*");
    }
}
