namespace EscolaAtenta.Domain.Enums;

/// <summary>
/// Classifica o tipo de alerta gerado pelo sistema.
/// 
/// Decisão de design: Evita a criação de uma segunda entidade/tabela AlertaAtraso,
/// pois o ciclo de vida (criar, listar, resolver) é idêntico ao de evasão.
/// 
/// Compatibilidade: Evasao = 1 é o valor default, garantindo que todos os registros
/// pré-existentes no banco (sem a coluna Tipo) sejam interpretados corretamente.
/// </summary>
public enum TipoAlerta
{
    Evasao = 1, // Default — faltas consecutivas excessivas
    Atraso = 2  // Atrasos acumulados no trimestre
}
