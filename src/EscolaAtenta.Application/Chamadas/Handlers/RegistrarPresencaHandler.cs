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
/// Fluxo:
/// 1. Carrega a Chamada com seus RegistrosPresenca (para validação de duplicidade).
/// 2. Carrega o Aluno para verificação de limite de faltas.
/// 3. Delega o registro ao método de negócio Chamada.RegistrarPresenca().
/// 4. Se o status for Falta, conta o total de faltas e verifica o limite via Aluno.
/// 5. Persiste via SaveChangesAsync — que automaticamente:
///    a. Preenche campos de auditoria.
///    b. Despacha Domain Events (LimiteFaltasAtingidoEvent se aplicável).
/// 
/// Decisão sobre carregamento: Carregamos apenas os dados necessários para
/// a operação, evitando over-fetching. A coleção RegistrosPresenca é carregada
/// para validação de duplicidade no domínio.
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

        // ── Verificação de limite de faltas (apenas para Falta e Justificada) ──
        var alertaGerado = false;

        if (request.Status == StatusPresenca.Falta)
        {
            var aluno = await _context.Alunos
                .FirstOrDefaultAsync(a => a.Id == request.AlunoId, cancellationToken)
                ?? throw new DomainException($"Aluno '{request.AlunoId}' não encontrado.");

            // Conta faltas existentes + a falta atual que ainda não foi salva
            var totalFaltas = await _context.RegistrosPresenca
                .CountAsync(rp =>
                    rp.AlunoId == request.AlunoId &&
                    rp.Status == StatusPresenca.Falta &&
                    rp.Chamada.TurmaId == chamada.TurmaId,
                    cancellationToken);

            // +1 pela falta atual que está sendo registrada agora
            totalFaltas++;

            var limite = _configuration.GetValue<int>("RegrasNegocio:LimiteFaltasParaAlerta", 5);

            // O método VerificarLimiteFaltas dispara o Domain Event se necessário
            aluno.VerificarLimiteFaltas(totalFaltas, limite);

            // Verifica se um evento foi adicionado (indica que alerta será gerado)
            alertaGerado = aluno.DomainEvents.Any();
        }

        // ── Persiste — auditoria e Domain Events são tratados no SaveChangesAsync
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Presença registrada: Chamada={ChamadaId}, Aluno={AlunoId}, Status={Status}, AlertaGerado={AlertaGerado}",
            request.ChamadaId, request.AlunoId, request.Status, alertaGerado);

        return new RegistrarPresencaResult(registro.Id, alertaGerado);
    }
}
