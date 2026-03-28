namespace BookStoreAPI.DTOs
{
    public record CreateReviewDto(
        int BookId,
        int Rating,    
        string? Comment
    );

    public record ReviewResponseDto(
        int ReviewId,
        string CustomerName,
        int Rating,
        string? Comment,
        DateTime CreatedAt
    );
}
