using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStoreAPI.Models
{
    public class Review
    {
        [Key]
        public int reviewID { get; set; }

        public int bookID { get; set; }   // FK → Books
        public int userID { get; set; }   // FK → Customers

        [Range(1, 5)]
        public int rating { get; set; }

        [MaxLength(1000)]
        public string? comment { get; set; }

        public DateTime createdAt { get; set; } = DateTime.UtcNow;
        public DateTime updatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Book Book { get; set; } = null!;
        public Customer Customer { get; set; } = null!;
    }
}