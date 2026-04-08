using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using BookStoreAPI.Data;
using BookStoreAPI.Models;
using BookStoreAPI.Services;
using BookStoreAPI.DTOs;

namespace BookStoreAPI.Controllers
{
    [ApiController]
    [Route("api/payment")]
    public class PaymentController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly VNPayService _vnpay;
        private readonly ILogger<PaymentController> _log;

        public PaymentController(AppDbContext db, VNPayService vnpay,
                                 ILogger<PaymentController> log)
        {
            _db = db;
            _vnpay = vnpay;
            _log = log;
        }

        private int GetUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub") ?? "0");

        // ══════════════════════════════════════════════════════════════
        //  POST /api/payment/checkout-vnpay          [Customer]
        //
        //  Tạo Order (pending) → tạo URL VNPay → trả về cho frontend
        //  redirect người dùng sang cổng thanh toán.
        //
        //  Flow:
        //   1. Validate giỏ hàng
        //   2. Tạo Order với status = "pending" + paymentMethod = "vnpay"
        //   3. Trừ tồn kho tạm (lock stock) — sẽ hoàn lại nếu cancel
        //   4. Trả về paymentUrl để frontend redirect
        // ══════════════════════════════════════════════════════════════
        [HttpPost("checkout-vnpay")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> CheckoutVNPay([FromBody] CheckoutDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Phone))
                return BadRequest(new { message = "Số điện thoại không được để trống" });
            if (string.IsNullOrWhiteSpace(dto.Address))
                return BadRequest(new { message = "Địa chỉ giao hàng không được để trống" });

            var userId = GetUserId();

            var cartItems = await _db.CartItems
                .Include(c => c.Book)
                .Where(c => c.userID == userId)
                .ToListAsync();

            if (!cartItems.Any())
                return BadRequest(new { message = "Giỏ hàng trống" });

            // Kiểm tra tồn kho
            var stockErrors = cartItems
                .Where(i => i.Book.numberStock < i.quantity)
                .Select(i => $"Sách '{i.Book.title}' chỉ còn {i.Book.numberStock} cuốn")
                .ToList();

            if (stockErrors.Any())
                return BadRequest(new { message = "Không đủ hàng", errors = stockErrors });

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // Tạo đơn hàng ở trạng thái pending — chờ VNPay xác nhận
                var order = new Order
                {
                    userID = userId,
                    phone = dto.Phone.Trim(),
                    address = dto.Address.Trim(),
                    note = dto.Note?.Trim(),
                    status = "pending",
                    paymentMethod = "vnpay",
                    paymentStatus = "unpaid",
                    createdAt = DateTime.UtcNow,
                    updatedAt = DateTime.UtcNow
                };

                decimal total = 0;
                var orderItems = new List<OrderItem>();

                foreach (var item in cartItems)
                {
                    total += item.Book.price * item.quantity;
                    orderItems.Add(new OrderItem
                    {
                        bookID = item.bookID,
                        quantity = item.quantity,
                        unitPrice = item.Book.price,
                        createdAt = DateTime.UtcNow,
                        updatedAt = DateTime.UtcNow
                    });

                    // Lock tồn kho ngay — tránh oversell
                    item.Book.numberStock -= item.quantity;
                    item.Book.numberSold += item.quantity;
                }

                order.totalCost = total;
                order.OrderItems = orderItems;

                _db.Orders.Add(order);
                _db.CartItems.RemoveRange(cartItems);
                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                // Tạo URL thanh toán VNPay
                var payUrl = _vnpay.CreatePaymentUrl(
                    order.orderID,
                    total,
                    $"Thanh toan don hang #{order.orderID}");

                return Ok(new
                {
                    message = "Đơn hàng tạo thành công, vui lòng thanh toán",
                    orderId = order.orderID,
                    totalCost = total,
                    paymentUrl = payUrl          // Frontend redirect đến đây
                });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _log.LogError(ex, "Lỗi tạo đơn hàng VNPay");
                return StatusCode(500, new { message = "Lỗi tạo đơn hàng", detail = ex.Message });
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  GET /api/payment/vnpay-return             [Public — VNPay redirect]
        //
        //  VNPay redirect browser người dùng về đây sau khi thanh toán.
        //  Endpoint này XÁC THỰC chữ ký, cập nhật trạng thái đơn hàng
        //  rồi redirect tiếp về trang kết quả của frontend.
        //
        //  Lưu ý: KHÔNG dùng endpoint này để cập nhật nghiệp vụ CHÍNH —
        //  dùng IPN (/vnpay-ipn) cho đó vì return URL có thể bị user bỏ qua.
        // ══════════════════════════════════════════════════════════════
        [HttpGet("vnpay-return")]
        public async Task<IActionResult> VNPayReturn()
        {
            var (isValid, orderId, rspCode, txnRef) =
                _vnpay.ValidateCallback(Request.Query);

            var frontendBase = _db.Database
                .GetDbConnection().ConnectionString.Contains("localhost")
                ? "http://localhost:3000"
                : "https://yourdomain.com";  // ← thay bằng URL frontend thực tế

            if (!isValid)
            {
                _log.LogWarning("VNPay return — chữ ký không hợp lệ. TxnRef={txnRef}", txnRef);
                return Redirect($"{frontendBase}/payment/result?status=invalid");
            }

            // Dù đã có IPN xử lý rồi, vẫn gọi lại để đảm bảo nhất quán
            await ProcessPaymentResult(orderId, rspCode, txnRef, source: "return");

            var success = rspCode == "00";
            return Redirect($"{frontendBase}/payment/result?status={(success ? "success" : "failed")}&orderId={orderId}");
        }

        // ══════════════════════════════════════════════════════════════
        //  GET /api/payment/vnpay-ipn                [Public — VNPay server-to-server]
        //
        //  VNPay server gọi trực tiếp vào endpoint này (không qua browser).
        //  Đây là nơi CẬP NHẬT CHÍNH của hệ thống.
        //  Phải trả về { RspCode, Message } theo chuẩn VNPay.
        // ══════════════════════════════════════════════════════════════
        [HttpGet("vnpay-ipn")]
        public async Task<IActionResult> VNPayIPN()
        {
            try
            {
                var (isValid, orderId, rspCode, txnRef) =
                    _vnpay.ValidateCallback(Request.Query);

                if (!isValid)
                {
                    _log.LogWarning("VNPay IPN — chữ ký không hợp lệ. TxnRef={txnRef}", txnRef);
                    return Ok(new { RspCode = "97", Message = "Invalid signature" });
                }

                var order = await _db.Orders.FindAsync(orderId);
                if (order is null)
                    return Ok(new { RspCode = "01", Message = "Order not found" });

                // Idempotent — nếu đã xử lý rồi thì bỏ qua
                if (order.paymentStatus != "unpaid")
                    return Ok(new { RspCode = "02", Message = "Already processed" });

                // Kiểm tra số tiền (VNPay gửi * 100)
                var vnpAmount = long.Parse(Request.Query["vnp_Amount"].ToString());
                var expectedAmount = (long)(order.totalCost * 100);
                if (vnpAmount != expectedAmount)
                {
                    _log.LogError(
                        "VNPay IPN — số tiền không khớp. Order={orderId} Expected={exp} Got={got}",
                        orderId, expectedAmount, vnpAmount);
                    return Ok(new { RspCode = "04", Message = "Invalid amount" });
                }

                await ProcessPaymentResult(orderId, rspCode, txnRef, source: "ipn");

                return Ok(new { RspCode = "00", Message = "Confirm success" });
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "VNPay IPN exception");
                return Ok(new { RspCode = "99", Message = "Unknown error" });
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  GET /api/payment/status/{orderId}         [Customer | Admin]
        //
        //  Kiểm tra trạng thái thanh toán của đơn hàng (polling).
        //  Frontend dùng để poll sau khi redirect về từ VNPay.
        // ══════════════════════════════════════════════════════════════
        [HttpGet("status/{orderId:int}")]
        [Authorize]
        public async Task<IActionResult> GetPaymentStatus(int orderId)
        {
            var userId = GetUserId();
            var isAdmin = User.IsInRole("Admin");

            var order = await _db.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.orderID == orderId);

            if (order is null) return NotFound(new { message = "Không tìm thấy đơn hàng" });
            if (!isAdmin && order.userID != userId) return Forbid();

            return Ok(new
            {
                orderId = order.orderID,
                status = order.status,
                paymentStatus = order.paymentStatus,
                paymentMethod = order.paymentMethod,
                totalCost = order.totalCost,
                updatedAt = order.updatedAt
            });
        }

        // ──────────────────────────────────────────
        // PRIVATE: xử lý kết quả thanh toán chung
        // (dùng chung cho cả IPN lẫn ReturnUrl)
        // ──────────────────────────────────────────
        private async Task ProcessPaymentResult(
            int orderId, string rspCode, string txnRef, string source)
        {
            var order = await _db.Orders
                .Include(o => o.OrderItems)!.ThenInclude(oi => oi.Book)
                .FirstOrDefaultAsync(o => o.orderID == orderId);

            if (order is null) return;

            // Idempotent guard
            if (order.paymentStatus != "unpaid") return;

            if (rspCode == "00")
            {
                // ✅ Thanh toán thành công
                order.paymentStatus = "paid";
                order.status = "confirmed";      // Tự động xác nhận
                order.vnpayTxnRef = txnRef;
                order.paidAt = DateTime.UtcNow;
                order.updatedAt = DateTime.UtcNow;

                _log.LogInformation(
                    "[{src}] Thanh toán thành công. OrderId={id} TxnRef={ref}",
                    source, orderId, txnRef);
            }
            else
            {
                // ❌ Thanh toán thất bại / bị huỷ — hoàn kho
                order.paymentStatus = "failed";
                order.status = "cancelled";
                order.updatedAt = DateTime.UtcNow;

                if (order.OrderItems != null)
                    foreach (var item in order.OrderItems)
                    {
                        item.Book.numberStock += item.quantity;
                        item.Book.numberSold -= item.quantity;
                    }

                _log.LogWarning(
                    "[{src}] Thanh toán thất bại. OrderId={id} RspCode={code}",
                    source, orderId, rspCode);
            }

            await _db.SaveChangesAsync();
        }
    }
}