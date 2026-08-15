using EscolaAtenta.API.Controllers;
using EscolaAtenta.Application.Chamadas.Commands;
using EscolaAtenta.Application.Tests.Fakes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EscolaAtenta.Application.Tests.Controllers;

public class SyncControllerTests
{
    private class SyncMediatorComRejeicoes : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (typeof(TResponse) == typeof(SyncPushResult))
            {
                var resultado = new SyncPushResult(
                    0,
                    0,
                    [new SyncRejeicao("reg-local-1", "Prazo de 7 dias expirado.")]);
                return Task.FromResult((TResponse)(object)resultado);
            }

            return Task.FromResult(default(TResponse)!);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
            => Task.CompletedTask;

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => Task.FromResult<object?>(null);

        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.CompletedTask;

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    [Fact]
    public async Task Push_ComRejeicoes_DeveRetornar422UnprocessableEntity()
    {
        var mediator = new SyncMediatorComRejeicoes();
        var controller = new SyncController(mediator);

        var command = new SyncPushCommand(
            new SyncChanges(),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var resultado = await controller.Push(command, CancellationToken.None);

        resultado.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)resultado;
        objectResult.StatusCode.Should().Be(422);
        objectResult.Value.Should().BeOfType<SyncPushResult>();
        var value = (SyncPushResult)objectResult.Value!;
        value.Rejeicoes.Should().HaveCount(1);
    }

    [Fact]
    public async Task Push_SemRejeicoes_DeveRetornar200Ok()
    {
        var mediator = new FakeMediator();
        var controller = new SyncController(mediator);

        var command = new SyncPushCommand(
            new SyncChanges(),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var resultado = await controller.Push(command, CancellationToken.None);

        resultado.Should().BeOfType<OkObjectResult>();
    }
}
