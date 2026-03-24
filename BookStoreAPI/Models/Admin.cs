using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStoreAPI.Models
{
    [Table("Admin")]
    public class Admin
    {
        [Key]
        public int userID { get; set; }

        [Required]
        public int accountID { get; set; }

        [StringLength(100)]
        public string name { get; set; } = null!;

        [StringLength(255)]
        public string? address { get; set; }

        // Navigation
        public Account Account { get; set; } = null!;
    }
}