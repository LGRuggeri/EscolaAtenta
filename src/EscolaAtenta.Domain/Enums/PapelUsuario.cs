// Roles/Papeis do sistema - segue o principio de menor privilegio
// Administrador: acesso total ao sistema
// Professor: acesso a turmas e chamadas
// Coordenador: acesso a relatorios e gestao de turmas
namespace EscolaAtenta.Domain.Enums;

public enum PapelUsuario
{
    Professor = 1,
    Coordenador = 2,
    Administrador = 3
}
