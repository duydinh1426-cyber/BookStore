using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using BookStoreAPI.Data;
using BookStoreAPI.DTOs;
using BookStoreAPI.Models;

namespace BookStoreAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _cfg;

        public AuthController(AppDbContext db, IConfiguration cfg)
        {
            _db = db;
            _cfg = cfg;
        }

        // ── Helpers ───────────────────────────────────────────
        private int GetAccountId() =>
            int.Parse(User.FindFirstValue("accountId") ?? "0");

        private int GetUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")
                   ?? "0");

        private string HashPassword(string password)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(bytes).ToLower();
        }

        private string GenerateJwt(Account account, int userId, string name)
        {
            var key = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(_cfg["Jwt:SecretKey"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("accountId",               account.accountID.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name,           name),
                new Claim(ClaimTypes.Email,          account.email),
                new Claim(ClaimTypes.Role,           account.isAdmin ? "Admin" : "Customer")
            };

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // ── POST /api/auth/register ────────────────────────────
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (await _db.Accounts.AnyAsync(a => a.username == dto.Username))
                return BadRequest(new { message = "Tên đăng nhập đã tồn tại." });

            if (await _db.Accounts.AnyAsync(a => a.email == dto.Email))
                return BadRequest(new { message = "Email đã được sử dụng." });

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var account = new Account
                {
                    username = dto.Username,
                    password = HashPassword(dto.Password),
                    email = dto.Email,
                    isAdmin = false
                };
                _db.Accounts.Add(account);
                await _db.SaveChangesAsync();

                var customer = new Customer
                {
                    accountID = account.accountID,
                    name = dto.Name,
                    address = dto.Address
                };
                _db.Customers.Add(customer);
                await _db.SaveChangesAsync();

                await tx.CommitAsync();
                return Ok(new { message = "Đăng ký thành công!" });
            }
            catch
            {
                await tx.RollbackAsync();
                return StatusCode(500, new { message = "Lỗi khi tạo tài khoản." });
            }
        }

        // ── POST /api/auth/login ───────────────────────────────
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var account = await _db.Accounts
                .Include(a => a.Customer)
                .Include(a => a.Admin)
                .FirstOrDefaultAsync(a => a.username == dto.Username);

            if (account == null || account.password != HashPassword(dto.Password))
                return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu không đúng." });

            var userId = account.isAdmin
                ? account.Admin?.userID
                : account.Customer?.userID;

            var name = account.isAdmin
                ? account.Admin?.name
                : account.Customer?.name;

            if (userId == null)
                return Unauthorized(new { message = "Tài khoản chưa được thiết lập." });

            var token = GenerateJwt(account, userId.Value, name ?? "");

            return Ok(new AuthResponseDto(
                Token: token,
                AccountId: account.accountID,
                UserId: userId.Value,
                Name: name ?? "",
                IsAdmin: account.isAdmin
            ));
        }

        // ── POST /api/auth/forgot-password ────────────────────
        // Hướng 2: xác thực username + email → đặt lại mật khẩu ngay
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username))
                return BadRequest(new { message = "Vui lòng nhập tên đăng nhập." });

            if (string.IsNullOrWhiteSpace(dto.Email) || !dto.Email.Contains('@'))
                return BadRequest(new { message = "Email không hợp lệ." });

            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 6)
                return BadRequest(new { message = "Mật khẩu mới phải có ít nhất 6 ký tự." });

            if (dto.NewPassword != dto.ConfirmPassword)
                return BadRequest(new { message = "Xác nhận mật khẩu không khớp." });

            var account = await _db.Accounts
                .FirstOrDefaultAsync(a => a.username == dto.Username);

            // Dùng thông báo chung để tránh lộ thông tin tài khoản
            if (account == null || !string.Equals(
                    account.email.Trim(),
                    dto.Email.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Tên đăng nhập hoặc email không đúng." });
            }

            // Không cho đặt lại bằng mật khẩu cũ
            if (account.password == HashPassword(dto.NewPassword))
                return BadRequest(new { message = "Mật khẩu mới không được trùng với mật khẩu hiện tại." });

            account.password = HashPassword(dto.NewPassword);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập lại." });
        }

        // ── GET /api/auth/me   [Authorize] ────────────────────
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetMe()
        {
            var accountId = GetAccountId();

            var account = await _db.Accounts
                .Include(a => a.Customer)
                .Include(a => a.Admin)
                .FirstOrDefaultAsync(a => a.accountID == accountId);

            if (account is null)
                return NotFound(new { message = "Không tìm thấy tài khoản." });

            if (account.isAdmin)
            {
                return Ok(new
                {
                    accountId = account.accountID,
                    username = account.username,
                    email = account.email,
                    name = account.Admin?.name ?? "",
                    isAdmin = true,
                    createdAt = account.createdAt
                });
            }

            return Ok(new
            {
                accountId = account.accountID,
                username = account.username,
                email = account.email,
                name = account.Customer?.name ?? "",
                address = account.Customer?.address ?? "",
                isAdmin = false,
                createdAt = account.createdAt
            });
        }

        // ── PUT /api/auth/me   [Authorize(Customer)] ──────────
        [HttpPut("me")]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "Họ tên không được để trống." });

            var accountId = GetAccountId();
            var userId = GetUserId();

            var account = await _db.Accounts
                .Include(a => a.Customer)
                .FirstOrDefaultAsync(a => a.accountID == accountId);

            if (account is null)
                return NotFound(new { message = "Không tìm thấy tài khoản." });

            var newEmail = dto.Email?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(newEmail) && newEmail != account.email)
            {
                if (await _db.Accounts.AnyAsync(a => a.email == newEmail && a.accountID != accountId))
                    return BadRequest(new { message = "Email này đã được sử dụng bởi tài khoản khác." });
                account.email = newEmail;
            }

            if (account.Customer is not null)
            {
                account.Customer.name = dto.Name.Trim();
                account.Customer.address = dto.Address?.Trim() ?? "";
            }

            await _db.SaveChangesAsync();

            var newToken = GenerateJwt(account, userId, account.Customer?.name ?? "");

            return Ok(new
            {
                message = "Cập nhật thông tin thành công.",
                token = newToken,
                name = account.Customer?.name ?? "",
                email = account.email,
                address = account.Customer?.address ?? ""
            });
        }

        // ── PUT /api/auth/me/change-password   [Authorize] ────
        [HttpPut("me/change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.CurrentPassword))
                return BadRequest(new { message = "Vui lòng nhập mật khẩu hiện tại." });

            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 6)
                return BadRequest(new { message = "Mật khẩu mới phải có ít nhất 6 ký tự." });

            if (dto.NewPassword != dto.ConfirmPassword)
                return BadRequest(new { message = "Xác nhận mật khẩu không khớp." });

            var accountId = GetAccountId();
            var account = await _db.Accounts.FindAsync(accountId);

            if (account is null)
                return NotFound(new { message = "Không tìm thấy tài khoản." });

            if (account.password != HashPassword(dto.CurrentPassword))
                return BadRequest(new { message = "Mật khẩu hiện tại không đúng." });

            account.password = HashPassword(dto.NewPassword);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Đổi mật khẩu thành công." });
        }
    }
}