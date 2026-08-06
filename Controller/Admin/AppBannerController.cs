using System.Text.Json;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller.Admin
{
    /// <summary>
    /// مدیریت بنرهای اپ موبایل در پنل ادمین
    /// </summary>
    [ApiController]
    [Route("api/Admin/[controller]")]
    [Authorize(Policy = "AdminOnly")]
    [Produces("application/json")]
    public class AppBannerController : VappControllerBase
    {
        private const long MaxBannerBodyBytes = (5L * 1024 * 1024) + (512L * 1024); // 5 MB + overhead

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IAdminAppBannerService _service;

        public AppBannerController(
            IAdminAppBannerService service,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<AppBannerResponseDto>>>> GetAll(
            [FromQuery] bool includeInactive = true)
        {
            var result = await _service.GetAllAsync(includeInactive);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<AppBannerResponseDto>>> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// به‌روزرسانی بنر — هم JSON و هم multipart را می‌پذیرد (بدون ModelState بایندینگ).
        /// </summary>
        [HttpPost("{id:int}/update")]
        [RequestSizeLimit(MaxBannerBodyBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxBannerBodyBytes)]
        public async Task<ActionResult<ApiResponse<AppBannerResponseDto>>> Update(int id)
        {
            UpdateAppBannerDto dto;
            IFormFile? imageFile = null;

            try
            {
                if (Request.HasFormContentType)
                {
                    var form = await Request.ReadFormAsync();
                    dto = new UpdateAppBannerDto
                    {
                        Title = form["Title"].ToString(),
                        Description = form["Description"].ToString(),
                        LinkType = form["LinkType"].ToString(),
                        LinkUrl = form["LinkUrl"].ToString(),
                        SortOrder = TryParseInt(form["SortOrder"]),
                        IsActive = TryParseBool(form["IsActive"], defaultValue: true),
                        ClearImage = TryParseBool(form["ClearImage"], defaultValue: false) == true
                    };
                    imageFile = form.Files.GetFile("ImageFile");
                }
                else
                {
                    using var reader = new StreamReader(Request.Body);
                    var raw = await reader.ReadToEndAsync();
                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        return StatusCode(
                            StatusCodes.Status400BadRequest,
                            ApiResponse<AppBannerResponseDto>.BadRequest(
                                "بدنه درخواست خالی است",
                                errorCode: ErrorCodes.ValidationFailed));
                    }

                    var parsed = JsonSerializer.Deserialize<UpdateAppBannerDto>(raw, JsonOptions);
                    if (parsed == null)
                    {
                        return StatusCode(
                            StatusCodes.Status400BadRequest,
                            ApiResponse<AppBannerResponseDto>.BadRequest(
                                "فرمت اطلاعات بنر نامعتبر است",
                                errorCode: ErrorCodes.ValidationFailed));
                    }

                    dto = parsed;
                }
            }
            catch (Exception)
            {
                return StatusCode(
                    StatusCodes.Status400BadRequest,
                    ApiResponse<AppBannerResponseDto>.BadRequest(
                        "خواندن اطلاعات بنر ناموفق بود. صفحه را تازه کنید و دوباره ذخیره کنید",
                        errorCode: ErrorCodes.ValidationFailed));
            }

            var result = await _service.UpdateAsync(id, dto);
            if (!result.Success)
                return StatusCode(result.StatusCode, result);

            if (imageFile != null && imageFile.Length > 0)
            {
                var imageResult = await _service.UpdateImageAsync(id, imageFile, clearImage: false);
                return StatusCode(imageResult.StatusCode, imageResult);
            }

            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// آپلود یا حذف تصویر بنر (فقط فایل).
        /// </summary>
        [HttpPost("{id:int}/image")]
        [RequestSizeLimit(MaxBannerBodyBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxBannerBodyBytes)]
        public async Task<ActionResult<ApiResponse<AppBannerResponseDto>>> UpdateImage(int id)
        {
            if (!Request.HasFormContentType)
            {
                return StatusCode(
                    StatusCodes.Status400BadRequest,
                    ApiResponse<AppBannerResponseDto>.BadRequest(
                        "برای آپلود تصویر باید فرم multipart ارسال شود",
                        errorCode: ErrorCodes.ValidationFailed));
            }

            IFormCollection form;
            try
            {
                form = await Request.ReadFormAsync();
            }
            catch (Exception)
            {
                return StatusCode(
                    StatusCodes.Status400BadRequest,
                    ApiResponse<AppBannerResponseDto>.BadRequest(
                        "خواندن فایل تصویر ناموفق بود",
                        errorCode: ErrorCodes.ValidationFailed));
            }

            var clearImage = TryParseBool(form["ClearImage"], defaultValue: false) == true;
            var imageFile = form.Files.GetFile("ImageFile");

            var result = await _service.UpdateImageAsync(id, imageFile, clearImage);
            return StatusCode(result.StatusCode, result);
        }

        private static int? TryParseInt(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;
            return int.TryParse(raw.Trim(), out var value) ? value : null;
        }

        private static bool? TryParseBool(string? raw, bool defaultValue)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return defaultValue;

            var v = raw.Trim();
            if (bool.TryParse(v, out var parsed))
                return parsed;
            if (v == "1" || v.Equals("on", StringComparison.OrdinalIgnoreCase) || v.Equals("yes", StringComparison.OrdinalIgnoreCase))
                return true;
            if (v == "0" || v.Equals("off", StringComparison.OrdinalIgnoreCase) || v.Equals("no", StringComparison.OrdinalIgnoreCase))
                return false;

            return defaultValue;
        }
    }
}
