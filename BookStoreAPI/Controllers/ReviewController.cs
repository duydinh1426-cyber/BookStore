/*
 * GET - /api/reviews/book/{bookId} : Lấy tất cả đánh giá của 1 cuốn sách (public)
 * GET /api/reviews/status/{bookId} [Customer] : Kiểm tra trạng thái review của user với sách
 * POST - /api/reviews [Customer] : Tạo đánh giá mới (Điều kiện: đã mua sách và chưa đánh giá cuốn này)
 * DELETE - /api/reviews/{id} [Admin] : Xóa đánh giá
 * GET - /api/reviews/admin/all [Admin] : Lấy tất cả đánh giá, có filter theo rating và bookId
 */

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
    [Route("api/reviews")]
    public class ReviewController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ReviewController(AppDbContext db) => _db = db;

        // Helper lấy userId từ JWT
        private int GetUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")
                   ?? "0");

        // GET - /api/reviews/book/{bookId} : Lấy tất cả đánh giá của 1 cuốn sách (public)
        [HttpGet("book/{bookId:int}")]
        public async Task<IActionResult> GetByBook(
            int bookId,
            int page = 1,
            int pageSize = 10,
            int? rating = null)
        {
            var bookExists = await _db.Books.AnyAsync(b => b.bookID == bookId);
            if (!bookExists) return NotFound(new { message = "Không tìm thấy sách" });

            var query = _db.Reviews
                .Include(r => r.Customer)
                .Where(r => r.bookID == bookId);

            if (rating.HasValue && rating >= 1 && rating <= 5)
                query = query.Where(r => r.rating == rating.Value);

            var total = await query.CountAsync();

            var ratingStats = await _db.Reviews
                .Where(r => r.bookID == bookId)
                .GroupBy(r => r.rating)
                .Select(g => new { star = g.Key, count = g.Count() })
                .ToListAsync();

            var avgRating = total > 0
                ? await query.AverageAsync(r => (double)r.rating)
                : 0.0;

            var reviews = await query
                .OrderByDescending(r => r.createdAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new ReviewResponseDto(
                    r.reviewID,
                    r.Customer.name,
                    r.rating,
                    r.comment,
                    r.createdAt))
                .ToListAsync();

            return Ok(new
            {
                total,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(total / (double)pageSize),
                avgRating = Math.Round(avgRating, 1),
                ratingStats = ratingStats.ToDictionary(x => x.star, x => x.count),
                data = reviews
            });
        }

        // GET /api/reviews/status/{bookId} [Customer] : Kiểm tra trạng thái review của user với sách
        [HttpGet("status/{bookId:int}")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> GetReviewStatus(int bookId)
        {
            var userId = GetUserId();

            // Kiểm tra đã mua và đơn đã completed chưa
            var hasPurchased = await _db.OrderItems
                .AnyAsync(oi => oi.bookID == bookId
                             && oi.Order.userID == userId
                             && oi.Order.status == "completed");

            if (!hasPurchased)
                return Ok(new { canReview = false, reason = "not_purchased" });

            // Kiểm tra đã review chưa
            var review = await _db.Reviews
                .FirstOrDefaultAsync(r => r.bookID == bookId && r.userID == userId);

            if (review != null)
                return Ok(new
                {
                    canReview = false,
                    reason = "already_reviewed",
                    rating = review.rating,
                    comment = review.comment
                });

            return Ok(new { canReview = true });
        }

        // POST - /api/reviews [Customer] : Tạo đánh giá mới (Điều kiện: đã mua sách và chưa đánh giá cuốn này)
        [HttpPost]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Create([FromBody] CreateReviewDto dto)
        {
            if (dto.Rating < 1 || dto.Rating > 5)
                return BadRequest(new { message = "Đánh giá phải từ 1 đến 5 sao" });

            var userId = GetUserId();

            // Kiểm tra sách tồn tại
            var bookExists = await _db.Books.AnyAsync(b => b.bookID == dto.BookId);
            if (!bookExists)
                return NotFound(new { message = "Không tìm thấy sách" });

            // Kiểm tra đã mua và giao hàng thành công
            var hasPurchased = await _db.OrderItems
                .AnyAsync(oi => oi.bookID == dto.BookId
                             && oi.Order.userID == userId
                             && oi.Order.status == "completed");

            if (!hasPurchased)
                return BadRequest(new { message = "Bạn cần mua và nhận sách thành công trước khi đánh giá." });

            // Kiểm tra chưa đánh giá cuốn này
            var alreadyReviewed = await _db.Reviews
                .AnyAsync(r => r.bookID == dto.BookId && r.userID == userId);

            if (alreadyReviewed)
                return BadRequest(new { message = "Bạn đã đánh giá cuốn sách này rồi." });

            var review = new Review
            {
                bookID = dto.BookId,
                userID = userId,
                rating = dto.Rating,
                comment = dto.Comment?.Trim(),
                createdAt = DateTime.UtcNow,
                updatedAt = DateTime.UtcNow
            };

            _db.Reviews.Add(review);
            await _db.SaveChangesAsync();
            await UpdateBookRating(dto.BookId);

            return Created($"/api/reviews/{review.reviewID}",
                new { message = "Đánh giá đã được ghi nhận.", reviewId = review.reviewID });
        }

        // DELETE - /api/reviews/{id} [Admin] : Xóa đánh giá
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var review = await _db.Reviews
                .FirstOrDefaultAsync(r => r.reviewID == id);

            if (review is null)
                return NotFound(new { message = "Không tìm thấy đánh giá" });

            var bookId = review.bookID;

            _db.Reviews.Remove(review);
            await _db.SaveChangesAsync();

            if (bookId > 0)
                await UpdateBookRating(bookId);

            return Ok(new { message = "Đã xóa đánh giá" });
        }

        // GET - /api/reviews/admin/all [Admin] : Lấy tất cả đánh giá, có filter theo rating và bookId
        [HttpGet("admin/all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminGetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] int? rating = null,
            [FromQuery] int? bookId = null)
        {
            var query = _db.Reviews
                .Include(r => r.Customer)
                .Include(r => r.Book)
                .AsQueryable();

            if (rating.HasValue) query = query.Where(r => r.rating == rating.Value);
            if (bookId.HasValue) query = query.Where(r => r.bookID == bookId.Value);

            var total = await query.CountAsync();

            var reviews = await query
                .OrderByDescending(r => r.createdAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new
                {
                    r.reviewID,
                    r.rating,
                    r.comment,
                    r.createdAt,
                    customer = r.Customer.name,
                    book = new { r.Book.bookID, r.Book.title }
                })
                .ToListAsync();

            return Ok(new
            {
                total,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(total / (double)pageSize),
                data = reviews
            });
        }

        // Cập nhật điểm trung bình trên bảng Books
        private async Task UpdateBookRating(int bookId)
        {
            var book = await _db.Books.FindAsync(bookId);
            if (book is null) return;

            // Tính trực tiếp trên DB, không load toàn bộ reviews vào bộ nhớ
            var stats = await _db.Reviews
                .Where(r => r.bookID == bookId)
                .GroupBy(r => r.bookID)
                .Select(g => new { avg = g.Average(r => (double)r.rating), count = g.Count() })
                .FirstOrDefaultAsync();

            book.avgRating = stats != null ? (decimal)Math.Round(stats.avg, 2) : 0;
            book.reviewCount = stats?.count ?? 0;

            await _db.SaveChangesAsync();
        }
    }
}