using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStoreAPI.Models
{
    [Table("Orders")]
    public class Order
    {
        [Key]
        public int orderID { get; set; }

        [Required]
        public int userID { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal totalCost { get; set; }

        [StringLength(255)]
        public string? note { get; set; }

        [StringLength(20)]
        public string? phone { get; set; }

        [StringLength(255)]
        public string? address { get; set; }

        [Required]
        [StringLength(50)]
        public string status { get; set; } = "pending";

        [Required]
        public DateTime createdAt { get; set; } = DateTime.UtcNow;

        [Required]
        public DateTime updatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Customer Customer { get; set; } = null!;
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}