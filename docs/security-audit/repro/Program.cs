// Reproduções locais: somente SQLite em memória e servidor loopback. Não usa dados reais.
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using EscolaAtenta.API.Controllers;
using EscolaAtenta.Application.Auth;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Interfaces;
using EscolaAtenta.Infrastructure.Data;
using EscolaAtenta.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var resultados = new List<object>();
var chave = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));
var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?> {
    ["Jwt:SecretKey"] = chave, ["EscolaContext:Id"] = Guid.NewGuid().ToString()
});
await using var conexao = new SqliteConnection("Data Source=:memory:");
await conexao.OpenAsync();
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(conexao));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<IEscolaTenantProvider, EscolaTenantProvider>();
builder.Services.AddSingleton<IPeriodoLetivoProvider, PeriodoLetivoProvider>();
builder.Services.AddSingleton<ISqliteWriteLockProvider, SqliteWriteLockProvider>();
builder.Services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(LoginHandler).Assembly));
builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly).AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters {
    ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
    ValidIssuer = "EscolaAtenta", ValidAudience = "EscolaAtenta", IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chave)), ClockSkew = TimeSpan.FromSeconds(30)
});
builder.Services.AddAuthorization();
var app = builder.Build();
app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
var senha = Guid.NewGuid().ToString("N");
var hash = BCrypt.Net.BCrypt.HashPassword(senha);
var admin = new Usuario("Admin sintético", "admin@example.test", hash, PapelUsuario.Administrador);
admin.ExigirTrocaDeSenha();
var monitor = new Usuario("Monitor sintético", "monitor@example.test", hash, PapelUsuario.Monitor);
var supervisor = new Usuario("Supervisor sintético", "supervisor@example.test", hash, PapelUsuario.Supervisao);
var turmaA = new Turma(Guid.NewGuid(), "Turma autorizada", "Manhã", 2026);
var turmaB = new Turma(Guid.NewGuid(), "Turma alheia", "Manhã", 2026);
var alunoB = new Aluno(Guid.NewGuid(), "Aluno sintético alheio", "TESTE", turmaB.Id);
var alertaB = AlertaEvasao.CriarAlertaAluno(alunoB.Id, turmaB.Id, NivelAlertaFalta.Aviso, "Alerta sintético");
using (var scope = app.Services.CreateScope()) {
    var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await ctx.Database.EnsureCreatedAsync();
    ctx.AddRange(admin, monitor, supervisor, turmaA, turmaB, alunoB, alertaB);
    ctx.UsuarioTurmas.AddRange(new UsuarioTurma(Guid.NewGuid(), monitor.Id, turmaA.Id), new UsuarioTurma(Guid.NewGuid(), supervisor.Id, turmaA.Id));
    await ctx.SaveChangesAsync(); ctx.ChangeTracker.Clear();
}
await app.StartAsync();
var endereco = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
using var cliente = new HttpClient { BaseAddress = new Uri(endereco) };
void Registrar(string caso, bool confirmado, string detalhe) => resultados.Add(new {caso, confirmado, detalhe});
async Task<JsonElement> Login(string email) {
    var r = await cliente.PostAsJsonAsync("/api/v1/auth/login", new {email, senha});
    r.EnsureSuccessStatusCode(); return JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.Clone();
}
void Token(string token) => cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
var anonimo = await cliente.GetAsync("/api/v1/sync/pull");
Registrar("controle_sem_token", (int)anonimo.StatusCode == 401, $"HTTP {(int)anonimo.StatusCode}");
var lm = await Login(monitor.Email); var tm = lm.GetProperty("token").GetString()!; Token(tm);
var usuarios = await cliente.GetAsync("/api/v1/usuarios");
Registrar("controle_monitor_usuarios", (int)usuarios.StatusCode == 403, $"HTTP {(int)usuarios.StatusCode}");
var pull = await cliente.GetAsync("/api/v1/sync/pull?lastPulledAt=0");
Registrar("pull_expoe_aluno_alheio", pull.IsSuccessStatusCode && (await pull.Content.ReadAsStringAsync()).Contains(alunoB.Nome), $"HTTP {(int)pull.StatusCode}; aluno da turma B retornado a monitor vinculado apenas à A");
var dash = await cliente.GetAsync("/api/v1/dashboard/alunos-com-faltas");
Registrar("dashboard_expoe_aluno_alheio", dash.IsSuccessStatusCode && (await dash.Content.ReadAsStringAsync()).Contains(alunoB.Nome), $"HTTP {(int)dash.StatusCode}");
var turmas = await cliente.GetAsync("/api/v1/turmas");
Registrar("listagem_expoe_turma_alheia", turmas.IsSuccessStatusCode && (await turmas.Content.ReadAsStringAsync()).Contains(turmaB.Id.ToString()), $"HTTP {(int)turmas.StatusCode}");
var lerAlunos = await cliente.GetAsync($"/api/v1/alunos/turma/{turmaB.Id}");
Registrar("controle_lista_alunos_bloqueada", !lerAlunos.IsSuccessStatusCode, $"HTTP {(int)lerAlunos.StatusCode}; host isolado sem GlobalExceptionHandler");
var criarTurma = await cliente.PostAsJsonAsync("/api/v1/turmas", new {nome="Criada pelo monitor", turno="Manhã", anoLetivo=2026});
Registrar("monitor_cria_turma_online", (int)criarTurma.StatusCode == 201, $"HTTP {(int)criarTurma.StatusCode}");
var criarAluno = await cliente.PostAsJsonAsync("/api/v1/alunos", new {nome="Inserção indevida sintética", matricula="AUDIT", turmaId=turmaB.Id});
Registrar("monitor_cria_aluno_turma_alheia", (int)criarAluno.StatusCode == 201, $"HTTP {(int)criarAluno.StatusCode}");
var ls = await Login(supervisor.Email); Token(ls.GetProperty("token").GetString()!);
var resolver = await cliente.PatchAsJsonAsync($"/api/v1/alertas/{alertaB.Id}/resolver", new {justificativa="Reprodução local de autorização ausente"});
Registrar("supervisor_resolve_alerta_alheio", (int)resolver.StatusCode == 204, $"HTTP {(int)resolver.StatusCode}");
var auditoria = await cliente.GetAsync("/api/v1/alertas/auditoria");
Registrar("auditoria_sqlite_falha", (int)auditoria.StatusCode == 500, $"HTTP {(int)auditoria.StatusCode}; não contar como vazamento comprovado de detalhes");
var la = await Login(admin.Email); var ta = la.GetProperty("token").GetString()!; Token(ta);
var antesTroca = await cliente.GetAsync("/api/v1/usuarios");
Registrar("troca_obrigatoria_so_cliente", la.GetProperty("deveAlterarSenha").GetBoolean() && antesTroca.IsSuccessStatusCode, $"deveAlterarSenha=true; GET usuários HTTP {(int)antesTroca.StatusCode}");
var alterarSenha = await cliente.PutAsJsonAsync("/api/v1/auth/trocar-senha", new {novaSenha=Guid.NewGuid().ToString("N")});
var renovar = await cliente.PostAsJsonAsync("/api/v1/auth/refresh", new {refreshToken=la.GetProperty("refreshToken").GetString()});
Registrar("refresh_sobrevive_troca_senha", alterarSenha.IsSuccessStatusCode && renovar.IsSuccessStatusCode, $"Troca HTTP {(int)alterarSenha.StatusCode}; refresh anterior HTTP {(int)renovar.StatusCode}");
using (var scope = app.Services.CreateScope()) {
    var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var a = await ctx.Usuarios.SingleAsync(u => u.Id == admin.Id); a.AtualizarPerfil(a.Nome, PapelUsuario.Monitor);
    var m = await ctx.Usuarios.SingleAsync(u => u.Id == monitor.Id); m.Desativar(admin.Id.ToString());
    await ctx.SaveChangesAsync(); ctx.ChangeTracker.Clear();
}
Token(ta); var rebaixado = await cliente.GetAsync("/api/v1/usuarios");
Registrar("jwt_preserva_papel_revogado", rebaixado.IsSuccessStatusCode, $"Admin rebaixado no banco; token antigo HTTP {(int)rebaixado.StatusCode}");
Token(tm); var inativo = await cliente.GetAsync("/api/v1/sync/pull");
Registrar("jwt_usuario_desativado_aceito", inativo.IsSuccessStatusCode, $"Usuário inativo no banco; token antigo HTTP {(int)inativo.StatusCode}");
// Prova criptográfica do fallback Development, sem gravar ou imprimir chave/token.
var raiz = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.."));
var codigo = File.ReadAllText(Path.Combine(raiz, "src/EscolaAtenta.API/Program.cs"));
var match = System.Text.RegularExpressions.Regex.Match(codigo, "secretKey = \"([^\"]+)\";");
if (match.Success) {
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(match.Groups[1].Value));
    var forjado = new JwtSecurityToken("EscolaAtenta", "EscolaAtenta", new[]{new Claim("sub", Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, "Administrador")}, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(5), new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
    var val = new JwtSecurityTokenHandler().ValidateToken(new JwtSecurityTokenHandler().WriteToken(forjado), new TokenValidationParameters {ValidIssuer="EscolaAtenta", ValidAudience="EscolaAtenta", IssuerSigningKey=key, ValidateLifetime=true}, out _);
    Registrar("fallback_dev_assinatura_forjavel", val.IsInRole("Administrador"), "Validação criptográfica local; condicionada a Development com Jwt:SecretKey vazia; nenhum segredo exportado");
}
await app.StopAsync();
var destino = Path.Combine(raiz, "docs/security-audit/reproducoes.json");
await File.WriteAllTextAsync(destino, JsonSerializer.Serialize(resultados, new JsonSerializerOptions { WriteIndented=true, Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
Console.WriteLine(JsonSerializer.Serialize(resultados));
if (resultados.Any(r => !(bool)r.GetType().GetProperty("confirmado")!.GetValue(r)!)) Environment.ExitCode=1;
