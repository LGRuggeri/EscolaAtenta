using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Exceptions;

namespace EscolaAtenta.Domain.Tests.Entities;

/// <summary>
/// Testes para RegistroPresenca — criado via Chamada.RegistrarPresenca() (construtor internal).
/// Os testes de criação e duplicidade já estão em ChamadaTests.
/// Aqui testamos apenas AlterarStatus().
/// </summary>
public class RegistroPresencaTests
{
    private static RegistroPresenca CriarRegistroViaChama(StatusPresenca statusInicial = StatusPresenca.Presente)
    {
        var chamada = new Chamada(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), Guid.NewGuid());
        return chamada.RegistrarPresenca(Guid.NewGuid(), statusInicial);
    }

    [Fact]
    public void AlterarStatus_ParaStatusDiferente_DeveAtualizar()
    {
        var registro = CriarRegistroViaChama(StatusPresenca.Presente);

        registro.AlterarStatus(StatusPresenca.Falta);

        registro.Status.Should().Be(StatusPresenca.Falta);
    }

    [Fact]
    public void AlterarStatus_ParaMesmoStatus_DeveLancarDomainException()
    {
        var registro = CriarRegistroViaChama(StatusPresenca.Presente);

        var acao = () => registro.AlterarStatus(StatusPresenca.Presente);

        acao.Should().Throw<DomainException>().WithMessage("*já é*");
    }

    [Fact]
    public void AlterarStatus_DevePermitirVariosStatusDiferentes()
    {
        var registro = CriarRegistroViaChama(StatusPresenca.Presente);

        registro.AlterarStatus(StatusPresenca.Falta);
        registro.AlterarStatus(StatusPresenca.FaltaJustificada);

        registro.Status.Should().Be(StatusPresenca.FaltaJustificada);
    }
}
