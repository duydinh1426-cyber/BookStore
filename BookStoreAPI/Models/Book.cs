using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStoreAPI.Models
{
    [Table("Books")]
    public class Book
    {
        [Key]
        public int bookID { get; set; }

        public int? categoryID { get; set; }

        [StringLength(255)]
        public string? author { get; set; }

        [Required]
        [StringLength(100)]
        public string title { get; set; } = null!;

        public int? publisherYear { get; set; }

        [StringLength(255)]
        public string? description { get; set; }

        [StringLength(100)]
        public string? image { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal price { get; set; }

        public int? numberPage { get; set; }

        [Required]
        public int numberSold { get; set; } = 0;

        [Required]
        public int numberStock { get; set; } = 0;

        [Required]
        [Column(TypeName = "decimal(3,2)")]
        public decimal avgRating { get; set; } = 0;

        [Required]
        public int reviewCount { get; set; } = 0;

        [Required]
        public DateTime createdAt { get; set; } = DateTime.UtcNow;

        [Required]
        public DateTime updatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Category? Category { get; set; }
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
        public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    }
}