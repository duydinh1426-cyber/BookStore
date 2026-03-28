/*
 * GET - /api/categories : Lấy tất cả thể loại
 * GET - /api/categories/{id} : Lấy chi tiết 1 thể loại
 * GET - /api/categories/{id}/books : Lấy tất cả sách thuộc thể loại
 * POST - /api/categories [Admin] : Tạo thể loại mới
 * PUT /api/categories/{id} [Admin] : Cập nhật tên thể loại
 * DELETE - /api/categories/{id} [Admin] : Xóa thể loại (kiểm tra còn sách không)
 * GET /api/categories/search?q=... : Tìm kiếm thể loại theo tên
 */

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using BookStoreAPI.Data;
using BookStoreAPI.Models;
using BookStoreAPI.DTOs;

namespace BookStoreAPI.Controllers
{
    [ApiController]
    [Route("api/categories")]
    public class CategoryController : ControllerBase
    {
        private readonly AppDbContext _db;

        public CategoryController(AppDbContext db) => _db = db;

        // GET - /api/categories : Lấy tất cả thể loại
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] bool includeBookCount = false)
        {
            if (includeBookCount)
            {
                var result = await _db.Categories
                    .Select(c => new
                    {
                        categoryId = c.categoryID,
                        categoryName = c.categoryName,
                        bookCount = c.Books != null ? c.Books.Count : 0
                    })
                    .OrderBy(c => c.categoryName)
                    .ToListAsync();

                return Ok(result);
            }

            var cats = await _db.Categories
                .OrderBy(c => c.categoryName)
                .Select(c => new CategoryDto(c.categoryID, c.categoryName))
                .ToListAsync();

            return Ok(cats);
        }

        // GET - /api/categories/{id} : Lấy chi tiết 1 thể loại
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var cat = await _db.Categories.FindAsync(id);
            if (cat is null) return NotFound(new { message = "Không tìm thấy thể loại" });

            return Ok(new CategoryDto(cat.categoryID, cat.categoryName));
        }

        // GET - /api/categories/{id}/books : Lấy tất cả sách thuộc thể loại
        [HttpGet("{id:int}/books")]
        public async Task<IActionResult> GetBooks(int id,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 12)
        {
            var exists = await _db.Categories.AnyAsync(c => c.categoryID == id);
            if (!exists) return NotFound(new { message = "Không tìm thấy thể loại" });

            var query = _db.Books
                .Where(b => b.categoryID == id)
                .OrderBy(b => b.title);

            var total = await query.CountAsync();

            var books = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new
                {
                    b.bookID,
                    b.title,
                    b.author,
                    b.price,
                    b.image,
                    b.numberStock,
                    b.numberSold
                })
                .ToListAsync();

            return Ok(new
            {
                total,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(total / (double)pageSize),
                data = books
            });
        }

        // POST - /api/categories [Admin] : Tạo thể loại mới
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateCategoryDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.CategoryName))
                return BadRequest(new { message = "Tên thể loại không được để trống" });

            var duplicate = await _db.Categories
                .AnyAsync(c => c.categoryName.ToLower() == dto.CategoryName.ToLower().Trim());

            if (duplicate)
                return Conflict(new { message = "Thể loại đã tồn tại" });

            var cat = new Category { categoryName = dto.CategoryName.Trim() };
            _db.Categories.Add(cat);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById),
                new { id = cat.categoryID },
                new CategoryDto(cat.categoryID, cat.categoryName));
        }

        // PUT /api/categories/{id} [Admin] : Cập nhật tên thể loại
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateCategoryDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.CategoryName))
                return BadRequest(new { message = "Tên thể loại không được để trống" });

            var cat = await _db.Categories.FindAsync(id);
            if (cat is null) return NotFound(new { message = "Không tìm thấy thể loại" });

            var duplicate = await _db.Categories
                .AnyAsync(c => c.categoryName.ToLower() == dto.CategoryName.ToLower().Trim()
                            && c.categoryID != id);

            if (duplicate)
                return Conflict(new { message = "Tên thể loại đã tồn tại" });

            cat.categoryName = dto.CategoryName.Trim();
            await _db.SaveChangesAsync();

            return Ok(new CategoryDto(cat.categoryID, cat.categoryName));
        }

        // DELETE - /api/categories/{id} [Admin] : Xóa thể loại (kiểm tra còn sách không)
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id, [FromQuery] bool force = false)
        {
            var cat = await _db.Categories
                .Include(c => c.Books)
                .FirstOrDefaultAsync(c => c.categoryID == id);

            if (cat is null) return NotFound(new { message = "Không tìm thấy thể loại" });

            if (cat.Books != null && cat.Books.Any() && !force)
            {
                return Conflict(new
                {
                    message = $"Thể loại còn {cat.Books.Count} cuốn sách. Dùng ?force=true để xóa bắt buộc",
                    bookCount = cat.Books.Count
                });
            }

            _db.Categories.Remove(cat);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Đã xóa thể loại thành công" });
        }

        // GET /api/categories/search?q=... : Tìm kiếm thể loại theo tên
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string q = "")
        {
            var cats = await _db.Categories
                .Where(c => c.categoryName.Contains(q))
                .OrderBy(c => c.categoryName)
                .Select(c => new CategoryDto(c.categoryID, c.categoryName))
                .ToListAsync();

            return Ok(cats);
        }
    }
}