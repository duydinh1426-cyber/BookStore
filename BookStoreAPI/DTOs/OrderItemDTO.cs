using Microsoft.AspNetCore.Mvc;

namespace BookStoreAPI.DTOs
{
    public record OrderItemDto(
        int OrderItemId,
        int BookId,
        string BookTitle,
        string? BookImage,
        int Quantity,
        decimal UnitPrice,
        decimal SubTotal
    );

    public record OrderItemSummaryDto(
        int OrderId,
        decimal TotalCost,
        string Status,
        string Phone,
        string Address,
        string? Note,
        DateTime CreatedAt,
        int ItemCount
    );
}
