using Api_Vapp.Models;

namespace Api_Vapp.Interfaces
{
    public interface IBookingAppointmentRepository
    {
        Task<BookingSystem?> GetActiveSystemBySlugAsync(string slug);
        Task<BookingServiceItem?> GetActiveServiceBySlugAsync(string slug, int serviceId);
        Task<BookingServiceItem?> GetServiceForBookingAsync(int systemId, int serviceId);
        Task<List<BookingAppointment>> GetAppointmentsForServiceOnDateAsync(int serviceId, DateOnly dateUtc);
        Task<List<BookingAppointment>> GetPendingRemindersAsync(DateTime utcNow, int maxReminderOffsetMinutes);
        Task<BookingAppointment?> GetByIdAndSystemIdAsync(int appointmentId, int systemId);
        Task<(List<BookingAppointment> Items, int TotalCount)> GetBySystemIdAsync(
            int systemId,
            int pageNumber,
            int pageSize,
            string? status,
            DateTime? fromUtc,
            DateTime? toUtc,
            int? serviceId);
    }
}
