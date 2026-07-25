using Api_Vapp.Models;

namespace Api_Vapp.Interfaces
{
    public interface IBookingAppointmentRepository
    {
        Task<BookingSystem?> GetActiveSystemBySlugAsync(string slug);
        Task<BookingServiceItem?> GetActiveServiceBySlugAsync(string slug, int serviceId);
        Task<BookingServiceItem?> GetServiceForBookingAsync(int systemId, int serviceId);
        Task<List<BookingAppointment>> GetAppointmentsForServiceOnDateAsync(int serviceId, DateOnly dateUtc);
        Task<List<BookingAppointment>> GetAppointmentsForSystemOnDateAsync(int systemId, DateOnly dateUtc);
        Task<List<DateTime>> GetBlockedStartsForSystemOnDateAsync(int systemId, DateOnly dateUtc);
        Task<List<BookingAppointment>> GetPendingRemindersAsync(DateTime utcNow, int maxReminderOffsetMinutes);
        Task<BookingAppointment?> GetByIdAndSystemIdAsync(int appointmentId, int systemId);
        Task<(List<BookingAppointment> Items, int TotalCount)> GetBySystemIdAsync(
            int systemId,
            int pageNumber,
            int pageSize,
            string? status,
            DateTime? fromUtc,
            DateTime? toUtc,
            int? serviceId,
            string? searchName = null);
        Task<BookingDashboardCounts> GetDashboardCountsAsync(int systemId, DateOnly todayUtc);
        Task<List<BookingAppointment>> GetCalendarAppointmentsAsync(int systemId, DateTime fromUtc, DateTime toUtc);
    }

    public class BookingDashboardCounts
    {
        public int TodayTotal { get; set; }
        public int Confirmed { get; set; }
        public int Pending { get; set; }
        public int Cancelled { get; set; }
    }
}
