using EscolaAtenta.API.Controllers;
using EscolaAtenta.Application.Chamadas.Queries;
using EscolaAtenta.Application.Tests.Fakes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EscolaAtenta.Application.Tests.Controllers;

public class ChamadasControllerTests
{
    private readonly IMediator _mediator = new FakeMediator();

    [Theory]
    [InlineData("invalido")]
    [InlineData("2026-13-45")]
    [InlineData("")]
    public async Task ObterChamadaPorDia_DataInvalida_DeveRetornar400(string dataInvalida)
    {
        var controller = new ChamadasController(_mediator);

        var resultado = await controller.ObterChamadaPorDia(Guid.NewGuid(), dataInvalida, CancellationToken.None);

        resultado.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = (BadRequestObjectResult)resultado;
        badRequest.Value.Should().BeEquivalentTo(new { mensagem = $"Data inválida: '{dataInvalida}'." });
    }
}
