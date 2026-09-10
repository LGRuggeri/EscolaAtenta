using EscolaAtenta.Application.Alertas.Commands;
using EscolaAtenta.Application.Alertas.Handlers;
using EscolaAtenta.Application.Alunos.Queries;
using EscolaAtenta.Application.Chamadas.Handlers;
using EscolaAtenta.Application.Chamadas.Queries;
using EscolaAtenta.Application.Turmas.Commands;
using EscolaAtenta.Application.Turmas.Handlers;
using EscolaAtenta.Application.Turmas.Queries;
using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EscolaAtenta.Application.Tests.Security;

public class EscopoAcessoTests
{
    [Theory]
    [InlineData("Monitor")]
    [InlineData("Supervisao")]
    [InlineData("Administrador")]
    public async Task LeiturasLimitamTurmasEAlunosAoVinculo(string papel)
    {
        await using var f = await Dados.Criar(papel);
        var esperado = papel == "Administrador" ? 2 : 1;
        (await new GetTurmasQueryHandler(f.Db, f.User).Handle(new GetTurmasQuery(), default)).Should().HaveCount(esperado);
        (await new GetAlunosComFaltasHandler(f.Db, f.User).Handle(new GetAlunosComFaltasQuery(), default)).Should().HaveCount(esperado);
        var pull = new SyncPullHandler(f.Db, NullLogger<SyncPullHandler>.Instance, f.User);
        var full = await pull.Handle(new SyncPullQuery(0), default);
        full.Changes.Turmas.Created.Should().HaveCount(esperado);
        full.Changes.Alunos.Created.Should().HaveCount(esperado);
        full.AllowedTurmaIds.Should().HaveCount(esperado);
        // Delta antigo também precisa aplicar os mesmos filtros.
        (await pull.Handle(new SyncPullQuery(1), default)).Changes.Alunos.Created.Should().HaveCount(esperado);
    }

    [Theory]
    [InlineData("Monitor")]
    [InlineData("Supervisao")]
    public async Task CadastroNaoAdministrativoEhNegadoSemPersistir(string papel)
    {
        await using var f = await Dados.Criar(papel);
        Func<Task> act = () => new CriarTurmaHandler(f.Db, f.User).Handle(new CriarTurmaCommand("Proibida", "Manhã", 2026), default);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        (await f.Db.Turmas.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task SupervisaoNaoResolveAlertaForaDoVinculo()
    {
        await using var f = await Dados.Criar("Supervisao");
        var alunos = await f.Db.Alunos.ToListAsync();
        foreach (var aluno in alunos) f.Db.AlertasEvasao.Add(AlertaEvasao.CriarAlertaAluno(aluno.Id, aluno.TurmaId, NivelAlertaFalta.Aviso, "Teste"));
        await f.Db.SaveChangesAsync();
        var handler = new ResolverAlertaHandler(f.Db, f.User);
        foreach (var alerta in await f.Db.AlertasEvasao.ToListAsync())
        {
            (await handler.Handle(new ResolverAlertaCommand { AlertaId=alerta.Id, Justificativa="Verificado pela supervisão" }, default)).Should().Be(alerta.TurmaId == f.Permitida);
            alerta.Resolvido.Should().Be(alerta.TurmaId == f.Permitida);
        }
    }

    [Fact]
    public async Task IdentidadeInvalidaNaoRecebeDados()
    {
        await using var f = await Dados.Criar("Administrador"); var invalido = new FakeCurrentUserService { UsuarioId="invalido" };
        Func<Task> act = () => new SyncPullHandler(f.Db, NullLogger<SyncPullHandler>.Instance, invalido).Handle(new SyncPullQuery(0), default);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private sealed class Dados : IAsyncDisposable
    {
        public required AppDbContext Db { get; init; }
        public required SqliteConnection Connection { get; init; }
        public required FakeCurrentUserService User { get; init; }
        public required Guid Permitida { get; init; }
        public static async Task<Dados> Criar(string papel)
        {
            var conn=new SqliteConnection("Data Source=:memory:"); await conn.OpenAsync();
            var user=new FakeCurrentUserService { UsuarioId=Guid.NewGuid().ToString(), Papel=papel };
            var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options,user,new FakeMediator(),new FakeTenantProvider());
            await db.Database.EnsureCreatedAsync(); await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF");
            var permitida=Guid.NewGuid(); var outra=Guid.NewGuid();
            db.Turmas.AddRange(new Turma(permitida,"Permitida","Manhã",2026),new Turma(outra,"Outra","Tarde",2026));
            db.Alunos.AddRange(new Aluno(Guid.NewGuid(),"Aluno permitido",null,permitida),new Aluno(Guid.NewGuid(),"Aluno restrito",null,outra));
            db.UsuarioTurmas.Add(new UsuarioTurma(Guid.NewGuid(),Guid.Parse(user.UsuarioId),permitida));
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            return new Dados { Db=db,Connection=conn,User=user,Permitida=permitida };
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }
}
