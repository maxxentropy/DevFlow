namespace DevFlow.Application.Common;

/// <summary>
/// Represents a paginated result set.
/// </summary>
/// <typeparam name="T">The item type</typeparam>
public sealed record PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public required int TotalCount { get; init; }
    public required int PageNumber { get; init; }
    public required int PageSize { get; init; }
    
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}