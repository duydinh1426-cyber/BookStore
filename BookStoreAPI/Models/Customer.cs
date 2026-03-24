using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStoreAPI.Models
{
    [Table("Customers")]
    public class Customer
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
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
        public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    }
}