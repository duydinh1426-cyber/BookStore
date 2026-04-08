using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace BookStoreAPI.Services
{
    /// <summary>
    /// Tích hợp VNPay — tạo URL thanh toán và xác thực chữ ký callback.
    /// Cấu hình trong appsettings.json (mục "VNPay").
    /// </summary>
    public class VNPayService
    {
        private readonly IConfiguration _cfg;
        private readonly IHttpContextAccessor _httpCtx;

        public VNPayService(IConfiguration cfg, IHttpContextAccessor httpCtx)
        {
            _cfg = cfg;
            _httpCtx = httpCtx;
        }

        // ──────────────────────────────────────────────────
        // Tạo URL redirect sang trang thanh toán VNPay
        // ──────────────────────────────────────────────────
        public string CreatePaymentUrl(int orderId, decimal amount, string orderInfo)
        {
            var vnp = _cfg.GetSection("VNPay");

            var tick = DateTime.UtcNow.AddHours(7); // VNPay yêu cầu giờ VN (UTC+7)

            var vnpParams = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["vnp_Version"] = "2.1.0",
                ["vnp_Command"] = "pay",
                ["vnp_TmnCode"] = vnp["TmnCode"]!,
                ["vnp_Amount"] = ((long)(amount * 100)).ToString(),   // VNPay nhân 100
                ["vnp_CurrCode"] = "VND",
                ["vnp_TxnRef"] = $"{orderId}_{tick:yyyyMMddHHmmss}",  // unique ref
                ["vnp_OrderInfo"] = orderInfo,
                ["vnp_OrderType"] = "other",
                ["vnp_Locale"] = "vn",
                ["vnp_ReturnUrl"] = vnp["ReturnUrl"]!,
                ["vnp_IpAddr"] = GetClientIp(),
                ["vnp_CreateDate"] = tick.ToString("yyyyMMddHHmmss"),
                ["vnp_ExpireDate"] = tick.AddMinutes(15).ToString("yyyyMMddHHmmss"),
            };

            var query = BuildQuery(vnpParams);
            var secureHash = HmacSha512(vnp["HashSecret"]!, query);

            return $"{vnp["PayUrl"]}?{query}&vnp_SecureHash={secureHash}";
        }

        // ──────────────────────────────────────────────────
        // Xác thực chữ ký từ VNPay gửi về (IPN / ReturnUrl)
        // Trả về (isValid, orderId, responseCode, txnRef)
        // ──────────────────────────────────────────────────
        public (bool isValid, int orderId, string responseCode, string txnRef)
            ValidateCallback(IQueryCollection query)
        {
            var hashSecret = _cfg["VNPay:HashSecret"]!;

            // Lấy secure hash do VNPay gửi về
            var receivedHash = query["vnp_SecureHash"].ToString();

            // Build lại query KHÔNG có vnp_SecureHash / vnp_SecureHashType
            var filtered = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in query)
            {
                if (kv.Key == "vnp_SecureHash" || kv.Key == "vnp_SecureHashType") continue;
                filtered[kv.Key] = kv.Value.ToString();
            }

            var rawData = BuildQuery(filtered);
            var computed = HmacSha512(hashSecret, rawData);

            bool isValid = computed.Equals(receivedHash, StringComparison.OrdinalIgnoreCase);

            // txnRef = "{orderId}_{timestamp}"
            var txnRef = query["vnp_TxnRef"].ToString();
            var orderId = int.TryParse(txnRef.Split('_')[0], out var oid) ? oid : 0;
            var rspCode = query["vnp_ResponseCode"].ToString();

            return (isValid, orderId, rspCode, txnRef);
        }

        // ──────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────
        private static string BuildQuery(SortedDictionary<string, string> pars)
        {
            var sb = new StringBuilder();
            foreach (var kv in pars)
            {
                if (string.IsNullOrEmpty(kv.Value)) continue;
                if (sb.Length > 0) sb.Append('&');
                sb.Append(WebUtility.UrlEncode(kv.Key));
                sb.Append('=');
                sb.Append(WebUtility.UrlEncode(kv.Value));
            }
            return sb.ToString();
        }

        private static string HmacSha512(string key, string data)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var dataBytes = Encoding.UTF8.GetBytes(data);
            using var hmac = new HMACSHA512(keyBytes);
            var hash = hmac.ComputeHash(dataBytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

        private string GetClientIp()
        {
            var ctx = _httpCtx.HttpContext;
            if (ctx is null) return "127.0.0.1";

            // Lấy IP qua X-Forwarded-For nếu đứng sau proxy/nginx
            var forwarded = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwarded))
                return forwarded.Split(',')[0].Trim();

            return ctx.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        }
    }
}