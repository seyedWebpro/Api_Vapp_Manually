using Api_Vapp.DTOs.BookingSystem;
using Api_Vapp.DTOs.Common;

namespace Api_Vapp.Interfaces
{
    public interface IBookingAppointmentService
    {
        Task<ApiResponse<BookingPublicSystemDto>> GetPublicSystemAsync(string slug);
        Task<ApiResponse<BookingAvailableSlotsDto>> GetAvailableSlotsAsync(string slug, int serviceId, DateOnly date);
        Task<ApiResponse<CreatePublicBookingResponseDto>> CreatePublicBookingAsync(string slug, CreatePublicBookingDto dto);

        Task<ApiResponse<BookingAppointmentListDto>> GetAppointmentsAsync(
            int systemId,
            int userId,
            int pageNumber,
            int pageSize,
            string? status,
            DateTime? fromUtc,
            DateTime? toUtc,
            int? serviceId);

        Task<ApiResponse<BookingAppointmentDto>> CancelAppointmentAsync(
            int systemId,
            int appointmentId,
            int userId,
            CancelBookingAppointmentDto? dto);

        Task ProcessRemindersAsync(CancellationToken cancellationToken = default);
    }
}
