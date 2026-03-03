namespace EscolaAtenta.Application.Common;

/// <summary>
/// Envelope de paginação genérico para qualquer consulta paginada.
///
/// Contrato com o Front-end:
/// - Items: página atual de dados
/// - TotalCount: total de registros no banco (para cálculo de hasNextPage)
/// - PageNumber: página atual (1-indexed)
/// - PageSize: tamanho de página solicitado
/// - TotalPages: calculado automaticamente (Math.Ceiling)
/// - HasNextPage / HasPreviousPage: conveniência para o cliente decidir
///   se deve ou não disparar nova requisição de scroll infinito.
///
/// Uso: PagedResult{T}.Create(items, totalCount, pageNumber, pageSize)
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => PageNumber < TotalPages;
    public bool HasPreviousPage => PageNumber > 1;

    public static PagedResult<T> Create(IReadOnlyList<T> items, int totalCount, int pageNumber, int pageSize)
        => new(items, totalCount, pageNumber, pageSize);

    /// <summary>Retorna página vazia com metadados preservados (para respostas 200 sem dados).</summary>
    public static PagedResult<T> Empty(int pageNumber, int pageSize)
        => new(Array.Empty<T>(), 0, pageNumber, pageSize);
}
