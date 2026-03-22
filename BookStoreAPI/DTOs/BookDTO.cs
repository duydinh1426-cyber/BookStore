namespace BookStoreAPI.DTOs
{
    public record BookSummaryDto(
        int BookId,
        string Title,
        string Author,
        decimal Price,
        string? Image,
        string? CategoryName,
        int NumberStock,
        int NumberSold
    );

    public record BookDetailDto(
        int BookId,
        string Title,
        string Author,
        decimal Price,
        string? Image,
        string? Description,
        int? PublisherYear,
        int? NumberPage,
        int NumberStock,
        int NumberSold,
        string? CategoryName,
        double AvgRating,
        int ReviewCount
    );

    /// <summary>Dùng cho cả POST (thêm mới) và PUT (cập nhật)</summary>
    public record BookUpsertDto(
        string Title,
        string? Author,
        decimal Price,
        int NumberStock,
        int? CategoryId,
        string? Description,
        string? Image,
        int? PublisherYear,
        int? NumberPage
    );
}