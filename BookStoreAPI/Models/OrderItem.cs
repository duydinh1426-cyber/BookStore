using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStoreAPI.Models
{
    [Table("OrderItems")]
    public class OrderItem
    {
        [Key]
        public int orderItemID { get; set; }

        [Required]
        public int orderID { get; set; } 

        [Required]
        public int bookID { get; set; }

        [Required]
        public int quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal unitPrice { get; set; } = 0;

        [Required]
        public DateTime createdAt { get; set; } = DateTime.UtcNow;

        [Required]
        public DateTime updatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Order Order { get; set; } = null!;
        public Book Book { get; set; } = null!;
    }
}