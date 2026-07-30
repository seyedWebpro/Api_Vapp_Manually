using Api_Vapp.DTOs.BusinessCard;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.User;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Api_Vapp.Attributes;
using Api_Vapp.Constants;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// کنترلر مدیریت کارت ویزیت دیجیتال
    /// </summary>
    /// <remarks>
    /// فلو مشابه فرم‌ساز: پیش‌نویس → اطلاعات اصلی → بخش‌ها → انتشار و دریافت لینک عمومی.
    /// قالب‌ها سمت کلاینت (Flutter) مدیریت می‌شوند و بکند فقط پیکربندی نهایی را ذخیره می‌کند.
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [RequireSubscriptionFeature(SubscriptionFeatureCodes.BusinessCard)]
    [Produces("application/json")]
    public class BusinessCardController : VappControllerBase
    {
        private readonly IBusinessCardService _businessCardService;

        public BusinessCardController(
            IBusinessCardService businessCardService,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _businessCardService = businessCardService;
        }

        /// <summary>
        /// ایجاد پیش‌نویس کارت ویزیت
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<BusinessCardResponseDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<BusinessCardResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<BusinessCardResponseDto>>> CreateDraft([FromBody] CreateBusinessCardDto? createDto)
        {
            if (createDto == null)
            {
                return StatusCode(400, ApiResponse<BusinessCardResponseDto>.BadRequest(
                    "داده‌های ورودی نامعتبر است",
                    errorCode: ErrorCodes.ValidationFailed));
            }

            var invalid = InvalidModelStateResponse<BusinessCardResponseDto>();
            if (invalid != null)
            {
                return invalid;
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _businessCardService.CreateDraftAsync(userId, createDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت لیست کارت‌های ویزیت کاربر
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<BusinessCardListResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<BusinessCardListResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<BusinessCardListResponseDto>>> GetCards(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _businessCardService.GetCardsAsync(userId, pageNumber, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت جزئیات کارت ویزیت
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<BusinessCardResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<BusinessCardResponseDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<BusinessCardResponseDto>>> GetById(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _businessCardService.GetByIdAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// به‌روزرسانی اطلاعات اصلی (نام، لوگو، slug)
        /// </summary>
        [HttpPost("{id}/update-info")]
        [ProducesResponseType(typeof(ApiResponse<BusinessCardResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<BusinessCardResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<BusinessCardResponseDto>>> UpdateInfo(int id, [FromBody] UpdateBusinessCardInfoDto? updateDto)
        {
            if (updateDto == null)
            {
                return StatusCode(400, ApiResponse<BusinessCardResponseDto>.BadRequest(
                    "هیچ موردی برای به‌روزرسانی ارسال نشده است",
                    errorCode: ErrorCodes.ValidationFailed));
            }

            var invalid = InvalidModelStateResponse<BusinessCardResponseDto>();
            if (invalid != null)
            {
                return invalid;
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _businessCardService.UpdateInfoAsync(id, userId, updateDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// به‌روزرسانی بخش‌های کارت (اسلایدر، توضیحات، تعرفه، نقشه، تماس)
        /// </summary>
        [HttpPost("{id}/update-sections")]
        [ProducesResponseType(typeof(ApiResponse<BusinessCardResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<BusinessCardResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<BusinessCardResponseDto>>> UpdateSections(int id, [FromBody] UpdateBusinessCardSectionsDto? updateDto)
        {
            if (updateDto == null)
            {
                return StatusCode(400, ApiResponse<BusinessCardResponseDto>.BadRequest(
                    "هیچ موردی برای به‌روزرسانی ارسال نشده است",
                    errorCode: ErrorCodes.ValidationFailed));
            }

            var invalid = InvalidModelStateResponse<BusinessCardResponseDto>();
            if (invalid != null)
            {
                return invalid;
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _businessCardService.UpdateSectionsAsync(id, userId, updateDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// آپلود تصویر کارت ویزیت (لوگو / اسلایدر / تعرفه)
        /// </summary>
        /// <remarks>
        /// الگوی مشابه Contact/User:
        /// - multipart/form-data
        /// - فیلد فایل: imageFile
        /// - imageType اختیاری: logo | slider | service | image
        /// - برای logo مسیر در DB ذخیره می‌شود (جایگزین لوگوی قبلی)
        /// - برای slider/service فقط URL برمی‌گردد؛ کلاینت در update-sections ذخیره می‌کند
        /// </remarks>
        [HttpPost("{id}/upload-image")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<string>>> UploadImage(int id, [FromForm] UploadBusinessCardImageDto dto)
        {
            if (dto == null || dto.ImageFile == null)
            {
                return StatusCode(400, ApiResponse<string>.BadRequest(
                    "فایل عکس ارسال نشده است. لطفاً یک فایل تصویری انتخاب کنید",
                    errorCode: ErrorCodes.ValidationFailed));
            }

            if (!ModelState.IsValid)
            {
                var errors = ExtractModelStateErrors();
                return StatusCode(400, ApiResponse<string>.BadRequest("داده‌های ورودی نامعتبر است", errors));
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _businessCardService.UploadImageAsync(id, userId, dto.ImageFile, dto.ImageType);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// انتشار کارت ویزیت و دریافت لینک عمومی
        /// </summary>
        [HttpPost("{id}/publish")]
        [ProducesResponseType(typeof(ApiResponse<BusinessCardResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<BusinessCardResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<BusinessCardResponseDto>>> Publish(int id, [FromBody] PublishBusinessCardDto? publishDto = null)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _businessCardService.PublishAsync(id, userId, publishDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// حذف نرم کارت ویزیت
        /// </summary>
        [HttpPost("{id}/delete")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _businessCardService.DeleteAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// فعال/غیرفعال کردن کارت منتشرشده
        /// </summary>
        [HttpPost("{id}/toggle-active")]
        [HttpPost("{id}/toggle-status")]
        [ProducesResponseType(typeof(ApiResponse<BusinessCardResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<BusinessCardResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<BusinessCardResponseDto>>> SetActiveStatus(
            int id,
            [FromBody] ToggleActiveDto? statusDto)
        {
            if (statusDto == null)
            {
                return StatusCode(400, ApiResponse<BusinessCardResponseDto>.BadRequest(
                    "مقدار isActive الزامی است",
                    errorCode: ErrorCodes.ValidationFailed));
            }

            var invalid = InvalidModelStateResponse<BusinessCardResponseDto>();
            if (invalid != null)
            {
                return invalid;
            }

            var userId = await GetCurrentUserIdAsync();
            var result = await _businessCardService.SetActiveStatusAsync(id, userId, statusDto.IsActive);
            return StatusCode(result.StatusCode, result);
        }
    }
}
