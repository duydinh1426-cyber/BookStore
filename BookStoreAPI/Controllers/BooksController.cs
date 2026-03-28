/*
 * GET - /api/books
 * GET - /api/books/top-new
 * GET - /api/books/top-selling
 * GET - /api/books/top-rated
 * GET - /api/books/{id}
 * POST /api/books [Admin]
 * PUT - /api/books/{id}  [Admin]
 * DELETE - /api/books/{id}  [Admin]
 */

using BookStoreAPI.Data;
using BookStoreAPI.Models;
using BookStoreAPI.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace BookStoreAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BooksController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BooksController(AppDbContext context)
        {
            _context = context;
        }

        // GET - /api/books
        [HttpGet]
        public async Task<IActionResult> GetBooks(
            int page = 1,
            int pageSize = 10,
            string? keyword = null,
            int? categoryId = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            string sortBy = "createdAt",
            string sortOrder = "desc")
        {
            if (minPrice < 0 || maxPrice < 0)
                return BadRequest(new { message = "Giá không được âm." });

            if (minPrice.HasValue && maxPrice.HasValue && minPrice > maxPrice)
                return BadRequest(new { message = "Giá tối thiểu không được lớn hơn giá tối đa." });

            var query = _context.Books
                .AsNoTracking()
                .Include(b => b.Category)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = $"%{keyword.Trim()}%";
                query = query.Where(b =>
                    EF.Functions.Like(b.title, kw) ||
                    EF.Functions.Like(b.author, kw));
            }

            if (categoryId.HasValue)
                query = query.Where(b => b.categoryID == categoryId);

            if (minPrice.HasValue)
                query = query.Where(b => b.price >= minPrice.Value);

            if (maxPrice.HasValue)
                query = query.Where(b => b.price <= maxPrice.Value);

            query = (sortBy.ToLower(), sortOrder.ToLower()) switch
            {
                ("price", "asc") => query.OrderBy(b => b.price),
                ("price", _) => query.OrderByDescending(b => b.price),
                ("title", "asc") => query.OrderBy(b => b.title),
                ("title", _) => query.OrderByDescending(b => b.title),
                ("numbersold", "asc") => query.OrderBy(b => b.numberSold),
                ("numbersold", _) => query.OrderByDescending(b => b.numberSold),
                ("avgrating", "asc") => query.OrderBy(b => b.avgRating),
                ("avgrating", _) => query.OrderByDescending(b => b.avgRating),
                (_, "asc") => query.OrderBy(b => b.createdAt),
                _ => query.OrderByDescending(b => b.createdAt)
            };

            var total = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)total / pageSize);

            var books = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new BookSummaryDto(
                    b.bookID, b.title, b.author, b.price, b.image,
                    b.Category != null ? b.Category.categoryName : null,
                    b.numberStock, b.numberSold))
                .ToListAsync();

            return Ok(new { total, page, pageSize, totalPages, data = books });
        }

        // GET - /api/books/top-new
        [HttpGet("top-new")]
        public async Task<IActionResult> GetTopNew([FromQuery] int count = 6)
        {
            var books = await _context.Books
                .AsNoTracking().Include(b => b.Category)
                .OrderByDescending(b => b.createdAt).Take(count)
                .Select(b => new BookSummaryDto(
                    b.bookID, b.title, b.author, b.price, b.image,
                    b.Category != null ? b.Category.categoryName : null,
                    b.numberStock, b.numberSold))
                .ToListAsync();
            return Ok(books);
        }

        // GET - /api/books/top-selling
        [HttpGet("top-selling")]
        public async Task<IActionResult> GetTopSelling([FromQuery] int count = 6)
        {
            var books = await _context.Books
                .AsNoTracking().Include(b => b.Category)
                .OrderByDescending(b => b.numberSold).Take(count)
                .Select(b => new BookSummaryDto(
                    b.bookID, b.title, b.author, b.price, b.image,
                    b.Category != null ? b.Category.categoryName : null,
                    b.numberStock, b.numberSold))
                .ToListAsync();
            return Ok(books);
        }

        // GET - /api/books/top-rated
        [HttpGet("top-rated")]
        public async Task<IActionResult> GetTopRated([FromQuery] int count = 6)
        {
            var books = await _context.Books
                .AsNoTracking().Include(b => b.Category)
                .Where(b => b.reviewCount > 0)
                .OrderByDescending(b => b.avgRating)
                .ThenByDescending(b => b.reviewCount).Take(count)
                .Select(b => new BookSummaryDto(
                    b.bookID, b.title, b.author, b.price, b.image,
                    b.Category != null ? b.Category.categoryName : null,
                    b.numberStock, b.numberSold))
                .ToListAsync();
            return Ok(books);
        }

        // GET - /api/books/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetBookById(int id)
        {
            var book = await _context.Books
                .AsNoTracking().Include(b => b.Category)
                .FirstOrDefaultAsync(b => b.bookID == id);

            if (book == null)
                return NotFound(new { message = "Không tìm thấy sách." });

            return Ok(new BookDetailDto(
                book.bookID, book.title, book.author, book.price,
                book.image, book.description, book.publisherYear,
                book.numberPage, book.numberStock, book.numberSold,
                book.categoryID,
                book.Category?.categoryName,
                Math.Round((double)book.avgRating, 1), book.reviewCount));
        }

        // POST /api/books [Admin]
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> AddBook([FromBody] BookUpsertDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
                return BadRequest(new { message = "Tên sách không được để trống." });

            if (dto.Price <= 0)
                return BadRequest(new { message = "Giá sách phải lớn hơn 0." });

            if (dto.CategoryId.HasValue)
            {
                var exists = await _context.Categories
                    .AnyAsync(c => c.categoryID == dto.CategoryId);
                if (!exists)
                    return BadRequest(new { message = "Thể loại không tồn tại." });
            }

            var book = new Book
            {
                categoryID = dto.CategoryId,
                author = dto.Author?.Trim() ?? "",
                title = dto.Title.Trim(),
                publisherYear = dto.PublisherYear,
                description = dto.Description?.Trim(),
                image = dto.Image?.Trim(),
                price = dto.Price,
                numberPage = dto.NumberPage ?? 0,
                numberStock = dto.NumberStock,
                numberSold = 0,
                avgRating = 0,
                reviewCount = 0,
                createdAt = DateTime.UtcNow,
                updatedAt = DateTime.UtcNow
            };

            _context.Books.Add(book);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetBookById),
                new { id = book.bookID },
                new { message = "Thêm sách thành công.", bookId = book.bookID });
        }

        // PUT - /api/books/{id}  [Admin]
        [Authorize(Roles = "Admin")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateBook(int id, [FromBody] BookUpsertDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
                return BadRequest(new { message = "Tên sách không được để trống." });

            if (dto.Price <= 0)
                return BadRequest(new { message = "Giá sách phải lớn hơn 0." });

            var book = await _context.Books.FindAsync(id);
            if (book == null)
                return NotFound(new { message = "Không tìm thấy sách." });

            if (dto.CategoryId.HasValue)
            {
                var exists = await _context.Categories
                    .AnyAsync(c => c.categoryID == dto.CategoryId);
                if (!exists)
                    return BadRequest(new { message = "Thể loại không tồn tại." });
            }

            book.categoryID = dto.CategoryId;
            book.author = dto.Author?.Trim() ?? "";
            book.title = dto.Title.Trim();
            book.publisherYear = dto.PublisherYear;
            book.description = dto.Description?.Trim();
            book.image = dto.Image?.Trim();
            book.price = dto.Price;
            book.numberPage = dto.NumberPage ?? 0;
            book.numberStock = dto.NumberStock;
            book.updatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cập nhật sách thành công." });
        }

        // DELETE - /api/books/{id}  [Admin]
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteBook(int id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book == null)
                return NotFound(new { message = "Không tìm thấy sách." });

            var hasOrders = await _context.OrderItems.AnyAsync(oi => oi.bookID == id);
            if (hasOrders)
                return BadRequest(new { message = "Không thể xóa sách đã có trong đơn hàng." });

            _context.Books.Remove(book);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Xóa sách thành công." });
        }
    }
}