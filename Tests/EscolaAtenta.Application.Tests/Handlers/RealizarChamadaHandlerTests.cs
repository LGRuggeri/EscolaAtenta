using EscolaAtenta.Application.Chamadas.Commands;
using EscolaAtenta.Application.Chamadas.Handlers;
using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Exceptions;
using EscolaAtenta.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EscolaAtenta.Application.Tests.Handlers;

public class RealizarChamadaHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public RealizarChamadaHandlerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private AppDbContext CriarContexto(FakeCurrentUserService? currentUser = null)
    {
        var ctx = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options,
            currentUser ?? new FakeCurrentUserService(),
            new FakeMediator(),
            new FakeTenantProvider());

        ctx.Database.EnsureCreated();
        ctx.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF");
        return ctx;
    }

    private static RealizarChamadaHandler CriarHandler(AppDbContext ctx, FakeCurrentUserService? currentUser = null) =>
        new(ctx, currentUser ?? new FakeCurrentUserService(), NullLogger<RealizarChamadaHandler>.Instance);

    /// <summary>
    /// Vincula um usuário a uma turma para passar na validação IDOR.
    /// </summary>
    private static async Task VincularUsuarioTurma(AppDbContext ctx, Guid usuarioId, Guid turmaId)
    {
        ctx.UsuarioTurmas.Add(new UsuarioTurma(Guid.NewGuid(), usuarioId, turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Handle_QuandoTurmaNaoExiste_DeveDispararDomainException()
    {
        await using var ctx = CriarContexto();

        var command = new RealizarChamadaCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            [new RegistroAlunoDto(Guid.NewGuid(), StatusPresenca.Presente)]);

        Func<Task> act = () => CriarHandler(ctx).Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*turma*não existe*");
    }

    [Fact]
    public async Task Handle_QuandoComandoValido_DeveCriarChamadaComRegistros()
    {
        // FakeCurrentUserService padrão usa Papel = "Administrador" (bypassa IDOR)
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        var turma = new Turma(turmaId, "3º Ano A", "Manhã", 2026);
        var aluno1 = new Aluno(Guid.NewGuid(), "João", "MAT001", turmaId);
        var aluno2 = new Aluno(Guid.NewGuid(), "Maria", "MAT002", turmaId);
        ctx.Turmas.Add(turma);
        ctx.Alunos.AddRange(aluno1, aluno2);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var command = new RealizarChamadaCommand(
            turmaId,
            Guid.NewGuid(),
            [
                new RegistroAlunoDto(aluno1.Id, StatusPresenca.Presente),
                new RegistroAlunoDto(aluno2.Id, StatusPresenca.Falta)
            ]);

        var resultado = await CriarHandler(ctx).Handle(command, CancellationToken.None);

        resultado.ChamadaId.Should().NotBeEmpty();

        var chamadaSalva = await ctx.Chamadas
            .Include(c => c.RegistrosPresenca)
            .FirstAsync(c => c.Id == resultado.ChamadaId);

        chamadaSalva.TurmaId.Should().Be(turmaId);
        chamadaSalva.RegistrosPresenca.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_QuandoAutenticado_DeveUsarUsuarioIdDoTokenComoResponsavel()
    {
        var usuarioId = Guid.NewGuid();
        var fakeUser = new FakeCurrentUserService
        {
            UsuarioId = usuarioId.ToString(),
            EstaAutenticado = true,
            Papel = "Monitor"
        };
        await using var ctx = CriarContexto(fakeUser);
        var turmaId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "1º Ano", "Manhã", 2026));
        var aluno = new Aluno(Guid.NewGuid(), "Pedro", null, turmaId);
        ctx.Alunos.Add(aluno);
        await ctx.SaveChangesAsync();

        // Vincula o monitor à turma (IDOR)
        await VincularUsuarioTurma(ctx, usuarioId, turmaId);

        var responsavelFalso = Guid.NewGuid(); // Tentativa de spoofing
        var command = new RealizarChamadaCommand(
            turmaId,
            responsavelFalso,
            [new RegistroAlunoDto(aluno.Id, StatusPresenca.Presente)]);

        var resultado = await CriarHandler(ctx, fakeUser).Handle(command, CancellationToken.None);

        var chamada = await ctx.Chamadas.FindAsync(resultado.ChamadaId);
        chamada!.ResponsavelId.Should().Be(usuarioId, "deve usar o UsuarioId do JWT, não o enviado pelo cliente");
    }

    [Fact]
    public async Task Handle_QuandoNaoAutenticado_DeveUsarResponsavelIdDoRequest()
    {
        var fakeUser = new FakeCurrentUserService
        {
            UsuarioId = "sistema",
            EstaAutenticado = false,
            Papel = "Monitor"
        };
        await using var ctx = CriarContexto(fakeUser);
        var turmaId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "1º Ano", "Manhã", 2026));
        var aluno = new Aluno(Guid.NewGuid(), "Ana", null, turmaId);
        ctx.Alunos.Add(aluno);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var responsavelId = Guid.NewGuid();
        var command = new RealizarChamadaCommand(
            turmaId,
            responsavelId,
            [new RegistroAlunoDto(aluno.Id, StatusPresenca.Presente)]);

        var resultado = await CriarHandler(ctx, fakeUser).Handle(command, CancellationToken.None);

        var chamada = await ctx.Chamadas.FindAsync(resultado.ChamadaId);
        chamada!.ResponsavelId.Should().Be(responsavelId);
    }

    [Fact]
    public async Task Handle_AlunoInexistenteNaLista_DeveIgnorarSemErro()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "2º Ano", "Tarde", 2026));
        var alunoReal = new Aluno(Guid.NewGuid(), "Lucas", null, turmaId);
        ctx.Alunos.Add(alunoReal);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var alunoInexistenteId = Guid.NewGuid();
        var command = new RealizarChamadaCommand(
            turmaId,
            Guid.NewGuid(),
            [
                new RegistroAlunoDto(alunoReal.Id, StatusPresenca.Presente),
                new RegistroAlunoDto(alunoInexistenteId, StatusPresenca.Falta)
            ]);

        var resultado = await CriarHandler(ctx).Handle(command, CancellationToken.None);

        var chamada = await ctx.Chamadas
            .Include(c => c.RegistrosPresenca)
            .FirstAsync(c => c.Id == resultado.ChamadaId);

        chamada.RegistrosPresenca.Should().HaveCount(1, "aluno inexistente deve ser ignorado");
    }

    [Fact]
    public async Task Handle_QuandoFaltaRegistrada_DeveIncrementarContadorDoAluno()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "4º Ano", "Manhã", 2026));
        var aluno = new Aluno(Guid.NewGuid(), "Carlos", "MAT010", turmaId);
        ctx.Alunos.Add(aluno);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var command = new RealizarChamadaCommand(
            turmaId,
            Guid.NewGuid(),
            [new RegistroAlunoDto(aluno.Id, StatusPresenca.Falta)]);

        await CriarHandler(ctx).Handle(command, CancellationToken.None);
        ctx.ChangeTracker.Clear();

        var salvo = await ctx.Alunos.IgnoreQueryFilters().FirstAsync(a => a.Id == aluno.Id);
        salvo.TotalFaltas.Should().Be(1);
        salvo.FaltasConsecutivasAtuais.Should().Be(1);
    }

    [Fact]
    public async Task Handle_QuandoPresencaRegistrada_DeveZerarFaltasConsecutivas()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "5º Ano", "Manhã", 2026));
        var aluno = new Aluno(Guid.NewGuid(), "Fernanda", null, turmaId);
        ctx.Alunos.Add(aluno);
        await ctx.SaveChangesAsync();

        // Primeira chamada: falta
        ctx.ChangeTracker.Clear();
        var cmd1 = new RealizarChamadaCommand(
            turmaId, Guid.NewGuid(),
            [new RegistroAlunoDto(aluno.Id, StatusPresenca.Falta)]);
        await CriarHandler(ctx).Handle(cmd1, CancellationToken.None);

        // Segunda chamada: presença (deve zerar consecutivas)
        ctx.ChangeTracker.Clear();
        var cmd2 = new RealizarChamadaCommand(
            turmaId, Guid.NewGuid(),
            [new RegistroAlunoDto(aluno.Id, StatusPresenca.Presente)]);
        await CriarHandler(ctx).Handle(cmd2, CancellationToken.None);
        ctx.ChangeTracker.Clear();

        var salvo = await ctx.Alunos.IgnoreQueryFilters().FirstAsync(a => a.Id == aluno.Id);
        salvo.FaltasConsecutivasAtuais.Should().Be(0);
        salvo.TotalFaltas.Should().Be(1, "total histórico é preservado");
    }

    [Fact]
    public async Task Handle_QuandoAlertaGerado_DeveRetornarContagemDeAlertas()
    {
        await using var ctx = CriarContexto();
        var turmaId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "6º Ano", "Tarde", 2026));
        var aluno = new Aluno(Guid.NewGuid(), "Roberto", null, turmaId);
        ctx.Alunos.Add(aluno);
        await ctx.SaveChangesAsync();

        // Registra faltas para acumular o contador (1ª falta já gera domain event)
        ctx.ChangeTracker.Clear();
        var cmd = new RealizarChamadaCommand(
            turmaId, Guid.NewGuid(),
            [new RegistroAlunoDto(aluno.Id, StatusPresenca.Falta)]);
        var resultado = await CriarHandler(ctx).Handle(cmd, CancellationToken.None);

        resultado.AlertasGerados.Should().BeGreaterThanOrEqualTo(1);
    }

    // ── Testes IDOR ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_MonitorSemVinculo_DeveNegarAcesso()
    {
        var monitorId = Guid.NewGuid();
        var fakeUser = new FakeCurrentUserService
        {
            UsuarioId = monitorId.ToString(),
            EstaAutenticado = true,
            Papel = "Monitor"
        };
        await using var ctx = CriarContexto(fakeUser);
        var turmaId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "7º Ano", "Manhã", 2026));
        var aluno = new Aluno(Guid.NewGuid(), "Teste", null, turmaId);
        ctx.Alunos.Add(aluno);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        // NÃO vincula o monitor à turma

        var command = new RealizarChamadaCommand(
            turmaId, Guid.NewGuid(),
            [new RegistroAlunoDto(aluno.Id, StatusPresenca.Presente)]);

        Func<Task> act = () => CriarHandler(ctx, fakeUser).Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*permissão*");
    }

    [Fact]
    public async Task Handle_MonitorComVinculo_DevePermitirChamada()
    {
        var monitorId = Guid.NewGuid();
        var fakeUser = new FakeCurrentUserService
        {
            UsuarioId = monitorId.ToString(),
            EstaAutenticado = true,
            Papel = "Monitor"
        };
        await using var ctx = CriarContexto(fakeUser);
        var turmaId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "8º Ano", "Tarde", 2026));
        var aluno = new Aluno(Guid.NewGuid(), "Vinculado", null, turmaId);
        ctx.Alunos.Add(aluno);
        await ctx.SaveChangesAsync();

        await VincularUsuarioTurma(ctx, monitorId, turmaId);

        var command = new RealizarChamadaCommand(
            turmaId, Guid.NewGuid(),
            [new RegistroAlunoDto(aluno.Id, StatusPresenca.Presente)]);

        var resultado = await CriarHandler(ctx, fakeUser).Handle(command, CancellationToken.None);

        resultado.ChamadaId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_AdministradorSemVinculo_DevePermitirChamada()
    {
        // Administrador bypassa IDOR — não precisa de vínculo UsuarioTurma
        await using var ctx = CriarContexto(); // FakeCurrentUserService padrão = Administrador
        var turmaId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "9º Ano", "Manhã", 2026));
        var aluno = new Aluno(Guid.NewGuid(), "Admin", null, turmaId);
        ctx.Alunos.Add(aluno);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var command = new RealizarChamadaCommand(
            turmaId, Guid.NewGuid(),
            [new RegistroAlunoDto(aluno.Id, StatusPresenca.Presente)]);

        var resultado = await CriarHandler(ctx).Handle(command, CancellationToken.None);

        resultado.ChamadaId.Should().NotBeEmpty();
    }
}
