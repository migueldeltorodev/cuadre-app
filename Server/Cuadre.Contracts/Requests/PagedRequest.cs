namespace Cuadre.Contracts.Requests;

public class PagedRequest
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 25;

    public int? Page { get; init; } = DefaultPage;
    public int? PageSize { get; init; } = DefaultPageSize;
}
