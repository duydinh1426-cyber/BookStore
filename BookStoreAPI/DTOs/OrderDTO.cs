namespace BookStoreAPI.DTOs
{
    public record CheckoutDto(
        string Phone,
        string Address,
        string? Note
    );

    public record UpdateOrderStatusDto(string Status);
}
