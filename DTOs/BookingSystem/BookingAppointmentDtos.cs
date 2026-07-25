using System.ComponentModel.DataAnnotations;
using Api_Vapp.Models;

namespace Api_Vapp.DTOs.BookingSystem
{
    // ─── Public (بدون احراز هویت) ────────────────────────────────────

    public class BookingPublicSystemDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Location { get; set; }
        public string ActivityType { get; set; } = string.Empty;
        public string? ActivityTypeTitle { get; set; }
        public string Slug { get; set; } = string.Empty;
        public List<BookingPublicServiceDto> Services { get; set; } = new();
    }

    public class BookingPublicServiceDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
        public bool HasCost { get; set; }
        public decimal? Price { get; set; }
        public decimal? DepositAmount { get; set; }
    }

    public class BookingTimeSlotDto
    {
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
    }

    public class BookingAvailableSlotsDto
    {
        public int ServiceId { get; set; }
        public DateOnly Date { get; set; }
        public List<BookingTimeSlotDto> Slots { get; set; } = new();
    }

    public class CreatePublicBookingDto
    {
        [Required(ErrorMessage = "شناسه خدمت الزامی است")]
        public int ServiceId { get; set; }

        [Required(ErrorMessage = "زمان نوبت الزامی است")]
        public DateTime StartUtc { get; set; }

        [Required(ErrorMessage = "نام الزامی است")]
        [MaxLength(200, ErrorMessage = "نام نمی‌تواند بیشتر از 200 کاراکتر باشد")]
        public string CustomerFullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "شماره موبایل الزامی است")]
        [MaxLength(20, ErrorMessage = "شماره موبایل نامعتبر است")]
        public string CustomerMobile { get; set; } = string.Empty;

        [MaxLength(1000, ErrorMessage = "یادداشت نمی‌تواند بیشتر از 1000 کاراکتر باشد")]
        public string? CustomerNote { get; set; }
    }

    public class BookingAppointmentDto
    {
        public int Id { get; set; }

        /// <summary>شماره نوبت برای نمایش (همان Id)</summary>
        public int AppointmentNumber { get; set; }

        public int BookingSystemId { get; set; }
        public int ServiceId { get; set; }
        public string ServiceTitle { get; set; } = string.Empty;
        public string CustomerFullName { get; set; } = string.Empty;
        public string CustomerMobile { get; set; } = string.Empty;
        public string? CustomerNote { get; set; }
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? ReminderSentAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? CancellationReason { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class BookingAppointmentListDto
    {
        public List<BookingAppointmentDto> Appointments { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class CancelBookingAppointmentDto
    {
        public string? Reason { get; set; }
    }

    public class CreatePublicBookingResponseDto
    {
        public BookingAppointmentDto Appointment { get; set; } = new();
    }

    // ─── Dashboard ───────────────────────────────────────────────────

    public class BookingDashboardDto
    {
        public int SystemId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ActivityType { get; set; } = string.Empty;
        public string? ActivityTypeTitle { get; set; }
        public string? Location { get; set; }
        public string PublicUrl { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public BookingDashboardStatsDto Stats { get; set; } = new();
        public List<BookingAppointmentDto> TodaySchedule { get; set; } = new();
    }

    public class BookingDashboardStatsDto
    {
        public int TodayTotal { get; set; }
        public int Confirmed { get; set; }
        public int Pending { get; set; }
        public int Cancelled { get; set; }
    }

    // ─── Calendar summary ────────────────────────────────────────────

    public class BookingCalendarMonthDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public List<BookingCalendarDayDto> Days { get; set; } = new();
    }

    public class BookingCalendarDayDto
    {
        public DateOnly Date { get; set; }
        public int TotalCount { get; set; }
        public List<BookingCalendarSlotDto> Slots { get; set; } = new();
    }

    public class BookingCalendarSlotDto
    {
        public int AppointmentId { get; set; }
        public DateTime StartUtc { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CustomerFullName { get; set; } = string.Empty;
        public string ServiceTitle { get; set; } = string.Empty;
    }

    // ─── Manual / Edit ───────────────────────────────────────────────

    public class CreateManualBookingDto
    {
        [Required(ErrorMessage = "نام الزامی است")]
        [MaxLength(200)]
        public string CustomerFullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "شماره تماس الزامی است")]
        [MaxLength(20)]
        public string CustomerMobile { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? CustomerNote { get; set; }

        [Required(ErrorMessage = "شناسه خدمت الزامی است")]
        public int ServiceId { get; set; }

        [Required(ErrorMessage = "زمان نوبت الزامی است")]
        public DateTime StartUtc { get; set; }
    }

    public class UpdateBookingAppointmentDto
    {
        [MaxLength(200)]
        public string? CustomerFullName { get; set; }

        [MaxLength(20)]
        public string? CustomerMobile { get; set; }

        [MaxLength(1000)]
        public string? CustomerNote { get; set; }

        public int? ServiceId { get; set; }

        public DateTime? StartUtc { get; set; }
    }

    // ─── Free time / availability management ─────────────────────────

    public class BookingDayAvailabilityDto
    {
        public int SystemId { get; set; }
        public int ServiceId { get; set; }
        public string ServiceTitle { get; set; } = string.Empty;
        public DateOnly Date { get; set; }
        public List<BookingManagedSlotDto> Slots { get; set; } = new();
    }

    public class BookingManagedSlotDto
    {
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }

        /// <summary>Reserved | Empty | Blocked</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>آیا اسلات برای رزرو باز است (Blocked=false)</summary>
        public bool IsEnabled { get; set; }

        public int? AppointmentId { get; set; }
        public string? CustomerFullName { get; set; }
    }

    public static class BookingManagedSlotStatuses
    {
        public const string Reserved = "Reserved";
        public const string Empty = "Empty";
        public const string Blocked = "Blocked";
    }

    public class SaveBookingDayAvailabilityDto
    {
        [Required]
        public DateOnly Date { get; set; }

        /// <summary>
        /// خدمت مبنا برای تولید اسلات‌ها — اختیاری؛ در صورت خالی بودن اولین خدمت استفاده می‌شود
        /// </summary>
        public int? ServiceId { get; set; }

        [Required]
        public List<BookingSlotToggleDto> Slots { get; set; } = new();
    }

    public class BookingSlotToggleDto
    {
        [Required]
        public DateTime StartUtc { get; set; }

        /// <summary>true = باز برای رزرو، false = مسدود</summary>
        public bool IsEnabled { get; set; }
    }
}
