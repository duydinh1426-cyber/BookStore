using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStoreAPI.Models
{
    [Table("Admin")]
    public class Admin
    {
        [Key]
        public int userID { get; set; }
        public int accountID { get; set; }   // FK → Accounts
        public string name { get; set; } = "";
        public string? address { get; set; }

        // Navigation
        public Account Account { get; set; } = null!;
    }
}