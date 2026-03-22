using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace BookStoreAPI.Services
{
    public class JwtService
    {
        private readonly IConfiguration _cfg;

        public JwtService(IConfiguration cfg) => _cfg = cfg;

        // Thêm userId (Customers.userID) vào tham số
        public string GenerateToken(int accountId, int userId, string username, string role)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_cfg["Jwt:SecretKey"]!); // ✅ đọc từ config

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, accountId.ToString()), // accountId
                new Claim("userId", userId.ToString()),                      // Customers.userID
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(2),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}