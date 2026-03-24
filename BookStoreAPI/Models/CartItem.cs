using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStoreAPI.Models
{
    [Table("CartItems")]
    public class CartItem
    {
        [Key]
        public int cartItemID { get; set; }

        [Required]
        public int userID { get; set; }

        [Required]
        public int bookID { get; set; } 

        [Required]
        public int quantity { get; set; }

        [Required]
        public DateTime createdAt { get; set; } = DateTime.UtcNow;

        [Required]
        public DateTime updatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Customer Customer { get; set; } = null!;
        public Book Book { get; set; } = null!;
    }
}