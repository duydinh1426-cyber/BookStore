using System.ComponentModel.DataAnnotations;

namespace BookStoreAPI.Models
{
    public class Order
    {
        [Key]
        public int orderID { get; set; }
        public int userID { get; set; }   // FK → Customers.userID
        public decimal totalCost { get; set; }
        public string? note { get; set; }
        public string phone { get; set; } = "";
        public string address { get; set; } = "";
        public string status { get; set; } = "pending"; // thêm status để track đơn hàng
        public DateTime createdAt { get; set; } = DateTime.UtcNow;
        public DateTime updatedAt { get; set; } = DateTime.UtcNow;

        // Navigation — trỏ đúng tới Customer, không phải Account
        public Customer Customer { get; set; } = null!;
        public ICollection<OrderItem>? OrderItems { get; set; }
    }
}