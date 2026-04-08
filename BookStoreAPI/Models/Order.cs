using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStoreAPI.Models
{
    [Table("Orders")]
    public class Order
    {
        [Key]
        public int orderID { get; set; }

        [Required]
        public int userID { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal totalCost { get; set; }

        [StringLength(255)]
        public string? note { get; set; }

        [StringLength(20)]
        public string? phone { get; set; }

        [StringLength(255)]
        public string? address { get; set; }

        // ── Order lifecycle status ──────────────────────────────
        // pending → confirmed → shipping → completed
        //    ↓          ↓           ↓
        // cancelled  cancelled  cancelled
        [Required]
        [StringLength(50)]
        public string status { get; set; } = "pending";

        // ── Payment fields (mới thêm) ───────────────────────────
        /// <summary>vnpay | cod (mở rộng sau)</summary>
        [StringLength(20)]
        public string paymentMethod { get; set; } = "cod";

        /// <summary>unpaid | paid | failed | refunded</summary>
        [StringLength(20)]
        public string paymentStatus { get; set; } = "unpaid";

        /// <summary>Mã giao dịch VNPay (vnp_TxnRef)</summary>
        [StringLength(100)]
        public string? vnpayTxnRef { get; set; }

        /// <summary>Thời điểm thanh toán thành công</summary>
        public DateTime? paidAt { get; set; }

        // ── Timestamps ──────────────────────────────────────────
        [Required]
        public DateTime createdAt { get; set; } = DateTime.UtcNow;

        [Required]
        public DateTime updatedAt { get; set; } = DateTime.UtcNow;

        // ── Navigation ──────────────────────────────────────────
        public Customer Customer { get; set; } = null!;
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}