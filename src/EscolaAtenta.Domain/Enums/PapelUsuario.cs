// Roles/Papeis do sistema - Simplified RBAC
// 
// Novo Modelo de Negócio (Pivot):
// - Monitor: Operador que passa de sala em sala, lança chamadas no diário
// - Diretoria: Recebe alertas de evasão, visualiza relatórios e dashboard
// - Administrador: Gestão técnica do sistema
namespace EscolaAtenta.Domain.Enums;

public enum PapelUsuario
{
    /// <summary>
    /// Operador que passa de sala em sala, lança chamadas no diário.
    /// </summary>
    Monitor = 1,

    /// <summary>
    /// Recebe alertas de evasão, visualiza relatórios e dashboard.
    /// </summary>
    Diretoria = 2,

    /// <summary>
    /// Gestão técnica do sistema.
    /// </summary>
    Administrador = 3
}
