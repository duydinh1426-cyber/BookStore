using System.ComponentModel.DataAnnotations;

namespace BookStoreAPI.Models
{
    public class OrderItem
    {
        [Key]
        public int orderItemID { get; set; }
        public int orderID { get; set; }   // FK → Orders
        public int bookID { get; set; }   // FK → Books
        public int quantity { get; set; }
        public decimal unitPrice { get; set; }   // ⭐ thêm: snapshot giá lúc đặt
        public DateTime createdAt { get; set; } = DateTime.UtcNow;
        public DateTime updatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Order Order { get; set; } = null!;
        public Book Book { get; set; } = null!;
    }
}