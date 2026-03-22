namespace BookStoreAPI.DTOs
{
    public record RegisterDto(
        string Username,
        string Password,
        string Email,
        string Name,
        string? Address
    );

    public record LoginDto(string Username, string Password);

    public record AuthResponseDto(
        string Token,
        int AccountId,
        int UserId,
        string Name,
        bool IsAdmin
    );

    /// <summary>Cập nhật thông tin cá nhân (Customer)</summary>
    public record UpdateProfileDto(
        string Name,
        string? Email,
        string? Address
    );

    /// <summary>Đổi mật khẩu (đã đăng nhập)</summary>
    public record ChangePasswordDto(
        string CurrentPassword,
        string NewPassword,
        string ConfirmPassword
    );

    /// <summary>
    /// Quên mật khẩu – Hướng 2:
    /// Xác thực bằng username + email khớp trong DB
    /// rồi đặt lại mật khẩu mới ngay, không cần gửi mail.
    /// </summary>
    public record ForgotPasswordDto(
        string Username,
        string Email,
        string NewPassword,
        string ConfirmPassword
    );
}