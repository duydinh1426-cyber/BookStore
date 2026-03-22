using System.ComponentModel.DataAnnotations;

namespace BookStoreAPI.Models
{
    public class Account
    {
        [Key]
        public int accountID { get; set; }
        public string username { get; set; } = "";
        public string password { get; set; } = ""; // lưu hash, không lưu plain text
        public string email { get; set; } = "";
        public bool isAdmin { get; set; } = false;
        public DateTime createdAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Customer? Customer { get; set; }
        public Admin? Admin { get; set; }
    }
}