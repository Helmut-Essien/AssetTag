namespace Portal.ViewModels;

public class PaginationViewModel
{
    public int CurrentPage { get; init; }
    public int TotalPages { get; init; }
    public int TotalCount { get; init; }
    public int PageSize { get; init; }
    public bool HasPrevious { get; init; }
    public bool HasNext { get; init; }
    public Func<int, string> PageUrl { get; init; } = _ => "#";
    public string AriaLabel { get; init; } = "Pagination";
    public string? CssClass { get; init; }
    public bool Small { get; init; } = true;
    public bool ShowSummary { get; init; } = true;

    public static PaginationViewModel From(
        int currentPage,
        int totalPages,
        int totalCount,
        int pageSize,
        bool hasPrevious,
        bool hasNext,
        Func<int, string> pageUrl,
        string ariaLabel = "Pagination",
        bool small = true,
        bool showSummary = true,
        string? cssClass = null)
    {
        return new PaginationViewModel
        {
            CurrentPage = currentPage,
            TotalPages = totalPages,
            TotalCount = totalCount,
            PageSize = pageSize,
            HasPrevious = hasPrevious,
            HasNext = hasNext,
            PageUrl = pageUrl,
            AriaLabel = ariaLabel,
            Small = small,
            ShowSummary = showSummary,
            CssClass = cssClass
        };
    }

    public static PaginationViewModel FromPaginated<T>(
        Shared.DTOs.PaginatedResponse<T> response,
        Func<int, string> pageUrl,
        string ariaLabel = "Pagination",
        bool small = true,
        bool showSummary = true,
        string? cssClass = null)
    {
        return From(
            response.Page,
            response.TotalPages,
            response.TotalCount,
            response.PageSize,
            response.HasPrevious,
            response.HasNext,
            pageUrl,
            ariaLabel,
            small,
            showSummary,
            cssClass);
    }
}
