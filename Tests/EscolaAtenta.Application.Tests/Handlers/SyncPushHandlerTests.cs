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
        Papel = "Administrador"
    };

    private FakeCurrentUserService CriarMonitorAutenticado() => new()
    {
        UsuarioId = _monitorId.ToString(),
        EstaAutenticado = true,
        Papel = "Monitor"
    };

    private static async Task VincularUsuarioTurma(AppDbContext ctx, Guid usuarioId, Guid turmaId)
    {
        ctx.UsuarioTurmas.Add(new UsuarioTurma(Guid.NewGuid(), usuarioId, turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
    }

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

    [Fact]
    public async Task Handle_CriadoParaDiaJaExistente_DeveAtualizarStatus()
    {
        var user = CriarUsuarioAutenticado();
        await using var ctx = CriarContexto(user);
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Turma Existente", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno Existente", null, turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var dataMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var createCommand = new SyncPushCommand(
            new SyncChanges
            {
                RegistrosPresenca = new SyncTableData<RegistroPresencaSyncDto>
                {
                    Created = [new RegistroPresencaSyncDto
                    {
                        Id = "reg-dia-existente-1",
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

        // Simula novo registro offline para o mesmo dia/turma com status diferente
        var updateCommand = new SyncPushCommand(
            new SyncChanges
            {
                RegistrosPresenca = new SyncTableData<RegistroPresencaSyncDto>
                {
                    Created = [new RegistroPresencaSyncDto
                    {
                        Id = "reg-dia-existente-2",
                        AlunoId = alunoId.ToString(),
                        TurmaId = turmaId.ToString(),
                        Data = dataMs,
                        Status = "Falta"
                    }]
                }
            },
            dataMs);

        var resultado = await CriarHandler(ctx, user).Handle(updateCommand, CancellationToken.None);

        resultado.RegistrosSincronizados.Should().Be(1, "deve contar o registro atualizado");

        var chamadas = await ctx.Chamadas.Include(c => c.RegistrosPresenca).ToListAsync();
        chamadas.Should().HaveCount(1, "não deve criar chamada duplicada");
        chamadas[0].RegistrosPresenca.Should().HaveCount(1);
        chamadas[0].RegistrosPresenca.First().Status.Should().Be(StatusPresenca.Falta);
    }

    [Fact]
    public async Task Handle_CriadoParaDiaJaExistenteForaDe7Dias_DeveIgnorar()
    {
        var user = CriarUsuarioAutenticado();
        await using var ctx = CriarContexto(user);
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Turma Bloqueada", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno Bloqueado", null, turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var dataMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var createCommand = new SyncPushCommand(
            new SyncChanges
            {
                RegistrosPresenca = new SyncTableData<RegistroPresencaSyncDto>
                {
                    Created = [new RegistroPresencaSyncDto
                    {
                        Id = "reg-bloqueado-1",
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

        // Simula que a chamada foi criada há mais de 7 dias
        var chamada = await ctx.Chamadas.FirstAsync(c => c.TurmaId == turmaId);
        var dataAntiga = DateTimeOffset.UtcNow.AddDays(-10);
        await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Chamadas SET DataCriacao = {dataAntiga} WHERE Id = {chamada.Id}");
        ctx.ChangeTracker.Clear();

        var pushBloqueado = new SyncPushCommand(
            new SyncChanges
            {
                RegistrosPresenca = new SyncTableData<RegistroPresencaSyncDto>
                {
                    Created = [new RegistroPresencaSyncDto
                    {
                        Id = "reg-bloqueado-2",
                        AlunoId = alunoId.ToString(),
                        TurmaId = turmaId.ToString(),
                        Data = dataMs,
                        Status = "Falta"
                    }]
                }
            },
            dataMs);

        var resultado = await CriarHandler(ctx, user).Handle(pushBloqueado, CancellationToken.None);

        resultado.RegistrosSincronizados.Should().Be(0, "deve ignorar pois passou o prazo de 7 dias");
        resultado.Rejeicoes.Should().HaveCount(1);
        resultado.Rejeicoes[0].IdExterno.Should().Be("reg-bloqueado-2");

        var registro = await ctx.RegistrosPresenca.FirstAsync(r => r.AlunoId == alunoId);
        registro.Status.Should().Be(StatusPresenca.Presente);
    }

    [Fact]
    public async Task Handle_UpdateForaDe7Dias_DeveIgnorarERejeitar()
    {
        var user = CriarUsuarioAutenticado();
        await using var ctx = CriarContexto(user);
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Turma Update Bloqueado", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno Update Bloqueado", null, turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var dataMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var createCommand = new SyncPushCommand(
            new SyncChanges
            {
                RegistrosPresenca = new SyncTableData<RegistroPresencaSyncDto>
                {
                    Created = [new RegistroPresencaSyncDto
                    {
                        Id = "reg-update-bloqueado",
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

        // Simula que a chamada foi criada há mais de 7 dias
        var chamada = await ctx.Chamadas.FirstAsync(c => c.TurmaId == turmaId);
        var dataAntiga = DateTimeOffset.UtcNow.AddDays(-10);
        await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Chamadas SET DataCriacao = {dataAntiga} WHERE Id = {chamada.Id}");
        ctx.ChangeTracker.Clear();

        var updateCommand = new SyncPushCommand(
            new SyncChanges
            {
                RegistrosPresenca = new SyncTableData<RegistroPresencaSyncDto>
                {
                    Updated = [new RegistroPresencaSyncDto
                    {
                        Id = "reg-update-bloqueado",
                        AlunoId = alunoId.ToString(),
                        TurmaId = turmaId.ToString(),
                        Data = dataMs,
                        Status = "Falta"
                    }]
                }
            },
            dataMs);

        var resultado = await CriarHandler(ctx, user).Handle(updateCommand, CancellationToken.None);

        resultado.RegistrosSincronizados.Should().Be(0, "update deve ser ignorado pois passou o prazo de 7 dias");
        resultado.Rejeicoes.Should().HaveCount(1);
        resultado.Rejeicoes[0].IdExterno.Should().Be("reg-update-bloqueado");

        var registro = await ctx.RegistrosPresenca.FirstAsync(r => r.AlunoId == alunoId);
        registro.Status.Should().Be(StatusPresenca.Presente);
    }

    [Fact]
    public async Task Handle_ChamadaExistentePorTurmaEDia_DeveMergearSemCriarDuplicata()
    {
        var user = CriarUsuarioAutenticado();
        await using var ctx = CriarContexto(user);
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Merge", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno Merge", null, turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var dataMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var dataChamada = DateTimeOffset.UtcNow;

        // Cria uma única chamada para o dia/turma
        var chamadaExistente = new Chamada(Guid.NewGuid(), dataChamada, turmaId, Guid.NewGuid());
        chamadaExistente.RegistrarPresenca(alunoId, StatusPresenca.Falta);
        ctx.Chamadas.Add(chamadaExistente);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var pushCommand = new SyncPushCommand(
            new SyncChanges
            {
                RegistrosPresenca = new SyncTableData<RegistroPresencaSyncDto>
                {
                    Created = [new RegistroPresencaSyncDto
                    {
                        Id = "reg-merge",
                        AlunoId = alunoId.ToString(),
                        TurmaId = turmaId.ToString(),
                        Data = dataMs,
                        Status = "Presente"
                    }]
                }
            },
            dataMs);

        var resultado = await CriarHandler(ctx, user).Handle(pushCommand, CancellationToken.None);

        resultado.RegistrosSincronizados.Should().Be(1);

        // Deve manter apenas uma chamada para a turma no dia
        var chamadas = await ctx.Chamadas.Where(c => c.TurmaId == turmaId).ToListAsync();
        chamadas.Should().HaveCount(1, "não deve criar chamada duplicada");

        var syncLog = await ctx.SyncLogs.FirstAsync(s => s.IdExterno == "reg-merge");
        var registro = await ctx.RegistrosPresenca.FindAsync(syncLog.EntidadeId);
        registro.Should().NotBeNull();
        registro!.Status.Should().Be(StatusPresenca.Presente);
    }

    [Fact]
    public async Task Handle_MesmoStatusNoMerge_DeveCriarSyncLogParaFuturosUpdates()
    {
        var user = CriarUsuarioAutenticado();
        await using var ctx = CriarContexto(user);
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Merge", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno Merge", null, turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var dataMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var createCommand = new SyncPushCommand(
            new SyncChanges
            {
                RegistrosPresenca = new SyncTableData<RegistroPresencaSyncDto>
                {
                    Created = [new RegistroPresencaSyncDto
                    {
                        Id = "reg-merge-1",
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

        // Segundo push com o MESMO status para um novo IdExterno local
        var mergeCommand = new SyncPushCommand(
            new SyncChanges
            {
                RegistrosPresenca = new SyncTableData<RegistroPresencaSyncDto>
                {
                    Created = [new RegistroPresencaSyncDto
                    {
                        Id = "reg-merge-2",
                        AlunoId = alunoId.ToString(),
                        TurmaId = turmaId.ToString(),
                        Data = dataMs,
                        Status = "Presente"
                    }]
                }
            },
            dataMs);

        var resultado = await CriarHandler(ctx, user).Handle(mergeCommand, CancellationToken.None);

        resultado.RegistrosSincronizados.Should().Be(1);

        var syncLog = await ctx.SyncLogs.FirstOrDefaultAsync(s => s.IdExterno == "reg-merge-2");
        syncLog.Should().NotBeNull("SyncLog deve ser criado mesmo quando o status já coincide");

        // Agora edita o segundo registro localmente e faz update
        var updateCommand = new SyncPushCommand(
            new SyncChanges
            {
                RegistrosPresenca = new SyncTableData<RegistroPresencaSyncDto>
                {
                    Updated = [new RegistroPresencaSyncDto
                    {
                        Id = "reg-merge-2",
                        AlunoId = alunoId.ToString(),
                        TurmaId = turmaId.ToString(),
                        Data = dataMs,
                        Status = "Falta"
                    }]
                }
            },
            dataMs);

        var resultadoUpdate = await CriarHandler(ctx, user).Handle(updateCommand, CancellationToken.None);

        resultadoUpdate.RegistrosSincronizados.Should().Be(1, "update deve funcionar porque SyncLog foi criado no merge");
        var registro = await ctx.RegistrosPresenca.FindAsync(syncLog!.EntidadeId);
        registro!.Status.Should().Be(StatusPresenca.Falta);
    }

    [Fact]
    public async Task Handle_UpdateENovosRegistrosMesmoAluno_DeveRecalcularComTodosOsRegistros()
    {
        var user = CriarUsuarioAutenticado();
        await using var ctx = CriarContexto(user);
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Recalc", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno Recalc", null, turmaId));
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var dataDia1 = new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var createCommand = new SyncPushCommand(
            new SyncChanges
            {
                RegistrosPresenca = new SyncTableData<RegistroPresencaSyncDto>
                {
                    Created = [new RegistroPresencaSyncDto
                    {
                        Id = "reg-recalc-1",
                        AlunoId = alunoId.ToString(),
                        TurmaId = turmaId.ToString(),
                        Data = dataDia1,
                        Status = "Falta"
                    }]
                }
            },
            dataDia1);

        await CriarHandler(ctx, user).Handle(createCommand, CancellationToken.None);
        ctx.ChangeTracker.Clear();

        // Novo batch: corrige dia 1 para Presente e adiciona dia 2 com Falta
        var dataDia2 = new DateTimeOffset(2026, 1, 11, 8, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var batchCommand = new SyncPushCommand(
            new SyncChanges
            {
                RegistrosPresenca = new SyncTableData<RegistroPresencaSyncDto>
                {
                    Created =
                    [
                        new RegistroPresencaSyncDto
                        {
                            Id = "reg-recalc-2",
                            AlunoId = alunoId.ToString(),
                            TurmaId = turmaId.ToString(),
                            Data = dataDia1,
                            Status = "Presente"
                        },
                        new RegistroPresencaSyncDto
                        {
                            Id = "reg-recalc-3",
                            AlunoId = alunoId.ToString(),
                            TurmaId = turmaId.ToString(),
                            Data = dataDia2,
                            Status = "Falta"
                        }
                    ]
                }
            },
            dataDia2);

        await CriarHandler(ctx, user).Handle(batchCommand, CancellationToken.None);
        ctx.ChangeTracker.Clear();

        // Resultado esperado: dia 1 Presente, dia 2 Falta
        var aluno = await ctx.Alunos.IgnoreQueryFilters().FirstAsync(a => a.Id == alunoId);
        aluno.TotalFaltas.Should().Be(1, "apenas a falta do dia 2 deve contar");
        aluno.FaltasConsecutivasAtuais.Should().Be(1);
    }

    // ── Testes IDOR ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_MonitorSemVinculo_DeveRejeitarRegistrosDeOutraTurma()
    {
        var monitor = CriarMonitorAutenticado();
        await using var ctx = CriarContexto(monitor);
        var turmaPermitida = Guid.NewGuid();
        var turmaProtegida = Guid.NewGuid();
        var alunoId = Guid.NewGuid();

        ctx.Turmas.AddRange(
            new Turma(turmaPermitida, "Permitida", "Manhã", 2026),
            new Turma(turmaProtegida, "Protegida", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno Teste", null, turmaProtegida));
        await ctx.SaveChangesAsync();

        // Vincula o monitor apenas à turma permitida
        await VincularUsuarioTurma(ctx, _monitorId, turmaPermitida);

        var dataMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var pushCommand = new SyncPushCommand(
            new SyncChanges
            {
                RegistrosPresenca = new SyncTableData<RegistroPresencaSyncDto>
                {
                    Created = [new RegistroPresencaSyncDto
                    {
                        Id = "reg-idor",
                        AlunoId = alunoId.ToString(),
                        TurmaId = turmaProtegida.ToString(),
                        Data = dataMs,
                        Status = "Presente"
                    }]
                }
            },
            dataMs);

        var resultado = await CriarHandler(ctx, monitor).Handle(pushCommand, CancellationToken.None);

        resultado.RegistrosSincronizados.Should().Be(0);
        resultado.Rejeicoes.Should().HaveCount(1);
        resultado.Rejeicoes[0].IdExterno.Should().Be("reg-idor");

        var chamada = await ctx.Chamadas.FirstOrDefaultAsync(c => c.TurmaId == turmaProtegida);
        chamada.Should().BeNull();
    }

    [Fact]
    public async Task Handle_MonitorComVinculo_DevePermitirRegistrosDaTurma()
    {
        var monitor = CriarMonitorAutenticado();
        await using var ctx = CriarContexto(monitor);
        var turmaId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        ctx.Turmas.Add(new Turma(turmaId, "Vinculada", "Manhã", 2026));
        ctx.Alunos.Add(new Aluno(alunoId, "Aluno Vinculado", null, turmaId));
        await ctx.SaveChangesAsync();

        await VincularUsuarioTurma(ctx, _monitorId, turmaId);

        var dataMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var pushCommand = new SyncPushCommand(
            new SyncChanges
            {
                RegistrosPresenca = new SyncTableData<RegistroPresencaSyncDto>
                {
                    Created = [new RegistroPresencaSyncDto
                    {
                        Id = "reg-vinculo",
                        AlunoId = alunoId.ToString(),
                        TurmaId = turmaId.ToString(),
                        Data = dataMs,
                        Status = "Presente"
                    }]
                }
            },
            dataMs);

        var resultado = await CriarHandler(ctx, monitor).Handle(pushCommand, CancellationToken.None);

        resultado.RegistrosSincronizados.Should().Be(1);
        resultado.Rejeicoes.Should().BeEmpty();
    }
}
