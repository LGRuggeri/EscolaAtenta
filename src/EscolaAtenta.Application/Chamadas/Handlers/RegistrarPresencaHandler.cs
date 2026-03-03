using EscolaAtenta.Application.Chamadas.Commands;
using EscolaAtenta.Domain.Enums;
using EscolaAtenta.Domain.Exceptions;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EscolaAtenta.Application.Chamadas.Handlers;

/// <summary>
/// Handler para o comando RegistrarPresencaCommand.
/// 
/// Fluxo (Atualizado para novo modelo de negócio):
/// 1. Carrega a Chamada com seus RegistrosPresenca (para validação de duplicidade).
/// 2. Carrega o Aluno para atualizar contadores de falta.
/// 3. Delega o registro ao método de negócio Chamada.RegistrarPresenca().
/// 4. Chama o método Aluno.RegistrarPresenca() para atualizar contadores:
///    - Se Presente: Zera FaltasConsecutivasAtuais
///    - Se Falta: Incrementa FaltasConsecutivasAtuais E TotalFaltas.
///      O Domínio chama VerificarLimiteFaltas() automaticamente.
///    - Se FaltaJustificada: Zera FaltasConsecutivasAtuais mas conta no TotalFaltas
///    - Se Atraso: Incrementa AtrasosNoTrimestre.
///      O Domínio chama VerificarLimiteAtrasos() automaticamente.
/// 5. Persiste via SaveChangesAsync — que automaticamente:
///    a. Preenche campos de auditoria.
///    b. Despacha Domain Events (LimiteFaltasAtingidoEvent ou LimiteAtrasosAtingidoEvent).
/// </summary>
public class RegistrarPresencaHandler : IRequestHandler<RegistrarPresencaCommand, RegistrarPresencaResult>
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RegistrarPresencaHandler> _logger;

    public RegistrarPresencaHandler(
        AppDbContext context,
        IConfiguration configuration,
        ILogger<RegistrarPresencaHandler> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<RegistrarPresencaResult> Handle(
        RegistrarPresencaCommand request,
        CancellationToken cancellationToken)
    {
        // ── Carrega a Chamada com registros existentes ─────────────────────────
        // Include necessário para que Chamada.RegistrarPresenca() possa validar
        // duplicidade sem round-trip adicional ao banco
        var chamada = await _context.Chamadas
            .Include(c => c.RegistrosPresenca)
            .FirstOrDefaultAsync(c => c.Id == request.ChamadaId, cancellationToken)
            ?? throw new DomainException($"Chamada '{request.ChamadaId}' não encontrada.");

        // ── Delega ao domínio — invariantes são verificadas aqui ───────────────
        var registro = chamada.RegistrarPresenca(request.AlunoId, request.Status);

        // ── Carrega o Aluno para atualizar contadores de falta ────────────────
        var aluno = await _context.Alunos
            .FirstOrDefaultAsync(a => a.Id == request.AlunoId, cancellationToken)
            ?? throw new DomainException($"Aluno '{request.AlunoId}' não encontrado.");

        // ── Atualiza contadores de falta na entidade Aluno ────────────────────
        // RegistrarPresenca() delega internamente para RegistrarFalta(), RegistrarAtraso() etc.
        // Cada um desses métodos chama VerificarLimiteFaltas() ou VerificarLimiteAtrasos()
        // automaticamente. O Domínio é auto-suficiente — nenhuma checagem extra é necessária aqui.
        aluno.RegistrarPresenca(request.Status, chamada.DataHora.UtcDateTime);

        // Verifica se um evento de alerta foi adicionado (indica que alerta será gerado)
        var alertaGerado = aluno.DomainEvents.Any();

        // ── Persiste — auditoria e Domain Events são tratados no SaveChangesAsync
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Presença registrada: Chamada={ChamadaId}, Aluno={AlunoId}, Status={Status}, FaltasConsecutivas={FaltasConsecutivas}, TotalFaltas={TotalFaltas}, AlertaGerado={AlertaGerado}",
            request.ChamadaId, 
            request.AlunoId, 
            request.Status, 
            aluno.FaltasConsecutivasAtuais,
            aluno.TotalFaltas,
            alertaGerado);

        return new RegistrarPresencaResult(registro.Id, alertaGerado);
    }
}
