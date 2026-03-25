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

public class SyncPushHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Guid _monitorId = Guid.NewGuid();

    public SyncPushHandlerTests()
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
            currentUser ?? CriarUsuarioAutenticado(),
            new FakeMediator(),
            new FakeTenantProvider());

        ctx.Database.EnsureCreated();
        ctx.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF");
        return ctx;
    }

    private FakeCurrentUserService CriarUsuarioAutenticado() => new()
    {
        UsuarioId = _monitorId.ToString(),
        EstaAutenticado = true,
        Papel = "Monitor"
    };

    private static SyncPushHandler CriarHandler(AppDbContext ctx, FakeCurrentUserService? currentUser = null) =>
        new(ctx, currentUser ?? new FakeCurrentUserService { UsuarioId = Guid.NewGuid().ToString(), EstaAutenticado = true },
            NullLogger<SyncPushHandler>.Instance, new FakeSqliteWriteLockProvider());

    private static SyncPushCommand CriarCommandVazio() =>
        new(new SyncChanges(), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    [Fact]
    public async Task Handle_PayloadVazio_DeveRetornarZero()
    {
        var user = CriarUsuarioAutenticado();
        await using var ctx = CriarContexto(user);

        var resultado = await CriarHandler(ctx, user).Handle(CriarCommandVazio(), CancellationToken.None);

        resultado.RegistrosSincronizados.Should().Be(0);
        resultado.AlertasGerados.Should().Be(0);
    }

    [Fact]
    public async Task Handle_UsuarioNaoAutenticado_DeveDispararDomainException()
    {
        var fakeUser = new FakeCurrentUserService
        {
            UsuarioId = "sistema",
            EstaAutenticado = false,
            Papel = "Monitor"
        };
        await using var ctx = CriarContexto(fakeUser);

        var command = new SyncPushCommand(
            new SyncChanges
            {
                RegistrosPresenca = new SyncTableData<RegistroPresencaSyncDto>
                {
                    Created = [new RegistroPresencaSyncDto
                    {
                        Id = "abc123", AlunoId = Guid.NewGuid().ToString(),
                        TurmaId = Guid.NewGuid().ToString(), Data = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Status = "Presente"
                    }]
                }
            },
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        Func<Task> act = () => CriarHandler(ctx, fakeUser).Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*autenticação*");
    }

    [Fact]
    public async Task Handle_TurmaCriadaOffline_DevePersistirComSyncLog()
    {
        var user = CriarUsuarioAutenticado();
        await using var ctx = CriarContexto(user);

        var command = new SyncPushCommand(
            new SyncChanges
            {
                Turmas = new SyncTableData<TurmaOfflineSyncDto>
                {
                    Created = [new TurmaOfflineSyncDto
                    {
                        Id = "watermelon-turma-1",
                        Nome = "Turma Offline",
                        Turno = "Tarde",
                        AnoLetivo = 2026
                    }]
                }
            },
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var resultado = await CriarHandler(ctx, user).Handle(command, CancellationToken.None);

        resultado.RegistrosSincronizados.Should().Be(1);

        var syncLog = await ctx.SyncLogs.FirstOrDefaultAsync(s => s.IdExterno == "watermelon-turma-1");
        syncLog.Should().NotBeNull();
        syncLog!.TabelaOrigem.Should().Be("turmas");

        var turma = await ctx.Turmas.FindAsync(syncLog.EntidadeId);
        turma.Should().NotBeNull();
        turma!.Nome.Should().Be("Turma Offline");
    }

    [Fact]
    public async Task Handle_TurmaDuplicada_DeveSerIdempotente()
    {
        var user = CriarUsuarioAutenticado();
        await using var ctx = CriarContexto(user);

        var turmaDto = new TurmaOfflineSyncDto
        {
            Id = "watermelon-dup",
            Nome = "Turma Dup",
            Turno = "Manhã",
            AnoLetivo = 2026
        };

        var command = new SyncPushCommand(
            new SyncChanges
            {
                Turmas = new SyncTableData<TurmaOfflineSyncDto> { Created = [turmaDto] }
            },
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        // Primeiro push
        await CriarHandler(ctx, user).Handle(command, CancellationToken.None);
        ctx.ChangeTracker.Clear();

        // Segundo push com mesmo ID — deve ignorar
        var resultado2 = await CriarHandler(ctx, user).Handle(command, CancellationToken.None);

        resultado2.RegistrosSincronizados.Should().Be(0, "turma duplicada deve ser ignorada");
        var syncLogs = await ctx.SyncLogs.Where(s => s.IdExterno == "watermelon-dup").CountAsync();
        syncLogs.Should().Be(1);
    }

    [Fact]
    public async Task Handle_AlunoCriadoOffline_DevePersistirComSyncLog()
    {
        var user = CriarUsuarioAutenticado();
        await using var ctx = CriarContexto(user);

        // Primeiro cria a turma
        var turmaId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Turma Existente", "Manhã", 2026));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var command = new SyncPushCommand(
            new SyncChanges
            {
                Alunos = new SyncTableData<AlunoOfflineSyncDto>
                {
                    Created = [new AlunoOfflineSyncDto
                    {
                        Id = "watermelon-aluno-1",
                        Nome = "Aluno Offline",
                        TurmaId = turmaId.ToString()
                    }]
                }
            },
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var resultado = await CriarHandler(ctx, user).Handle(command, CancellationToken.None);

        resultado.RegistrosSincronizados.Should().Be(1);

        var syncLog = await ctx.SyncLogs.FirstOrDefaultAsync(s => s.IdExterno == "watermelon-aluno-1");
        syncLog.Should().NotBeNull();
        syncLog!.TabelaOrigem.Should().Be("alunos");
    }

    [Fact]
    public async Task Handle_PresencaCriadaOffline_DeveCriarChamadaERegistro()
    {
        var user = CriarUsuarioAutenticado();
        await using var ctx = CriarContexto(user);
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Turma Sync", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno Sync", null, turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var dataMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var command = new SyncPushCommand(
            new SyncChanges
            {
                RegistrosPresenca = new SyncTableData<RegistroPresencaSyncDto>
                {
                    Created = [new RegistroPresencaSyncDto
                    {
                        Id = "reg-offline-1",
                        AlunoId = alunoId.ToString(),
                        TurmaId = turmaId.ToString(),
                        Data = dataMs,
                        Status = "Presente"
                    }]
                }
            },
            dataMs);

        var resultado = await CriarHandler(ctx, user).Handle(command, CancellationToken.None);

        resultado.RegistrosSincronizados.Should().Be(1);

        // Verifica que a Chamada foi criada
        var chamada = await ctx.Chamadas.Include(c => c.RegistrosPresenca).FirstOrDefaultAsync();
        chamada.Should().NotBeNull();
        chamada!.TurmaId.Should().Be(turmaId);
        chamada.RegistrosPresenca.Should().HaveCount(1);

        // Verifica SyncLog
        var syncLog = await ctx.SyncLogs.FirstOrDefaultAsync(s => s.IdExterno == "reg-offline-1");
        syncLog.Should().NotBeNull();
        syncLog!.TabelaOrigem.Should().Be("registros_presenca");
    }

    [Fact]
    public async Task Handle_StatusInvalido_DeveDispararDomainException()
    {
        var user = CriarUsuarioAutenticado();
        await using var ctx = CriarContexto(user);
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Turma", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno", null, turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var command = new SyncPushCommand(
            new SyncChanges
            {
                RegistrosPresenca = new SyncTableData<RegistroPresencaSyncDto>
                {
                    Created = [new RegistroPresencaSyncDto
                    {
                        Id = "reg-invalido",
                        AlunoId = alunoId.ToString(),
                        TurmaId = turmaId.ToString(),
                        Data = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Status = "StatusInvalido"
                    }]
                }
            },
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        Func<Task> act = () => CriarHandler(ctx, user).Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Status de presença inválido*");
    }

    [Fact]
    public async Task Handle_UpdateRegistroExistente_DeveAlterarStatus()
    {
        var user = CriarUsuarioAutenticado();
        await using var ctx = CriarContexto(user);
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Turma Up", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno Up", null, turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        // Primeiro: cria o registro via push
        var dataMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var createCommand = new SyncPushCommand(
            new SyncChanges
            {
                RegistrosPresenca = new SyncTableData<RegistroPresencaSyncDto>
                {
                    Created = [new RegistroPresencaSyncDto
                    {
                        Id = "reg-update-1",
                        AlunoId = alunoId.ToString(),
                        TurmaId = turmaId.ToString(),
                        Data = dataMs,
                        Status = "Presente"
                    }]
                }
            },
            dataMs);

        await CriarHandler(ctx, user).Handle(createCommand, CancellationToken.None);
        ctx.ChangeTracker.Clear();

        // Segundo: atualiza status para Falta
        var updateCommand = new SyncPushCommand(
            new SyncChanges
            {
                RegistrosPresenca = new SyncTableData<RegistroPresencaSyncDto>
                {
                    Updated = [new RegistroPresencaSyncDto
                    {
                        Id = "reg-update-1",
                        AlunoId = alunoId.ToString(),
                        TurmaId = turmaId.ToString(),
                        Data = dataMs,
                        Status = "Falta"
                    }]
                }
            },
            dataMs);

        var resultado = await CriarHandler(ctx, user).Handle(updateCommand, CancellationToken.None);

        resultado.RegistrosSincronizados.Should().Be(1);

        // Verifica o status atualizado
        var syncLog = await ctx.SyncLogs.FirstAsync(s => s.IdExterno == "reg-update-1");
        var registro = await ctx.RegistrosPresenca.FindAsync(syncLog.EntidadeId);
        registro!.Status.Should().Be(StatusPresenca.Falta);
    }

    [Fact]
    public async Task Handle_UpdateMesmoStatus_DeveIgnorarSemErro()
    {
        var user = CriarUsuarioAutenticado();
        await using var ctx = CriarContexto(user);
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Turma Skip", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno Skip", null, turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        // Cria registro com status Presente
        var dataMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var createCommand = new SyncPushCommand(
            new SyncChanges
            {
                RegistrosPresenca = new SyncTableData<RegistroPresencaSyncDto>
                {
                    Created = [new RegistroPresencaSyncDto
                    {
                        Id = "reg-skip-1",
                        AlunoId = alunoId.ToString(),
                        TurmaId = turmaId.ToString(),
                        Data = dataMs,
                        Status = "Presente"
                    }]
                }
            },
            dataMs);

        await CriarHandler(ctx, user).Handle(createCommand, CancellationToken.None);
        ctx.ChangeTracker.Clear();

        // Update com mesmo status — DomainException é capturada internamente (skip)
        var updateCommand = new SyncPushCommand(
            new SyncChanges
            {
                RegistrosPresenca = new SyncTableData<RegistroPresencaSyncDto>
                {
                    Updated = [new RegistroPresencaSyncDto
                    {
                        Id = "reg-skip-1",
                        AlunoId = alunoId.ToString(),
                        TurmaId = turmaId.ToString(),
                        Data = dataMs,
                        Status = "Presente" // Mesmo status
                    }]
                }
            },
            dataMs);

        var resultado = await CriarHandler(ctx, user).Handle(updateCommand, CancellationToken.None);

        resultado.RegistrosSincronizados.Should().Be(0, "status idêntico deve ser ignorado (skip)");
    }
}
