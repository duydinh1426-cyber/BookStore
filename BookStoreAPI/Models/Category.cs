using System.ComponentModel.DataAnnotations;

namespace BookStoreAPI.Models
{
    public class Category
    {
        [Key]
        public int categoryID { get; set; }
        public string categoryName { get; set; } = "";

        // Navigation
        public ICollection<Book>? Books { get; set; }
    }
}