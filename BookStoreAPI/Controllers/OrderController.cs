using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using BookStoreAPI.Data;
using BookStoreAPI.Models;
using BookStoreAPI.DTOs;

namespace BookStoreAPI.Controllers
{
    [ApiController]
    [Route("api/orders")]
    [Authorize]
    public class OrderController : ControllerBase
    {
        private readonly AppDbContext _db;

        public OrderController(AppDbContext db) => _db = db;

        private int GetUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")
                   ?? "0");

        // ══════════════════════════════════════════════════════
        //  STATE MACHINE — định nghĩa luồng chuyển trạng thái
        //
        //  pending → confirmed → shipping → completed
        //     ↓           ↓          ↓
        //  cancelled   cancelled  cancelled
        //
        //  completed và cancelled là trạng thái CUỐI — không
        //  được chuyển đi đâu nữa.
        // ══════════════════════════════════════════════════════
        private static readonly Dictionary<string, string[]> _allowedTransitions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["pending"] = new[] { "confirmed", "cancelled" },
                ["confirmed"] = new[] { "shipping", "cancelled" },
                ["shipping"] = new[] { "completed", "cancelled" },
                ["completed"] = Array.Empty<string>(),
                ["cancelled"] = Array.Empty<string>(),
            };

        private static readonly Dictionary<string, string> _statusLabels =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["pending"] = "Chờ xác nhận",
                ["confirmed"] = "Đã xác nhận",
                ["shipping"] = "Đang giao",
                ["completed"] = "Hoàn thành",
                ["cancelled"] = "Đã huỷ",
            };

        // ──────────────────────────────────────────
        // POST /api/orders/checkout     [Customer]
        // ──────────────────────────────────────────
        [HttpPost("checkout")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Checkout([FromBody] CheckoutDto dto)
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

            var errors = new List<string>();
            foreach (var item in cartItems)
                if (item.Book.numberStock < item.quantity)
                    errors.Add($"Sách '{item.Book.title}' chỉ còn {item.Book.numberStock} cuốn");

            if (errors.Any())
                return BadRequest(new { message = "Không đủ hàng", errors });

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var order = new Order
                {
                    userID = userId,
                    phone = dto.Phone.Trim(),
                    address = dto.Address.Trim(),
                    note = dto.Note?.Trim(),
                    status = "pending",
                    createdAt = DateTime.UtcNow,
                    updatedAt = DateTime.UtcNow
                };

                decimal total = 0;
                var orderItems = new List<OrderItem>();

                foreach (var item in cartItems)
                {
                    var unitPrice = item.Book.price;
                    total += unitPrice * item.quantity;

                    orderItems.Add(new OrderItem
                    {
                        bookID = item.bookID,
                        quantity = item.quantity,
                        unitPrice = unitPrice,
                        createdAt = DateTime.UtcNow,
                        updatedAt = DateTime.UtcNow
                    });

                    item.Book.numberStock -= item.quantity;
                    item.Book.numberSold += item.quantity;
                }

                order.totalCost = total;
                order.OrderItems = orderItems;

                _db.Orders.Add(order);
                _db.CartItems.RemoveRange(cartItems);

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return StatusCode(201, new
                {
                    message = "Đặt hàng thành công",
                    orderId = order.orderID,
                    totalCost = total,
                    itemCount = orderItems.Count
                });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return StatusCode(500, new { message = "Lỗi tạo đơn hàng", detail = ex.Message });
            }
        }

        // ──────────────────────────────────────────
        // GET /api/orders/my            [Customer]
        // ──────────────────────────────────────────
        [HttpGet("my")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> GetMyOrders(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? status = null)
        {
            var userId = GetUserId();
            var query = _db.Orders.Where(o => o.userID == userId).AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(o => o.status == status);

            var total = await query.CountAsync();
            var orders = await query
                .OrderByDescending(o => o.createdAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new
                {
                    o.orderID,
                    o.totalCost,
                    o.status,
                    o.phone,
                    o.address,
                    o.note,
                    o.createdAt,
                    itemCount = o.OrderItems != null ? o.OrderItems.Count : 0
                })
                .ToListAsync();

            return Ok(new
            {
                total,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(total / (double)pageSize),
                data = orders
            });
        }

        // ──────────────────────────────────────────
        // GET /api/orders/{id}          [Customer | Admin]
        // ──────────────────────────────────────────
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var userId = GetUserId();
            var isAdmin = User.IsInRole("Admin");

            var order = await _db.Orders
                .Include(o => o.OrderItems)!.ThenInclude(oi => oi.Book)
                .Include(o => o.Customer).ThenInclude(c => c.Account)
                .FirstOrDefaultAsync(o => o.orderID == id);

            if (order is null) return NotFound(new { message = "Không tìm thấy đơn hàng" });
            if (!isAdmin && order.userID != userId) return Forbid();

            // Trả về thêm nextStatuses để frontend biết nút nào cần hiện
            var nextStatuses = _allowedTransitions.TryGetValue(order.status, out var next)
                ? next : Array.Empty<string>();

            return Ok(new
            {
                order.orderID,
                order.totalCost,
                order.status,
                order.phone,
                order.address,
                order.note,
                order.createdAt,
                order.updatedAt,
                // Danh sách trạng thái được phép chuyển tiếp
                nextStatuses,
                isFinal = nextStatuses.Length == 0,
                customer = new
                {
                    order.Customer.userID,
                    name = order.Customer.name,
                    email = order.Customer.Account?.email ?? ""
                },
                items = order.OrderItems?.Select(oi => new
                {
                    oi.orderItemID,
                    oi.quantity,
                    oi.unitPrice,
                    subTotal = oi.quantity * oi.unitPrice,
                    book = new
                    {
                        oi.Book.bookID,
                        oi.Book.title,
                        oi.Book.author,
                        oi.Book.image
                    }
                })
            });
        }

        // ──────────────────────────────────────────
        // PUT /api/orders/{id}/cancel   [Customer]
        // Chỉ hủy được khi đơn còn "pending"
        // ──────────────────────────────────────────
        [HttpPut("{id:int}/cancel")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Cancel(int id)
        {
            var userId = GetUserId();

            var order = await _db.Orders
                .Include(o => o.OrderItems)!.ThenInclude(oi => oi.Book)
                .FirstOrDefaultAsync(o => o.orderID == id);

            if (order is null) return NotFound(new { message = "Không tìm thấy đơn hàng" });
            if (order.userID != userId) return Forbid();

            // Dùng state machine — customer chỉ được hủy từ pending
            if (!_allowedTransitions.TryGetValue(order.status, out var allowed)
                || !allowed.Contains("cancelled"))
            {
                var label = _statusLabels.GetValueOrDefault(order.status, order.status);
                return BadRequest(new { message = $"Không thể hủy đơn ở trạng thái '{label}'" });
            }

            if (order.OrderItems != null)
                foreach (var item in order.OrderItems)
                {
                    item.Book.numberStock += item.quantity;
                    item.Book.numberSold -= item.quantity;
                }

            order.status = "cancelled";
            order.updatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(new { message = "Đã hủy đơn hàng thành công" });
        }

        // ──────────────────────────────────────────
        // GET /api/orders/admin/all     [Admin]
        // ──────────────────────────────────────────
        [HttpGet("admin/all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminGetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? status = null,
            [FromQuery] string? keyword = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            var query = _db.Orders
                .Include(o => o.Customer).ThenInclude(c => c.Account)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(o => o.status == status);

            if (!string.IsNullOrWhiteSpace(keyword))
                query = query.Where(o =>
                    o.Customer.name.Contains(keyword) ||
                    o.Customer.Account.email.Contains(keyword) ||
                    o.phone.Contains(keyword));

            if (from.HasValue) query = query.Where(o => o.createdAt >= from.Value);
            if (to.HasValue) query = query.Where(o => o.createdAt <= to.Value);

            var total = await query.CountAsync();
            var orders = await query
                .OrderByDescending(o => o.createdAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new
                {
                    o.orderID,
                    o.totalCost,
                    o.status,
                    o.phone,
                    o.address,
                    o.createdAt,
                    customerName = o.Customer.name,
                    itemCount = o.OrderItems != null ? o.OrderItems.Count : 0
                })
                .ToListAsync();

            return Ok(new
            {
                total,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(total / (double)pageSize),
                data = orders
            });
        }

        // ──────────────────────────────────────────
        // PUT /api/orders/admin/{id}/status [Admin]
        // ── STATE MACHINE: chỉ cho phép chuyển theo luồng ──
        // ──────────────────────────────────────────
        [HttpPut("admin/{id:int}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateOrderStatusDto dto)
        {
            // 1. Kiểm tra status đích có hợp lệ không
            if (!_allowedTransitions.ContainsKey(dto.Status))
                return BadRequest(new
                {
                    message = "Trạng thái không hợp lệ.",
                    valid = _allowedTransitions.Keys
                });

            var order = await _db.Orders
                .Include(o => o.OrderItems)!.ThenInclude(oi => oi.Book)
                .FirstOrDefaultAsync(o => o.orderID == id);

            if (order is null)
                return NotFound(new { message = "Không tìm thấy đơn hàng" });

            // 2. Kiểm tra state machine
            if (!_allowedTransitions.TryGetValue(order.status, out var allowed)
                || !allowed.Contains(dto.Status, StringComparer.OrdinalIgnoreCase))
            {
                var fromLabel = _statusLabels.GetValueOrDefault(order.status, order.status);
                var toLabel = _statusLabels.GetValueOrDefault(dto.Status, dto.Status);

                // Nếu đây là trạng thái cuối
                if (allowed?.Length == 0)
                    return BadRequest(new
                    {
                        message = $"Đơn hàng đã ở trạng thái '{fromLabel}' — không thể thay đổi thêm."
                    });

                var nextLabels = allowed?
                    .Select(s => _statusLabels.GetValueOrDefault(s, s))
                    .ToArray() ?? Array.Empty<string>();

                return BadRequest(new
                {
                    message = $"Không thể chuyển từ '{fromLabel}' sang '{toLabel}'.",
                    allowedNext = nextLabels
                });
            }

            // 3. Hoàn kho nếu chuyển sang cancelled
            if (dto.Status.Equals("cancelled", StringComparison.OrdinalIgnoreCase)
                && !order.status.Equals("cancelled", StringComparison.OrdinalIgnoreCase))
            {
                if (order.OrderItems != null)
                    foreach (var item in order.OrderItems)
                    {
                        item.Book.numberStock += item.quantity;
                        item.Book.numberSold -= item.quantity;
                    }
            }

            order.status = dto.Status.ToLower();
            order.updatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // Trả về nextStatuses để frontend cập nhật nút ngay
            var nextAllowed = _allowedTransitions.TryGetValue(order.status, out var nx)
                ? nx : Array.Empty<string>();

            return Ok(new
            {
                message = "Cập nhật trạng thái thành công",
                orderId = order.orderID,
                newStatus = order.status,
                nextStatuses = nextAllowed,
                isFinal = nextAllowed.Length == 0
            });
        }

        // ──────────────────────────────────────────
        // GET /api/orders/admin/stats   [Admin]
        // ──────────────────────────────────────────
        [HttpGet("admin/stats")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminStats(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            var query = _db.Orders.AsQueryable();
            if (from.HasValue) query = query.Where(o => o.createdAt >= from.Value);
            if (to.HasValue) query = query.Where(o => o.createdAt <= to.Value);

            var byStatus = await query
                .GroupBy(o => o.status)
                .Select(g => new
                {
                    status = g.Key,
                    count = g.Count(),
                    revenue = g.Sum(o => o.totalCost)
                })
                .ToListAsync();

            var totalRevenue = await query
                .Where(o => o.status == "completed")
                .SumAsync(o => (decimal?)o.totalCost) ?? 0;

            return Ok(new
            {
                totalOrders = await query.CountAsync(),
                totalRevenue,
                byStatus
            });
        }
    }
}