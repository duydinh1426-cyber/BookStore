/*
 * POST - /api/auth/register/send-otp
 * POST - /api/auth/register/verify-otp
 * POST - /api/auth/login
 * PUT - /api/auth/me
 * POST - /api/auth/forgot-password/send-otp
 * POST - /api/auth/forgot-password/verify-otp
 * POST - /api/auth/me/change-password/send-otp
 * POST - /api/auth/me/change-password/verify-otp 
 */

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
using BookStoreAPI.Services;

namespace BookStoreAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _cfg;
        private readonly EmailService _email;
        private readonly OtpService _otp;

        public AuthController(AppDbContext db, IConfiguration cfg,
                              EmailService email, OtpService otp)
        {
            _db = db;
            _cfg = cfg;
            _email = email;
            _otp = otp;
        }

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
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg["Jwt:SecretKey"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("accountId",               account.accountID.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name,           name),
                new Claim(ClaimTypes.Email,          account.email ?? ""),
                new Claim(ClaimTypes.Role,           account.isAdmin ? "Admin" : "Customer")
            };

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /*
         Đăng ký tài khoản 
         B1: Gửi OTP
         POST - /api/auth/register/send-otp
        */
        [HttpPost("register/send-otp")]
        public async Task<IActionResult> RegisterSendOtp([FromBody] SendOtpDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || !dto.Email.Contains('@'))
                return BadRequest(new { message = "Email không hợp lệ." });

            if (await _db.Accounts.AnyAsync(a => a.email == dto.Email))
                return BadRequest(new { message = "Email đã được sử dụng." });

            try
            {
                var otp = _otp.GenerateOtp($"register:{dto.Email}");
                await _email.SendOtpAsync(dto.Email, otp, "register");
                return Ok(new { message = "Đã gửi OTP về email." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi gửi email: {ex.Message}" });
            }
        }

        /* 
         Đăng ký tài khoản 
         B2: Xác thực OTP + Tạo tài khoản
         POST - /api/auth/register/verify-otp
        */
        [HttpPost("register/verify-otp")]
        public async Task<IActionResult> RegisterVerifyOtp([FromBody] VerifyRegisterOtpDto dto)
        {
            if (!_otp.VerifyOtp($"register:{dto.Email}", dto.Otp))
                return BadRequest(new { message = "OTP không hợp lệ hoặc đã hết hạn." });

            if (await _db.Accounts.AnyAsync(a => a.username == dto.Username))
                return BadRequest(new { message = "Tên đăng nhập đã tồn tại." });

            if (dto.Password.Length < 6)
                return BadRequest(new { message = "Mật khẩu phải có ít nhất 6 ký tự." });

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

                _db.Customers.Add(new Customer
                {
                    accountID = account.accountID,
                    name = dto.Name,
                    address = dto.Address
                });
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

        /* 
         Đăng nhập
         POST - /api/auth/login
        */
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var account = await _db.Accounts
                .Include(a => a.Customer)
                .Include(a => a.Admin)
                .FirstOrDefaultAsync(a => a.username == dto.Username);

            if (account == null || account.password != HashPassword(dto.Password))
                return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu không đúng." });

            var userId = account.isAdmin ? account.Admin?.userID : account.Customer?.userID;
            var name = account.isAdmin ? account.Admin?.name : account.Customer?.name;

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

        /* 
         Quên mật khẩu
         B1: Gửi OTP
         POST - /api/auth/forgot-password/send-otp
        */
        [HttpPost("forgot-password/send-otp")]
        public async Task<IActionResult> ForgotSendOtp([FromBody] SendOtpDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || !dto.Email.Contains('@'))
                return BadRequest(new { message = "Email không hợp lệ." });

            var account = await _db.Accounts
                .FirstOrDefaultAsync(a => a.email == dto.Email);

            if (account != null)
            {
                var otp = _otp.GenerateOtp($"forgot:{dto.Email}");
                await _email.SendOtpAsync(dto.Email, otp, "forgot");
            }

            return Ok(new { message = "Nếu email tồn tại, OTP đã được gửi." });
        }

        /* 
         Quên mật khẩu
         B2: Xác thực OTP + Đặt mật khẩu mới
         POST - /api/auth/forgot-password/verify-otp
        */
        [HttpPost("forgot-password/verify-otp")]
        public async Task<IActionResult> ForgotVerifyOtp([FromBody] VerifyForgotOtpDto dto)
        {
            if (!_otp.VerifyOtp($"forgot:{dto.Email}", dto.Otp))
                return BadRequest(new { message = "OTP không hợp lệ hoặc đã hết hạn." });

            if (dto.NewPassword.Length < 6)
                return BadRequest(new { message = "Mật khẩu phải có ít nhất 6 ký tự." });

            if (dto.NewPassword != dto.ConfirmPassword)
                return BadRequest(new { message = "Xác nhận mật khẩu không khớp." });

            var account = await _db.Accounts
                .FirstOrDefaultAsync(a => a.email == dto.Email);

            if (account == null)
                return BadRequest(new { message = "Email không tồn tại." });

            if (account.password == HashPassword(dto.NewPassword))
                return BadRequest(new { message = "Mật khẩu mới không được trùng mật khẩu cũ." });

            account.password = HashPassword(dto.NewPassword);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập lại." });
        }

        /* 
         Xem thong tin cá nhân
         GET - /api/auth/me
        */
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
                return Ok(new
                {
                    accountId = account.accountID,
                    username = account.username,
                    email = account.email,
                    name = account.Admin?.name ?? "",
                    isAdmin = true,
                    createdAt = account.createdAt
                });

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

        /* 
         Cập nhật thông tin cá nhân
         PUT - /api/auth/me
        */
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

        /* 
         Đổi mật khẩu 
         B1: Xác nhận mật khẩu cũ + Gửi OTP
         POST - /api/auth/me/change-password/send-otp
        */
        [HttpPost("me/change-password/send-otp")]
        [Authorize]
        public async Task<IActionResult> ChangeSendOtp([FromBody] SendChangePasswordOtpDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.CurrentPassword))
                return BadRequest(new { message = "Vui lòng nhập mật khẩu hiện tại." });

            var accountId = GetAccountId();
            var account = await _db.Accounts.FindAsync(accountId);

            if (account is null)
                return NotFound(new { message = "Không tìm thấy tài khoản." });

            if (account.password != HashPassword(dto.CurrentPassword))
                return BadRequest(new { message = "Mật khẩu hiện tại không đúng." });

            if (string.IsNullOrWhiteSpace(account.email))
                return BadRequest(new { message = "Tài khoản chưa có email." });

            var otp = _otp.GenerateOtp($"change:{accountId}");
            await _email.SendOtpAsync(account.email, otp, "change");

            return Ok(new { message = "Đã gửi OTP về email." });
        }

        /* 
         Đổi mật khẩu
         B2: Xác thực OTP + Đặt mật khẩu mới
         POST - /api/auth/me/change-password/verify-otp
        */
        [HttpPut("me/change-password/verify-otp")]
        [Authorize]
        public async Task<IActionResult> ChangeVerifyOtp([FromBody] VerifyChangePasswordOtpDto dto)
        {
            var accountId = GetAccountId();

            if (!_otp.VerifyOtp($"change:{accountId}", dto.Otp))
                return BadRequest(new { message = "OTP không hợp lệ hoặc đã hết hạn." });

            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 6)
                return BadRequest(new { message = "Mật khẩu mới phải có ít nhất 6 ký tự." });

            if (dto.NewPassword != dto.ConfirmPassword)
                return BadRequest(new { message = "Xác nhận mật khẩu không khớp." });

            var account = await _db.Accounts.FindAsync(accountId);
            if (account is null)
                return NotFound(new { message = "Không tìm thấy tài khoản." });

            if (account.password == HashPassword(dto.NewPassword))
                return BadRequest(new { message = "Mật khẩu mới không được trùng mật khẩu cũ." });

            account.password = HashPassword(dto.NewPassword);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Đổi mật khẩu thành công." });
        }
    }
}