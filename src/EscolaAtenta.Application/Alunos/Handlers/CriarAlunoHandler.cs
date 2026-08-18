using EscolaAtenta.Application.Alunos.Commands;
using EscolaAtenta.Application.Alunos.DTOs;
using EscolaAtenta.Domain.Entities;
using EscolaAtenta.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EscolaAtenta.Application.Alunos.Handlers;

public class CriarAlunoHandler : IRequestHandler<CriarAlunoCommand, AlunoDto>
{
    private readonly AppDbContext _context;

    public CriarAlunoHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AlunoDto> Handle(CriarAlunoCommand request, CancellationToken cancellationToken)
    {
        // Verifica se a Turma existe
        var turma = await _context.Turmas.FirstOrDefaultAsync(t => t.Id == request.TurmaId, cancellationToken);
        if (turma == null)
            throw new ArgumentException("A Turma informada não existe.");

        var aluno = new Aluno(
            id: Guid.NewGuid(),
            nome: request.Nome,
            matricula: request.Matricula,
            turmaId: request.TurmaId
        );

        // Registra o vínculo inicial no histórico de matrículas
        aluno.Matricular(turma.Id, turma.AnoLetivo, DateTime.UtcNow, "Matrícula inicial");

        _context.Alunos.Add(aluno);
        await _context.SaveChangesAsync(cancellationToken);

        return new AlunoDto(
            aluno.Id, 
            aluno.Nome, 
            aluno.Matricula, 
            aluno.TurmaId, 
            aluno.FaltasConsecutivasAtuais, 
            aluno.FaltasNoTrimestre,
            aluno.TotalFaltas,
            aluno.AtrasosNoTrimestre);
    }
}
