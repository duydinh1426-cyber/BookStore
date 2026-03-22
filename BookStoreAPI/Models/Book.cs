using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStoreAPI.Models
{
    public class Book
    {
        [Key]
        public int bookID { get; set; }

        public int? categoryID { get; set; }   // FK → Categories

        public string author { get; set; } = "";
        public string title { get; set; } = "";

        public int? publisherYear { get; set; }

        public string? description { get; set; }
        public string? image { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal price { get; set; }

        public int? numberPage { get; set; }
        public int numberSold { get; set; } = 0;
        public int numberStock { get; set; } = 0;

        // ── Review summary (cập nhật tự động qua ReviewController) ──
        [Column(TypeName = "decimal(3,2)")]
        public decimal avgRating { get; set; } = 0;   // 0.00 – 5.00
        public int reviewCount { get; set; } = 0;

        public DateTime createdAt { get; set; } = DateTime.UtcNow;
        public DateTime updatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Category? Category { get; set; }
        public ICollection<OrderItem>? OrderItems { get; set; }
        public ICollection<Review>? Reviews { get; set; }
    }
}