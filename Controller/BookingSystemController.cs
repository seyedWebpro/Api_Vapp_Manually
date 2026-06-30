using Api_Vapp.Attributes;
using Api_Vapp.Constants;
using Api_Vapp.DTOs.BookingSystem;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// کنترلر سیستم رزرو نوبت
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [RequireSubscriptionFeature(SubscriptionFeatureCodes.OnlineBooking)]
    [Produces("application/json")]
    public class BookingSystemController : VappControllerBase
    {
        private readonly IBookingSystemService _bookingSystemService;
        private readonly IBookingAppointmentService _appointmentService;

        public BookingSystemController(
            IBookingSystemService bookingSystemService,
            IBookingAppointmentService appointmentService,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _bookingSystemService = bookingSystemService;
            _appointmentService = appointmentService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<BookingSystemListDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<BookingSystemListDto>>> GetSystems(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] bool? isActive = null)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _bookingSystemService.GetSystemsAsync(userId, pageNumber, pageSize, isActive);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<BookingSystemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<BookingSystemDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<BookingSystemDto>>> GetById(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _bookingSystemService.GetByIdAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id}/toggle-status")]
        [ProducesResponseType(typeof(ApiResponse<BookingSystemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<BookingSystemDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<BookingSystemDto>>> ToggleStatus(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _bookingSystemService.ToggleStatusAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id}/delete")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _bookingSystemService.DeleteAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id}/update")]
        [ProducesResponseType(typeof(ApiResponse<BookingSystemDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<BookingSystemDto>>> Update(int id, [FromBody] UpdateBookingSystemDto updateDto)
        {
            var invalid = InvalidModelStateResponse<BookingSystemDto>();
            if (invalid != null) return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _bookingSystemService.UpdateAsync(id, userId, updateDto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("notebooks")]
        [ProducesResponseType(typeof(ApiResponse<List<BookingNotebookDto>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<List<BookingNotebookDto>>>> GetNotebooks()
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _bookingSystemService.GetNotebooksAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("activity-types")]
        [ProducesResponseType(typeof(ApiResponse<List<BookingActivityTypeDto>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<List<BookingActivityTypeDto>>>> GetActivityTypes()
        {
            var result = await _bookingSystemService.GetActivityTypesAsync();
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("validate-step1")]
        [ProducesResponseType(typeof(ApiResponse<BookingStep1ValidationResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<BookingStep1ValidationResponseDto>>> ValidateStep1([FromBody] BookingStep1Dto step1Dto)
        {
            var invalid = InvalidModelStateResponse<BookingStep1ValidationResponseDto>();
            if (invalid != null) return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _bookingSystemService.ValidateStep1Async(userId, step1Dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("validate-step2")]
        [ProducesResponseType(typeof(ApiResponse<BookingStep2ValidationResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<BookingStep2ValidationResponseDto>>> ValidateStep2([FromBody] BookingStep2Dto step2Dto)
        {
            var invalid = InvalidModelStateResponse<BookingStep2ValidationResponseDto>();
            if (invalid != null) return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _bookingSystemService.ValidateStep2Async(userId, step2Dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("validate-step3")]
        [ProducesResponseType(typeof(ApiResponse<BookingStep3ValidationResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<BookingStep3ValidationResponseDto>>> ValidateStep3([FromBody] BookingStep3Dto step3Dto)
        {
            var invalid = InvalidModelStateResponse<BookingStep3ValidationResponseDto>();
            if (invalid != null) return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _bookingSystemService.ValidateStep3Async(userId, step3Dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("validate-step4")]
        [ProducesResponseType(typeof(ApiResponse<BookingStep4ValidationResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<BookingStep4ValidationResponseDto>>> ValidateStep4([FromBody] BookingStep4Dto step4Dto)
        {
            var invalid = InvalidModelStateResponse<BookingStep4ValidationResponseDto>();
            if (invalid != null) return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _bookingSystemService.ValidateStep4Async(userId, step4Dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("summary")]
        [ProducesResponseType(typeof(ApiResponse<BookingSummaryDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<BookingSummaryDto>>> GetSummary([FromQuery] string draftId)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _bookingSystemService.GetSummaryAsync(userId, draftId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("confirm")]
        [ProducesResponseType(typeof(ApiResponse<ConfirmBookingSystemResponseDto>), StatusCodes.Status201Created)]
        public async Task<ActionResult<ApiResponse<ConfirmBookingSystemResponseDto>>> Confirm([FromBody] ConfirmBookingSystemDto request)
        {
            var invalid = InvalidModelStateResponse<ConfirmBookingSystemResponseDto>();
            if (invalid != null) return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _bookingSystemService.ConfirmAsync(userId, request);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id}/services")]
        [ProducesResponseType(typeof(ApiResponse<List<BookingServiceItemDto>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<List<BookingServiceItemDto>>>> GetServices(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _bookingSystemService.GetServicesAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id}/services/add")]
        [ProducesResponseType(typeof(ApiResponse<BookingServiceItemDto>), StatusCodes.Status201Created)]
        public async Task<ActionResult<ApiResponse<BookingServiceItemDto>>> AddService(int id, [FromBody] AddBookingServiceDto dto)
        {
            var invalid = InvalidModelStateResponse<BookingServiceItemDto>();
            if (invalid != null) return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _bookingSystemService.AddServiceAsync(id, userId, dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id}/services/{serviceId}/update")]
        [ProducesResponseType(typeof(ApiResponse<BookingServiceItemDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<BookingServiceItemDto>>> UpdateService(
            int id, int serviceId, [FromBody] UpdateBookingServiceDto dto)
        {
            var invalid = InvalidModelStateResponse<BookingServiceItemDto>();
            if (invalid != null) return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _bookingSystemService.UpdateServiceAsync(id, serviceId, userId, dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id}/services/{serviceId}/delete")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteService(int id, int serviceId)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _bookingSystemService.DeleteServiceAsync(id, serviceId, userId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id}/services/{serviceId}/schedule")]
        [ProducesResponseType(typeof(ApiResponse<BookingServiceItemDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<BookingServiceItemDto>>> GetServiceSchedule(int id, int serviceId)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _bookingSystemService.GetServiceScheduleAsync(id, serviceId, userId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id}/services/{serviceId}/schedule/save")]
        [ProducesResponseType(typeof(ApiResponse<BookingServiceItemDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<BookingServiceItemDto>>> SaveServiceSchedule(
            int id, int serviceId, [FromBody] SaveBookingServiceScheduleDto dto)
        {
            var invalid = InvalidModelStateResponse<BookingServiceItemDto>();
            if (invalid != null) return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _bookingSystemService.SaveServiceScheduleAsync(id, serviceId, userId, dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id}/services/{serviceId}/exceptions/add")]
        [ProducesResponseType(typeof(ApiResponse<BookingScheduleExceptionDto>), StatusCodes.Status201Created)]
        public async Task<ActionResult<ApiResponse<BookingScheduleExceptionDto>>> AddScheduleException(
            int id, int serviceId, [FromBody] AddBookingScheduleExceptionDto dto)
        {
            var invalid = InvalidModelStateResponse<BookingScheduleExceptionDto>();
            if (invalid != null) return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _bookingSystemService.AddScheduleExceptionAsync(id, serviceId, userId, dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id}/services/{serviceId}/exceptions/{exceptionId}/delete")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteScheduleException(int id, int serviceId, int exceptionId)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _bookingSystemService.DeleteScheduleExceptionAsync(id, serviceId, exceptionId, userId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id}/appointments")]
        [ProducesResponseType(typeof(ApiResponse<BookingAppointmentListDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<BookingAppointmentListDto>>> GetAppointments(
            int id,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? status = null,
            [FromQuery] DateTime? fromUtc = null,
            [FromQuery] DateTime? toUtc = null,
            [FromQuery] int? serviceId = null)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _appointmentService.GetAppointmentsAsync(
                id, userId, pageNumber, pageSize, status, fromUtc, toUtc, serviceId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id}/appointments/{appointmentId}/cancel")]
        [ProducesResponseType(typeof(ApiResponse<BookingAppointmentDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<BookingAppointmentDto>>> CancelAppointment(
            int id,
            int appointmentId,
            [FromBody] CancelBookingAppointmentDto? dto = null)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _appointmentService.CancelAppointmentAsync(id, appointmentId, userId, dto);
            return StatusCode(result.StatusCode, result);
        }
    }
}
