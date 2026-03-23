using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStoreAPI.Controllers
{
    [ApiController]
    [Route("api/upload")]
    [Authorize(Roles = "Admin")]
    public class UploadController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public UploadController(IWebHostEnvironment env) => _env = env;

        // POST /api/upload/image
        [HttpPost("image")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Vui lòng chọn file ảnh." });

            // Kiểm tra định dạng
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowed.Contains(ext))
                return BadRequest(new { message = "Chỉ chấp nhận JPG, PNG, WEBP, GIF." });

            // Giới hạn 5MB
            if (file.Length > 5 * 1024 * 1024)
                return BadRequest(new { message = "Ảnh không được vượt quá 5MB." });

            // Lưu vào wwwroot/images
            var imagesFolder = Path.Combine(_env.WebRootPath, "images");
            if (!Directory.Exists(imagesFolder))
                Directory.CreateDirectory(imagesFolder);

            // Tên file unique để tránh trùng
            var fileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(imagesFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
                await file.CopyToAsync(stream);

            return Ok(new
            {
                message = "Upload thành công.",
                fileName = fileName,
                url = $"/images/{fileName}"
            });
        }
    }
}