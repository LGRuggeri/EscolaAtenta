using EscolaAtenta.Application.Tests.Fakes;
using EscolaAtenta.Application.Chamadas.Commands;
using EscolaAtenta.Application.Chamadas.Handlers;
using EscolaAtenta.Application.EventHandlers;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Events;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

foreach (var falhar in new[] {false, true})
{
    await using var conn = new SqliteConnection("Data Source=:memory:"); await conn.OpenAsync();
    var current = new FakeCurrentUserService { UsuarioId=Guid.NewGuid().ToString(), Papel="Administrador" };
    var mediator = new MediatorVerificacao();
    await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options, current, mediator, new FakeTenantProvider());
    await db.Database.EnsureCreatedAsync();
    await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=OFF");
    var turma = new Turma(Guid.NewGuid(), "Sintética", "Manhã", 2026);
    var aluno = new Aluno(Guid.NewGuid(), "Aluno sintético", null, turma.Id);
    var chamada = new Chamada(Guid.NewGuid(), DateTimeOffset.UtcNow, turma.Id, Guid.Parse(current.UsuarioId!));
    db.AddRange(turma, aluno, chamada); await db.SaveChangesAsync();
    var publicacoes=0;
    mediator.AoPublicar = async (evt, ct) => {
        if (evt is LimiteFaltasAtingidoEvent limite) {
            publicacoes++;
            if (falhar) throw new InvalidOperationException("Falha sintética no despacho do alerta");
            await new LimiteFaltasAtingidoHandler(db, NullLogger<LimiteFaltasAtingidoHandler>.Instance).Handle(limite, ct);
        }
    };
    var handler = new RegistrarPresencaHandler(db, NullLogger<RegistrarPresencaHandler>.Instance, current);
    var command = new RegistrarPresencaCommand(chamada.Id, aluno.Id, StatusPresenca.Falta);
    string? erro = null;
    try { await handler.Handle(command, default); } catch(Exception ex) { erro=ex.Message; }
    db.ChangeTracker.Clear();
    var alertas = await db.AlertasEvasao.ToListAsync();
    var registros = await db.RegistrosPresenca.CountAsync();
    if (!falhar) {
        var alerta=alertas.Single();
        var reproduzido=alerta.EscolaId==Guid.Empty && alerta.DataCriacao==default && alerta.UsuarioCriacao==null;
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {caso="P1 auditoria cascata", reproduzido, publicacoes, escolaVazia=alerta.EscolaId==Guid.Empty, dataCriacaoPadrao=alerta.DataCriacao==default, criadorAusente=alerta.UsuarioCriacao==null}));
        if(!reproduzido) Environment.ExitCode=1;
    } else {
        string? retryErro=null;
        try { await handler.Handle(command, default); } catch(Exception ex) { retryErro=ex.Message; }
        var reproduzido=erro!=null && registros==1 && alertas.Count==0 && retryErro!=null;
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {caso="P1 atomicidade", reproduzido, publicacoes, erro, registrosPersistidos=registros, alertasPersistidos=alertas.Count, retryErro}));
        if(!reproduzido) Environment.ExitCode=1;
    }
}

class MediatorVerificacao : IMediator
{
    public Func<object,CancellationToken,Task> AoPublicar {get;set;} = (_,_)=>Task.CompletedTask;
    public Task Publish(object notification,CancellationToken ct=default)=>AoPublicar(notification,ct);
    public Task Publish<T>(T notification,CancellationToken ct=default) where T:INotification=>AoPublicar(notification,ct);
    public Task<T> Send<T>(IRequest<T> request,CancellationToken ct=default)=>throw new NotSupportedException();
    public Task Send<T>(T request,CancellationToken ct=default) where T:IRequest=>throw new NotSupportedException();
    public Task<object?> Send(object request,CancellationToken ct=default)=>throw new NotSupportedException();
    public IAsyncEnumerable<T> CreateStream<T>(IStreamRequest<T> request,CancellationToken ct=default)=>throw new NotSupportedException();
    public IAsyncEnumerable<object?> CreateStream(object request,CancellationToken ct=default)=>throw new NotSupportedException();
}
