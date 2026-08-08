using Api_Vapp.DTOs.BookingSystem;
using Api_Vapp.DTOs.Common;
using Microsoft.AspNetCore.Http;

namespace Api_Vapp.Interfaces
{
    public interface IBookingAppointmentService
    {
        Task<ApiResponse<BookingPublicSystemDto>> GetPublicSystemAsync(string slug);
        Task<ApiResponse<BookingAvailableSlotsDto>> GetAvailableSlotsAsync(string slug, int serviceId, DateOnly date);
        Task<ApiResponse<CreatePublicBookingResponseDto>> CreatePublicBookingAsync(
            string slug,
            CreatePublicBookingDto dto,
            IFormFile? paymentReceiptFile = null);

        Task<ApiResponse<PublicBookingStatusDto>> LookupPublicBookingStatusAsync(
            string slug,
            LookupPublicBookingDto dto);

        Task<ApiResponse<BookingDashboardDto>> GetDashboardAsync(int systemId, int userId, DateOnly? dateUtc = null);

        Task<ApiResponse<BookingCalendarMonthDto>> GetCalendarAsync(
            int systemId, int userId, int year, int month);

        Task<ApiResponse<BookingAppointmentListDto>> GetAppointmentsAsync(
            int systemId,
            int userId,
            int pageNumber,
            int pageSize,
            string? status,
            DateTime? fromUtc,
            DateTime? toUtc,
            int? serviceId,
            string? searchName = null);

        Task<ApiResponse<BookingAppointmentDto>> GetAppointmentByIdAsync(
            int systemId, int appointmentId, int userId);

        Task<ApiResponse<BookingPaymentReceiptDto>> GetPaymentReceiptAsync(
            int systemId, int appointmentId, int userId);

        Task<ApiResponse<BookingAppointmentDto>> CreateManualBookingAsync(
            int systemId, int userId, CreateManualBookingDto dto);

        Task<ApiResponse<BookingAppointmentDto>> UpdateAppointmentAsync(
            int systemId, int appointmentId, int userId, UpdateBookingAppointmentDto dto);

        Task<ApiResponse<BookingAppointmentDto>> ConfirmAppointmentAsync(
            int systemId, int appointmentId, int userId);

        Task<ApiResponse<BookingAppointmentDto>> CancelAppointmentAsync(
            int systemId,
            int appointmentId,
            int userId,
            CancelBookingAppointmentDto? dto);

        Task<ApiResponse<BookingDayAvailabilityDto>> GetDayAvailabilityAsync(
            int systemId, int userId, DateOnly date, int? serviceId = null);

        Task<ApiResponse<BookingDayAvailabilityDto>> SaveDayAvailabilityAsync(
            int systemId, int userId, SaveBookingDayAvailabilityDto dto);

        Task ProcessRemindersAsync(CancellationToken cancellationToken = default);
    }
}
