namespace BookStoreAPI.DTOs
{
    public record CreateReviewDto(
        int BookId,
        int Rating,     // 1-5
        string? Comment
    );

    public record ReviewResponseDto(
        int ReviewId,
        string CustomerName,
        int Rating,
        string? Comment,
        DateTime CreatedAt
    );

    public record UpdateReviewDto(
        int Rating,
        string? Comment
    );
}
