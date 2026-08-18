using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Application.Turmas.Handlers;
using EscolaAtenta.Application.Turmas.Queries;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Tests.Handlers;

public class GetTurmasQueryHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public GetTurmasQueryHandlerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private AppDbContext CriarContexto()
    {
        var ctx = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options,
            new FakeCurrentUserService(),
            new FakeMediator(),
            new FakeTenantProvider());

        ctx.Database.EnsureCreated();
        ctx.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF");
        return ctx;
    }

    private static GetTurmasQueryHandler CriarHandler(AppDbContext ctx) => new(ctx);

    [Fact]
    public async Task Handle_SemTurmas_DeveRetornarListaVazia()
    {
        await using var ctx = CriarContexto();

        var resultado = await CriarHandler(ctx).Handle(
            new GetTurmasQuery(), CancellationToken.None);

        resultado.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ComTurmas_DeveRetornarOrdenadoPorNome()
    {
        await using var ctx = CriarContexto();
        ctx.Turmas.Add(new Turma(Guid.NewGuid(), "3º Ano", "Manhã", 2026));
        ctx.Turmas.Add(new Turma(Guid.NewGuid(), "1º Ano", "Manhã", 2026));
        ctx.Turmas.Add(new Turma(Guid.NewGuid(), "2º Ano", "Tarde", 2026));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new GetTurmasQuery(), CancellationToken.None);

        resultado.Should().HaveCount(3);
        resultado[0].Nome.Should().Be("1º Ano");
        resultado[1].Nome.Should().Be("2º Ano");
        resultado[2].Nome.Should().Be("3º Ano");
    }

    [Fact]
    public async Task Handle_DeveMapearDtoCorretamente()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "5º Ano B", "Tarde", 2025));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var resultado = await CriarHandler(ctx).Handle(
            new GetTurmasQuery(), CancellationToken.None);

        var dto = resultado.Single();
        dto.Id.Should().Be(turmaId);
        dto.Nome.Should().Be("5º Ano B");
        dto.Turno.Should().Be("Tarde");
        dto.AnoLetivo.Should().Be(2025);
    }
}
