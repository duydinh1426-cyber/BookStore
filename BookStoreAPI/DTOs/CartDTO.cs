namespace BookStoreAPI.DTOs
{
    // ── INPUT ─────────────────────────────────────────────────

    public record AddCartDto(
        int BookId,
        int Quantity
    );

    public record UpdateCartDto(int Quantity);

    // ── OUTPUT ────────────────────────────────────────────────

    public record CartItemResponseDto(
        int CartItemId,
        int BookId,
        string Title,
        string Author,
        string? Image,
        decimal Price,
        int Quantity,
        decimal SubTotal    // Price × Quantity
    );

    public record CartResponseDto(
        IEnumerable<CartItemResponseDto> Items,
        decimal TotalPrice,  // tổng toàn bộ giỏ
        int TotalItems   // tổng số lượng sách
    );
}