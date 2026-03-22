namespace BookStoreAPI.DTOs
{
    public record CheckoutDto(
        string Phone,
        string Address,
        string? Note
    );

    public record UpdateOrderStatusDto(string Status);

    public record OrderItemResponseDto(
        int OrderItemId,
        int BookId,
        string Title,
        string Author,
        string? Image,
        decimal UnitPrice,
        int Quantity,
        decimal SubTotal
    );

    public record OrderResponseDto(
        int OrderId,
        string Status,
        string Phone,
        string Address,
        string? Note,
        decimal TotalCost,
        DateTime CreatedAt,
        IEnumerable<OrderItemResponseDto> Items
    );

    public record OrderSummaryDto(
        int OrderId,
        string Status,
        string StatusLabel,
        decimal TotalCost,
        int TotalItems,
        DateTime CreatedAt
    );
}
