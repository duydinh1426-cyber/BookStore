using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStoreAPI.Models
{
    [Table("Categories")]
    public class Category
    {
        [Key]
        public int categoryID { get; set; }

        [Required]
        [StringLength(100)]
        public string categoryName { get; set; } = null!;

        // Navigation
        public ICollection<Book> Books { get; set; } = new List<Book>();
    }
}