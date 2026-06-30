using System.ComponentModel.DataAnnotations;
using Api_Vapp.Models;

namespace Api_Vapp.DTOs.BookingSystem
{
    // ─── Public (بدون احراز هویت) ────────────────────────────────────

    public class BookingPublicSystemDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
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
    }

    public class BookingAppointmentDto
    {
        public int Id { get; set; }
        public int BookingSystemId { get; set; }
        public int ServiceId { get; set; }
        public string ServiceTitle { get; set; } = string.Empty;
        public string CustomerFullName { get; set; } = string.Empty;
        public string CustomerMobile { get; set; } = string.Empty;
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
}
