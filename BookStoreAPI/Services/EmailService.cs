using MailKit.Net.Smtp;
using MimeKit;

namespace BookStoreAPI.Services
{
    public class EmailService
    {
        private readonly IConfiguration _cfg;

        public EmailService(IConfiguration cfg)
        {
            _cfg = cfg;
        }

        public async Task SendOtpAsync(string toEmail, string otp, string purpose)
        {
            var subject = purpose switch
            {
                "register" => "Xác nhận đăng ký tài khoản",
                "forgot" => "Đặt lại mật khẩu",
                "change" => "Xác nhận đổi mật khẩu",
                _ => "Mã OTP"
            };

            var body = $"""
                <h2>Mã OTP của bạn</h2>
                <p>Mã OTP: <strong style="font-size:24px">{otp}</strong></p>
                <p>Mã có hiệu lực trong <strong>5 phút</strong>.</p>
                <p>Nếu bạn không yêu cầu, hãy bỏ qua email này.</p>
                """;

            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(_cfg["Email:From"]));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = body };

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_cfg["Email:Host"], int.Parse(_cfg["Email:Port"]!),
                MailKit.Security.SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_cfg["Email:Username"], _cfg["Email:Password"]);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }
    }
}