using System.Globalization;
using System.Text.Json;
using Api_Vapp.DTOs.BookingSystem;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// API عمومی رزرو نوبت — بدون احراز هویت
    /// </summary>
    [ApiController]
    [Route("api/BookingPublic")]
    [AllowAnonymous]
    [Produces("application/json")]
    public class BookingPublicController : VappControllerBase
    {
        private const long MaxBookBodyBytes = 11 * 1024 * 1024; // ۱۰ مگ فایل + سربار multipart

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IBookingAppointmentService _appointmentService;

        public BookingPublicController(
            IBookingAppointmentService appointmentService,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _appointmentService = appointmentService;
        }

        /// <summary>
        /// دریافت اطلاعات عمومی صفحه رزرو
        /// </summary>
        [HttpGet("{slug}")]
        [ProducesResponseType(typeof(ApiResponse<BookingPublicSystemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<BookingPublicSystemDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<BookingPublicSystemDto>>> GetSystem(string slug)
        {
            var result = await _appointmentService.GetPublicSystemAsync(slug);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت اسلات‌های خالی یک روز
        /// </summary>
        [HttpGet("{slug}/services/{serviceId}/slots")]
        [ProducesResponseType(typeof(ApiResponse<BookingAvailableSlotsDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<BookingAvailableSlotsDto>>> GetSlots(
            string slug,
            int serviceId,
            [FromQuery] DateOnly date)
        {
            var result = await _appointmentService.GetAvailableSlotsAsync(slug, serviceId, date);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ثبت نوبت توسط مشتری — JSON یا multipart (فیش اختیاری)
        /// </summary>
        [HttpPost("{slug}/book")]
        [RequestSizeLimit(MaxBookBodyBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxBookBodyBytes)]
        [ProducesResponseType(typeof(ApiResponse<CreatePublicBookingResponseDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<CreatePublicBookingResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<CreatePublicBookingResponseDto>>> Book(string slug)
        {
            CreatePublicBookingDto dto;
            IFormFile? paymentReceiptFile = null;

            if (Request.HasFormContentType)
            {
                IFormCollection form;
                try
                {
                    form = await Request.ReadFormAsync();
                }
                catch (Exception)
                {
                    return StatusCode(
                        StatusCodes.Status400BadRequest,
                        ApiResponse<CreatePublicBookingResponseDto>.BadRequest(
                            "خواندن اطلاعات رزرو ناموفق بود. صفحه را تازه کنید و دوباره تلاش کنید",
                            errorCode: ErrorCodes.ValidationFailed));
                }

                if (!TryParseFormInt(form, out var serviceId, "ServiceId", "serviceId") || serviceId <= 0)
                {
                    return StatusCode(
                        StatusCodes.Status400BadRequest,
                        ApiResponse<CreatePublicBookingResponseDto>.BadRequest(
                            "داده‌های ورودی نامعتبر است",
                            errors: new List<string> { "شناسه خدمت الزامی است" },
                            errorCode: ErrorCodes.ValidationFailed));
                }

                if (!TryParseFormDateTime(form, out var startUtc, "StartUtc", "startUtc"))
                {
                    return StatusCode(
                        StatusCodes.Status400BadRequest,
                        ApiResponse<CreatePublicBookingResponseDto>.BadRequest(
                            "داده‌های ورودی نامعتبر است",
                            errors: new List<string> { "زمان نوبت الزامی است" },
                            errorCode: ErrorCodes.ValidationFailed));
                }

                var fullName = GetFormValue(form, "CustomerFullName", "customerFullName");
                var mobile = GetFormValue(form, "CustomerMobile", "customerMobile");
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    return StatusCode(
                        StatusCodes.Status400BadRequest,
                        ApiResponse<CreatePublicBookingResponseDto>.BadRequest(
                            "داده‌های ورودی نامعتبر است",
                            errors: new List<string> { "نام الزامی است" },
                            errorCode: ErrorCodes.ValidationFailed));
                }

                if (string.IsNullOrWhiteSpace(mobile))
                {
                    return StatusCode(
                        StatusCodes.Status400BadRequest,
                        ApiResponse<CreatePublicBookingResponseDto>.BadRequest(
                            "داده‌های ورودی نامعتبر است",
                            errors: new List<string> { "شماره موبایل الزامی است" },
                            errorCode: ErrorCodes.ValidationFailed));
                }

                dto = new CreatePublicBookingDto
                {
                    ServiceId = serviceId,
                    StartUtc = startUtc,
                    CustomerFullName = fullName,
                    CustomerMobile = mobile,
                    CustomerNote = NullIfWhiteSpace(GetFormValue(form, "CustomerNote", "customerNote")),
                    RemindersEnabled = TryParseFormBool(form, "RemindersEnabled", "remindersEnabled")
                };

                paymentReceiptFile =
                    form.Files.GetFile("PaymentReceiptFile")
                    ?? form.Files.GetFile("paymentReceiptFile");
            }
            else
            {
                try
                {
                    using var reader = new StreamReader(Request.Body);
                    var raw = await reader.ReadToEndAsync();
                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        return StatusCode(
                            StatusCodes.Status400BadRequest,
                            ApiResponse<CreatePublicBookingResponseDto>.BadRequest(
                                "بدنه درخواست خالی است",
                                errorCode: ErrorCodes.ValidationFailed));
                    }

                    var parsed = JsonSerializer.Deserialize<CreatePublicBookingDto>(raw, JsonOptions);
                    if (parsed == null)
                    {
                        return StatusCode(
                            StatusCodes.Status400BadRequest,
                            ApiResponse<CreatePublicBookingResponseDto>.BadRequest(
                                "فرمت اطلاعات رزرو نامعتبر است",
                                errorCode: ErrorCodes.ValidationFailed));
                    }

                    dto = parsed;
                }
                catch (Exception)
                {
                    return StatusCode(
                        StatusCodes.Status400BadRequest,
                        ApiResponse<CreatePublicBookingResponseDto>.BadRequest(
                            "خواندن اطلاعات رزرو ناموفق بود. صفحه را تازه کنید و دوباره تلاش کنید",
                            errorCode: ErrorCodes.ValidationFailed));
                }

                TryValidateModel(dto);
                var invalid = InvalidModelStateResponse<CreatePublicBookingResponseDto>();
                if (invalid != null)
                {
                    return invalid;
                }
            }

            var result = await _appointmentService.CreatePublicBookingAsync(slug, dto, paymentReceiptFile);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// استعلام وضعیت نوبت با شماره نوبت + موبایل
        /// </summary>
        [HttpPost("{slug}/status")]
        [ProducesResponseType(typeof(ApiResponse<PublicBookingStatusDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<PublicBookingStatusDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<PublicBookingStatusDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<PublicBookingStatusDto>>> LookupStatus(
            string slug,
            [FromBody] LookupPublicBookingDto dto)
        {
            var invalid = InvalidModelStateResponse<PublicBookingStatusDto>();
            if (invalid != null)
            {
                return invalid;
            }

            var result = await _appointmentService.LookupPublicBookingStatusAsync(slug, dto);
            return StatusCode(result.StatusCode, result);
        }

        private static string GetFormValue(IFormCollection form, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (form.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value.ToString().Trim();
                }
            }

            return string.Empty;
        }

        private static string? NullIfWhiteSpace(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static bool TryParseFormInt(IFormCollection form, out int value, params string[] keys)
        {
            value = 0;
            var raw = GetFormValue(form, keys);
            return !string.IsNullOrWhiteSpace(raw) &&
                   int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryParseFormDateTime(IFormCollection form, out DateTime value, params string[] keys)
        {
            value = default;
            var raw = GetFormValue(form, keys);
            return !string.IsNullOrWhiteSpace(raw) &&
                   DateTime.TryParse(
                       raw,
                       CultureInfo.InvariantCulture,
                       DateTimeStyles.RoundtripKind | DateTimeStyles.AllowWhiteSpaces,
                       out value);
        }

        private static bool? TryParseFormBool(IFormCollection form, params string[] keys)
        {
            var raw = GetFormValue(form, keys);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            if (bool.TryParse(raw, out var boolValue))
            {
                return boolValue;
            }

            if (raw is "1" or "0")
            {
                return raw == "1";
            }

            return null;
        }
    }
}
