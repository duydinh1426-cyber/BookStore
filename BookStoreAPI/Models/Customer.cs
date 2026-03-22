// Thông tin cá nhân của khách hàng
using System.ComponentModel.DataAnnotations;

namespace BookStoreAPI.Models
{
    public class Customer
    {
        [Key]
        public int userID { get; set; }
        public int accountID { get; set; }   // FK → Accounts
        public string name { get; set; } = "";
        public string? address { get; set; }

        // Navigation
        public Account Account { get; set; } = null!;
        public ICollection<Order>? Orders { get; set; }
        public ICollection<Review>? Reviews { get; set; }
    }
}