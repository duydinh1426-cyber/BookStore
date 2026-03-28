using Microsoft.Extensions.Caching.Memory;

namespace BookStoreAPI.Services
{
    public class OtpService
    {
        private readonly IMemoryCache _cache;
        private static readonly Random _rng = new();

        public OtpService(IMemoryCache cache)
        {
            _cache = cache;
        }

        // Tạo và lưu OTP vào cache 5 phút
        public string GenerateOtp(string key)
        {
            var otp = _rng.Next(100000, 999999).ToString();
            _cache.Set(CacheKey(key), otp, TimeSpan.FromMinutes(5));
            return otp;
        }

        // Xác minh OTP — đúng thì xóa luôn
        public bool VerifyOtp(string key, string otp)
        {
            if (!_cache.TryGetValue(CacheKey(key), out string? stored))
                return false;

            if (stored != otp) return false;

            _cache.Remove(CacheKey(key));
            return true;
        }

        private static string CacheKey(string key) => $"otp:{key}";
    }
}