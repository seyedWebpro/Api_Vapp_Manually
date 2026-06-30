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
        /// ثبت نوبت توسط مشتری
        /// </summary>
        [HttpPost("{slug}/book")]
        [ProducesResponseType(typeof(ApiResponse<CreatePublicBookingResponseDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<CreatePublicBookingResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<CreatePublicBookingResponseDto>>> Book(
            string slug,
            [FromBody] CreatePublicBookingDto dto)
        {
            var invalid = InvalidModelStateResponse<CreatePublicBookingResponseDto>();
            if (invalid != null)
            {
                return invalid;
            }

            var result = await _appointmentService.CreatePublicBookingAsync(slug, dto);
            return StatusCode(result.StatusCode, result);
        }
    }
}
