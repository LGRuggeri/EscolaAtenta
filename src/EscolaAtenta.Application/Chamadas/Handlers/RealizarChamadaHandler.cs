using EscolaAtenta.Application.Chamadas.Commands;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Domain.Exceptions;
using EscolaAtenta.Domain.Interfaces;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EscolaAtenta.Application.Chamadas.Handlers;

public class RealizarChamadaHandler : IRequestHandler<RealizarChamadaCommand, RealizarChamadaResult>
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<RealizarChamadaHandler> _logger;

    public RealizarChamadaHandler(
        AppDbContext context,
        ICurrentUserService currentUser,
        ILogger<RealizarChamadaHandler> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<RealizarChamadaResult> Handle(RealizarChamadaCommand request, CancellationToken cancellationToken)
    {
        // 1. Verifica se a Turma existe
        var turmaExiste = await _context.Turmas.AnyAsync(t => t.Id == request.TurmaId, cancellationToken);
        if (!turmaExiste)
            throw new DomainException($"A turma informada '{request.TurmaId}' não existe.");

        // TODO: [IDOR] Quando existir a tabela UsuarioTurma, adicionar validação de ownership:
        // if (!await _context.UsuarioTurmas.AnyAsync(ut => ut.TurmaId == request.TurmaId && ut.UsuarioId == Guid.Parse(_currentUser.UsuarioId)))
        //     throw new DomainException("Você não tem permissão para realizar chamada nesta turma.");

        // SEGURANÇA: Usa o UsuarioId do token JWT como responsável da chamada
        // Em vez de confiar cegamente no ResponsavelId enviado pelo cliente (vetor de spoofing).
        // Se o usuário está autenticado e o UsuarioId é um Guid válido, usa-o.
        var responsavelIdSeguro = _currentUser.EstaAutenticado
            && Guid.TryParse(_currentUser.UsuarioId, out var parsedUserId)
            ? parsedUserId
            : request.ResponsavelId;

        // 2. Cria a nova Chamada
        var chamada = new Chamada(
            id: Guid.NewGuid(),
            dataHora: DateTimeOffset.UtcNow,
            turmaId: request.TurmaId,
            responsavelId: responsavelIdSeguro
        );

        _context.Chamadas.Add(chamada);

        // 3. Busca todos os alunos da lista para atualizar
        var alunosIds = request.Alunos.Select(a => a.AlunoId).ToList();
        var alunosDb = await _context.Alunos
            .Where(a => alunosIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        int alertasGerados = 0;

        // 4. Mapeia as presenças e computa status na Entidade chamada e Aluno
        foreach (var registroDto in request.Alunos)
        {
            if (!alunosDb.TryGetValue(registroDto.AlunoId, out var aluno))
            {
                _logger.LogWarning("Tentativa de registrar presença para aluno inexistente: {AlunoId}", registroDto.AlunoId);
                continue;
            }

            // Atribui registro à Entidade Chamada
            chamada.RegistrarPresenca(aluno.Id, registroDto.Status);

            // Atualiza contadores na Entidade Aluno.
            // O método RegistrarPresenca() delega para RegistrarFalta(), RegistrarAtraso() etc.,
            // que internamente chamam VerificarLimiteFaltas() e VerificarLimiteAtrasos().
            // O Domínio é auto-suficiente — não é preciso chamar VerificarLimiteFaltas() aqui.
            aluno.RegistrarPresenca(registroDto.Status, chamada.DataHora.UtcDateTime);

            if (aluno.DomainEvents.Count > 0)
            {
                alertasGerados++;
            }
        }

        // 5. Salva Tudo Atomicamente
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[AUDITORIA] Chamada realizada — TurmaId={TurmaId} Responsavel={ResponsavelId} TotalAlunos={Total} AlertasGerados={Alertas}",
            request.TurmaId, responsavelIdSeguro, request.Alunos.Count, alertasGerados);

        return new RealizarChamadaResult(chamada.Id, alertasGerados);
    }
}
