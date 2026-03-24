using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStoreAPI.Models
{
    [Table("Reviews")]
    public class Review
    {
        [Key]
        public int reviewID { get; set; }

        [Required]
        public int bookID { get; set; }

        [Required]
        public int userID { get; set; }

        [Required]
        [Range(1, 5)]
        public int rating { get; set; }

        [StringLength(255)]
        public string? comment { get; set; }

        [Required]
        public DateTime createdAt { get; set; } = DateTime.UtcNow;

        [Required]
        public DateTime? updatedAt { get; set; }

        // Navigation
        public Book Book { get; set; } = null!;
        public Customer Customer { get; set; } = null!;
    }
}