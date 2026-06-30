using Api_Vapp.DTOs.BookingSystem;
using Api_Vapp.Models;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// محاسبه اسلات‌های خالی بر اساس برنامه هفتگی، استثناها و نوبت‌های موجود
    /// </summary>
    public static class BookingSlotCalculator
    {
        public static List<BookingTimeSlotDto> CalculateAvailableSlots(
            BookingServiceItem service,
            DateOnly dateUtc,
            IEnumerable<BookingAppointment> existingAppointments)
        {
            if (IsExceptionDay(service, dateUtc))
            {
                return new List<BookingTimeSlotDto>();
            }

            var daySchedule = service.DaySchedules
                .FirstOrDefault(d => d.DayOfWeek == dateUtc.DayOfWeek);

            if (daySchedule == null || !daySchedule.IsOpen ||
                !daySchedule.StartTimeUtc.HasValue || !daySchedule.EndTimeUtc.HasValue)
            {
                return new List<BookingTimeSlotDto>();
            }

            var workStart = dateUtc.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) + daySchedule.StartTimeUtc.Value;
            var workEnd = dateUtc.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) + daySchedule.EndTimeUtc.Value;

            if (workEnd <= workStart)
            {
                return new List<BookingTimeSlotDto>();
            }

            var activeAppointments = existingAppointments
                .Where(a => BookingAppointmentStatuses.IsActive(a.Status))
                .OrderBy(a => a.StartUtc)
                .ToList();

            if (service.MaxDailyReservations.HasValue &&
                activeAppointments.Count >= service.MaxDailyReservations.Value)
            {
                return new List<BookingTimeSlotDto>();
            }

            var slots = new List<BookingTimeSlotDto>();
            var step = TimeSpan.FromMinutes(service.DurationMinutes + service.BufferMinutesBetweenAppointments);
            var duration = TimeSpan.FromMinutes(service.DurationMinutes);
            var cursor = workStart;

            while (cursor + duration <= workEnd)
            {
                var slotEnd = cursor + duration;
                if (!HasConflict(cursor, slotEnd, service.BufferMinutesBetweenAppointments, activeAppointments))
                {
                    slots.Add(new BookingTimeSlotDto
                    {
                        StartUtc = cursor,
                        EndUtc = slotEnd
                    });
                }

                cursor += step;
            }

            return slots;
        }

        public static bool IsSlotAvailable(
            BookingServiceItem service,
            DateTime startUtc,
            IEnumerable<BookingAppointment> existingAppointments)
        {
            var date = DateOnly.FromDateTime(startUtc);
            var slots = CalculateAvailableSlots(service, date, existingAppointments);
            return slots.Any(s => s.StartUtc == startUtc);
        }

        private static bool IsExceptionDay(BookingServiceItem service, DateOnly dateUtc)
        {
            return service.ScheduleExceptions.Any(e =>
                !e.IsDeleted &&
                DateOnly.FromDateTime(e.ExceptionDateUtc) == dateUtc);
        }

        private static bool HasConflict(
            DateTime slotStart,
            DateTime slotEnd,
            int bufferMinutes,
            List<BookingAppointment> appointments)
        {
            var buffer = TimeSpan.FromMinutes(bufferMinutes);

            foreach (var appointment in appointments)
            {
                var blockedEnd = appointment.EndUtc + buffer;
                if (slotStart < blockedEnd && slotEnd > appointment.StartUtc)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
