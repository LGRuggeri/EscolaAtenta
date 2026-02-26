namespace EscolaAtenta.Application.Alunos.DTOs;

public record AlunoDto(
    Guid Id, 
    string Nome, 
    string Matricula, 
    Guid TurmaId, 
    int FaltasConsecutivasAtuais, 
    int TotalFaltas);
