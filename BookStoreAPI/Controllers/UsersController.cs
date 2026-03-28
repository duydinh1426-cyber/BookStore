/*
 * GET - /api/users/admin/all
 * GET - /api/users/admin/{id}
 * POST - /api/users/admin/{id}/reset-password
 */

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using BookStoreAPI.Data;

namespace BookStoreAPI.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize(Roles = "Admin")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _db;

        public UsersController(AppDbContext db)
        {
            _db = db;
        }

        // GET - /api/users/admin/all
        [HttpGet("admin/all")]
        public async Task<IActionResult> GetAllUsers(
            [FromQuery] string? keyword,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 15)
        {
            var query = _db.Accounts
                .Where(a => !a.isAdmin)
                .Include(a => a.Customer)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(a =>
                    a.Customer!.name.ToLower().Contains(kw) ||
                    a.username.ToLower().Contains(kw) ||
                    a.email.ToLower().Contains(kw) ||
                    (a.Customer!.address != null &&
                     a.Customer.address.ToLower().Contains(kw))
                );
            }

            var total = await query.CountAsync();

            var users = await query
                .OrderByDescending(a => a.createdAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    userId = a.Customer!.userID,
                    accountId = a.accountID,
                    name = a.Customer.name,
                    username = a.username,
                    email = a.email,
                    address = a.Customer.address ?? "",
                    isAdmin = false,
                    createdAt = a.createdAt
                })
                .ToListAsync();

            return Ok(new
            {
                data = users,
                total = total,
                totalPages = (int)Math.Ceiling((double)total / pageSize),
                page = page
            });
        }

        // GET - /api/users/admin/{id}
        [HttpGet("admin/{id:int}")]
        public async Task<IActionResult> GetUserDetail(int id)
        {
            var account = await _db.Accounts
                .Include(a => a.Customer)
                    .ThenInclude(c => c!.Orders)
                .FirstOrDefaultAsync(a => a.accountID == id && !a.isAdmin);

            if (account is null)
                return NotFound(new { message = "Không tìm thấy người dùng." });

            var totalOrders = account.Customer?.Orders?.Count ?? 0;
            var totalSpent = account.Customer?.Orders?
                .Sum(o => (decimal)o.totalCost) ?? 0;

            return Ok(new
            {
                userId = account.Customer?.userID,
                accountId = account.accountID,
                name = account.Customer?.name ?? "",
                username = account.username,
                email = account.email,
                address = account.Customer?.address ?? "",
                isAdmin = false,
                totalOrders = totalOrders,
                totalSpent = totalSpent,
                createdAt = account.createdAt
            });
        }

        // POST - /api/users/admin/{id}/reset-password
        [HttpPost("admin/{id:int}/reset-password")]
        public async Task<IActionResult> ResetPassword(int id)
        {
            var account = await _db.Accounts
                .FirstOrDefaultAsync(a => a.accountID == id && !a.isAdmin);

            if (account is null)
                return NotFound(new { message = "Không tìm thấy người dùng." });

            const string DEFAULT_PASSWORD = "123456";
            var bytes = System.Security.Cryptography.SHA256.HashData(
                            System.Text.Encoding.UTF8.GetBytes(DEFAULT_PASSWORD));
            account.password = Convert.ToHexString(bytes).ToLower();
            await _db.SaveChangesAsync();

            return Ok(new { message = "Đã reset mật khẩu về 123456." });
        }
    }
}