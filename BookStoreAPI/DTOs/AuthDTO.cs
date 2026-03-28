namespace BookStoreAPI.DTOs
{
    public record LoginDto(string Username, string Password);

    public record AuthResponseDto(
        string Token,
        int AccountId,
        int UserId,
        string Name,
        bool IsAdmin
    );

    public record UpdateProfileDto(
        string Name,
        string? Email,
        string? Address
    );

    // OTP
    public record SendOtpDto(string Email);
    public record VerifyRegisterOtpDto(string Email, string Otp, string Username, string Password, string Name, string? Address);
    public record VerifyForgotOtpDto(string Email, string Otp, string NewPassword, string ConfirmPassword);
    public record SendChangePasswordOtpDto(string CurrentPassword);
    public record VerifyChangePasswordOtpDto(string Otp, string NewPassword, string ConfirmPassword);
}