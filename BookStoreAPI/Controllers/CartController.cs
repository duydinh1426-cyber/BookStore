using BookStoreAPI.Data;
using BookStoreAPI.DTOs;
using BookStoreAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BookStoreAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // tất cả API giỏ hàng đều cần đăng nhập
    public class CartController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CartController(AppDbContext context)
        {
            _context = context;
        }

        // Helper — lấy userID từ JWT token
        private int GetUserId() =>
    int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /*
         * ════════════════════════════════════════════════════════════════
         * GET /api/cart
         * ════════════════════════════════════════════════════════════════
         * Xem toàn bộ giỏ hàng của user hiện tại.
         * Trả về danh sách sách + số lượng + tổng tiền.
         *
         * RESPONSE: CartResponseDto
         *   { items: [...], totalPrice, totalItems }
         *
         * QUYỀN: Đã đăng nhập
         * ════════════════════════════════════════════════════════════════
         */
        [HttpGet]
        public async Task<IActionResult> GetCart()
        {
            var userId = GetUserId();

            var items = await _context.CartItems
                .Include(c => c.Book)
                .Where(c => c.userID == userId)
                .Select(c => new CartItemResponseDto(
                    c.cartItemID,
                    c.bookID,
                    c.Book.title,
                    c.Book.author,
                    c.Book.image,
                    c.Book.price,
                    c.quantity,
                    c.Book.price * c.quantity
                ))
                .ToListAsync();

            var response = new CartResponseDto(
                Items: items,
                TotalPrice: items.Sum(i => i.SubTotal),
                TotalItems: items.Sum(i => i.Quantity)
            );

            return Ok(response);
        }

        /*
         * ════════════════════════════════════════════════════════════════
         * POST /api/cart
         * ════════════════════════════════════════════════════════════════
         * Thêm sách vào giỏ hàng.
         * Nếu sách đã có trong giỏ → cộng thêm số lượng.
         * Nếu chưa có → thêm mới.
         *
         * BODY (AddCartDto):
         *   bookId   : ID sách muốn thêm
         *   quantity : số lượng muốn thêm
         *
         * LỖI CÓ THỂ GẶP:
         *   404 : sách không tồn tại
         *   400 : sách hết hàng
         *   400 : số lượng vượt quá tồn kho
         *
         * RESPONSE: { message }
         * QUYỀN: Đã đăng nhập
         * ════════════════════════════════════════════════════════════════
         */
        [HttpPost]
        public async Task<IActionResult> AddToCart([FromBody] AddCartDto dto)
        {
            var userId = GetUserId();

            // Kiểm tra sách tồn tại
            var book = await _context.Books.FindAsync(dto.BookId);
            if (book == null)
                return NotFound(new { message = "Không tìm thấy sách." });

            // Kiểm tra tồn kho
            if (book.numberStock <= 0)
                return BadRequest(new { message = "Sách đã hết hàng." });

            // Kiểm tra số lượng yêu cầu có vượt tồn kho không
            var cartItem = await _context.CartItems
                .FirstOrDefaultAsync(c => c.userID == userId && c.bookID == dto.BookId);

            var currentQty = cartItem?.quantity ?? 0;
            if (currentQty + dto.Quantity > book.numberStock)
                return BadRequest(new { message = $"Chỉ còn {book.numberStock} cuốn trong kho." });

            if (cartItem == null)
            {
                // Chưa có trong giỏ → thêm mới
                _context.CartItems.Add(new CartItem
                {
                    userID = userId,
                    bookID = dto.BookId,
                    quantity = dto.Quantity,
                    createdAt = DateTime.UtcNow,
                    updatedAt = DateTime.UtcNow
                });
            }
            else
            {
                // Đã có → cộng thêm số lượng
                cartItem.quantity += dto.Quantity;
                cartItem.updatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã thêm vào giỏ hàng." });
        }

        /*
         * ════════════════════════════════════════════════════════════════
         * PUT /api/cart/{bookId}
         * ════════════════════════════════════════════════════════════════
         * Cập nhật số lượng sách trong giỏ hàng.
         * Nếu quantity = 0 → tự động xóa khỏi giỏ.
         *
         * BODY (UpdateCartDto):
         *   quantity : số lượng mới (0 = xóa khỏi giỏ)
         *
         * LỖI CÓ THỂ GẶP:
         *   404 : sách không có trong giỏ
         *   400 : số lượng vượt quá tồn kho
         *
         * RESPONSE: { message }
         * QUYỀN: Đã đăng nhập
         * ════════════════════════════════════════════════════════════════
         */
        [HttpPut("{bookId:int}")]
        public async Task<IActionResult> UpdateCart(int bookId, [FromBody] UpdateCartDto dto)
        {
            var userId = GetUserId();

            var cartItem = await _context.CartItems
                .Include(c => c.Book)
                .FirstOrDefaultAsync(c => c.userID == userId && c.bookID == bookId);

            if (cartItem == null)
                return NotFound(new { message = "Sách không có trong giỏ hàng." });

            // quantity = 0 → xóa khỏi giỏ
            if (dto.Quantity <= 0)
            {
                _context.CartItems.Remove(cartItem);
                await _context.SaveChangesAsync();
                return Ok(new { message = "Đã xóa sách khỏi giỏ hàng." });
            }

            // Kiểm tra tồn kho
            if (dto.Quantity > cartItem.Book.numberStock)
                return BadRequest(new { message = $"Chỉ còn {cartItem.Book.numberStock} cuốn trong kho." });

            cartItem.quantity = dto.Quantity;
            cartItem.updatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã cập nhật giỏ hàng." });
        }

        /*
         * ════════════════════════════════════════════════════════════════
         * DELETE /api/cart/{bookId}
         * ════════════════════════════════════════════════════════════════
         * Xóa 1 sách khỏi giỏ hàng.
         *
         * LỖI CÓ THỂ GẶP:
         *   404 : sách không có trong giỏ
         *
         * RESPONSE: { message }
         * QUYỀN: Đã đăng nhập
         * ════════════════════════════════════════════════════════════════
         */
        [HttpDelete("{bookId:int}")]
        public async Task<IActionResult> RemoveFromCart(int bookId)
        {
            var userId = GetUserId();

            var cartItem = await _context.CartItems
                .FirstOrDefaultAsync(c => c.userID == userId && c.bookID == bookId);

            if (cartItem == null)
                return NotFound(new { message = "Sách không có trong giỏ hàng." });

            _context.CartItems.Remove(cartItem);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã xóa sách khỏi giỏ hàng." });
        }

        /*
         * ════════════════════════════════════════════════════════════════
         * DELETE /api/cart
         * ════════════════════════════════════════════════════════════════
         * Xóa toàn bộ giỏ hàng của user hiện tại.
         * Thường được gọi sau khi thanh toán thành công.
         *
         * RESPONSE: { message }
         * QUYỀN: Đã đăng nhập
         * ════════════════════════════════════════════════════════════════
         */
        [HttpDelete]
        public async Task<IActionResult> ClearCart()
        {
            var userId = GetUserId();

            await _context.CartItems
                .Where(c => c.userID == userId)
                .ExecuteDeleteAsync();

            return Ok(new { message = "Đã xóa toàn bộ giỏ hàng." });
        }
    }
}