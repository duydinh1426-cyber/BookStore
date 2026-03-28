namespace BookStoreAPI.DTOs
{
    public record AddCartDto(
        int BookId,
        int Quantity
    );

    public record UpdateCartDto(int Quantity);

    public record CartItemResponseDto(
        int CartItemId,
        int BookId,
        string Title,
        string Author,
        string? Image,
        decimal Price,
        int Quantity,
        decimal SubTotal   
    );

    public record CartResponseDto(
        IEnumerable<CartItemResponseDto> Items,
        decimal TotalPrice, 
        int TotalItems   
    );
}