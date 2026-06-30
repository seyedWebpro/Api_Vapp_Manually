using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp._Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    public class BookingAppointmentRepository : BaseRepository<BookingAppointment>, IBookingAppointmentRepository
    {
        public BookingAppointmentRepository(Api_Context context) : base(context)
        {
        }

        public async Task<BookingSystem?> GetActiveSystemBySlugAsync(string slug)
        {
            return await _context.BookingSystems
                .Include(b => b.Services.Where(s => !s.IsDeleted))
                .AsNoTracking()
                .FirstOrDefaultAsync(b =>
                    b.Slug == slug &&
                    !b.IsDeleted &&
                    b.IsActive &&
                    b.Status == BookingSystemStatus.Published);
        }

        public async Task<BookingServiceItem?> GetActiveServiceBySlugAsync(string slug, int serviceId)
        {
            return await _context.BookingServiceItems
                .Include(s => s.DaySchedules)
                .Include(s => s.ScheduleExceptions.Where(e => !e.IsDeleted))
                .AsNoTracking()
                .Where(s =>
                    s.Id == serviceId &&
                    !s.IsDeleted &&
                    s.BookingSystem.Slug == slug &&
                    !s.BookingSystem.IsDeleted &&
                    s.BookingSystem.IsActive &&
                    s.BookingSystem.Status == BookingSystemStatus.Published)
                .FirstOrDefaultAsync();
        }

        public async Task<BookingServiceItem?> GetServiceForBookingAsync(int systemId, int serviceId)
        {
            return await _context.BookingServiceItems
                .Include(s => s.DaySchedules)
                .Include(s => s.ScheduleExceptions.Where(e => !e.IsDeleted))
                .AsNoTracking()
                .Where(s =>
                    s.Id == serviceId &&
                    s.BookingSystemId == systemId &&
                    !s.IsDeleted &&
                    !s.BookingSystem.IsDeleted &&
                    s.BookingSystem.IsActive)
                .FirstOrDefaultAsync();
        }

        public async Task<List<BookingAppointment>> GetAppointmentsForServiceOnDateAsync(int serviceId, DateOnly dateUtc)
        {
            var dayStart = dateUtc.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var dayEnd = dayStart.AddDays(1);

            return await _dbSet
                .AsNoTracking()
                .Where(a =>
                    a.BookingServiceItemId == serviceId &&
                    !a.IsDeleted &&
                    a.Status == BookingAppointmentStatuses.Confirmed &&
                    a.StartUtc >= dayStart &&
                    a.StartUtc < dayEnd)
                .ToListAsync();
        }

        public async Task<List<BookingAppointment>> GetPendingRemindersAsync(DateTime utcNow, int maxReminderOffsetMinutes)
        {
            var maxStartUtc = utcNow.AddMinutes(maxReminderOffsetMinutes + 2);

            return await _dbSet
                .AsNoTracking()
                .Include(a => a.BookingServiceItem)
                .Include(a => a.BookingSystem)
                .Where(a =>
                    !a.IsDeleted &&
                    a.Status == BookingAppointmentStatuses.Confirmed &&
                    a.ReminderSentAt == null &&
                    a.StartUtc > utcNow &&
                    a.StartUtc <= maxStartUtc)
                .ToListAsync();
        }

        public async Task<BookingAppointment?> GetByIdAndSystemIdAsync(int appointmentId, int systemId)
        {
            return await _dbSet
                .Include(a => a.BookingServiceItem)
                .AsNoTracking()
                .FirstOrDefaultAsync(a =>
                    a.Id == appointmentId &&
                    a.BookingSystemId == systemId &&
                    !a.IsDeleted);
        }

        public async Task<(List<BookingAppointment> Items, int TotalCount)> GetBySystemIdAsync(
            int systemId,
            int pageNumber,
            int pageSize,
            string? status,
            DateTime? fromUtc,
            DateTime? toUtc,
            int? serviceId)
        {
            var query = _dbSet
                .AsNoTracking()
                .Where(a => a.BookingSystemId == systemId && !a.IsDeleted);

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(a => a.Status == status);
            }

            if (fromUtc.HasValue)
            {
                query = query.Where(a => a.StartUtc >= fromUtc.Value);
            }

            if (toUtc.HasValue)
            {
                query = query.Where(a => a.StartUtc <= toUtc.Value);
            }

            if (serviceId.HasValue)
            {
                query = query.Where(a => a.BookingServiceItemId == serviceId.Value);
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(a => a.StartUtc)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new BookingAppointment
                {
                    Id = a.Id,
                    BookingSystemId = a.BookingSystemId,
                    BookingServiceItemId = a.BookingServiceItemId,
                    CustomerFullName = a.CustomerFullName,
                    CustomerMobile = a.CustomerMobile,
                    StartUtc = a.StartUtc,
                    EndUtc = a.EndUtc,
                    Status = a.Status,
                    ReminderSentAt = a.ReminderSentAt,
                    CancelledAt = a.CancelledAt,
                    CancellationReason = a.CancellationReason,
                    CreatedAt = a.CreatedAt,
                    BookingServiceItem = new BookingServiceItem { Title = a.BookingServiceItem.Title }
                })
                .ToListAsync();

            return (items, totalCount);
        }
    }
}
