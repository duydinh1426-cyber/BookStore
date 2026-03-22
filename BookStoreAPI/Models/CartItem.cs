using System.ComponentModel.DataAnnotations;

namespace BookStoreAPI.Models
{
    public class CartItem
    {
        [Key]
        public int cartItemID { get; set; }       // INT
        public int userID { get; set; }       // INT (FK → Customers.userID)
        public int bookID { get; set; }       // INT (FK → Books.bookID)
        public int quantity { get; set; }       // INT
        public DateTime createdAt { get; set; } = DateTime.UtcNow;
        public DateTime updatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Customer Customer { get; set; } = null!;
        public Book Book { get; set; } = null!;
    }
}