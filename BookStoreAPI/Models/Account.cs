using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStoreAPI.Models
{
    [Table("Accounts")]
    public class Account
    {
        [Key]
        public int accountID { get; set; }

        [Required]
        [StringLength(200)]
        public string username { get; set; } = null!;

        [Required]
        [StringLength(255)]
        public string password { get; set; } = null!; // chuyển sang hash không dùng lưu trực tiếp

        [StringLength(200)]
        public string? email { get; set; }

        [Required]
        public bool isAdmin { get; set; } = false;

        [Required]
        public DateTime createdAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Customer? Customer { get; set; }
        public Admin? Admin { get; set; }
    }
}