using System.ComponentModel.DataAnnotations;
using Api_Vapp.Models;

namespace Api_Vapp.DTOs.BookingSystem
{
    // ─── List / Detail ───────────────────────────────────────────────

    public class BookingSystemListDto
    {
        public List<BookingSystemDto> Systems { get; set; } = new();
        public int TotalCount { get; set; }
        public int ActiveCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class BookingSystemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ActivityType { get; set; } = string.Empty;
        public string? ActivityTypeTitle { get; set; }
        public string? Description { get; set; }
        public string Slug { get; set; } = string.Empty;
        public string PublicUrl { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool SaveToPhonebook { get; set; }
        public bool IsActive { get; set; }
        public List<int> NotebookIds { get; set; } = new();
        public List<BookingServiceItemDto> Services { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? PublishedAt { get; set; }
    }

    public class BookingServiceItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
        public bool HasCost { get; set; }
        public decimal? Price { get; set; }
        public decimal? ServiceCost { get; set; }
        public decimal? DepositAmount { get; set; }
        public int BufferMinutesBetweenAppointments { get; set; }
        public int? MaxDailyReservations { get; set; }
        public int ReminderOffsetMinutes { get; set; }
        public int SortOrder { get; set; }
        public List<BookingDayScheduleDto> WeeklyDays { get; set; } = new();
        public List<BookingScheduleExceptionDto> Exceptions { get; set; } = new();
    }

    public class BookingNotebookDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int MembersCount { get; set; }
    }

    public class BookingActivityTypeDto
    {
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
    }

    // ─── Wizard Step 1 ───────────────────────────────────────────────

    public class BookingStep1Dto
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string ActivityType { get; set; } = string.Empty;

        public string? Description { get; set; }

        /// <summary>
        /// slug سفارشی — اختیاری؛ در صورت خالی بودن در confirm خودکار ساخته می‌شود
        /// </summary>
        public string? CustomSlug { get; set; }

        public bool SaveToPhonebook { get; set; }

        public List<int> NotebookIds { get; set; } = new();
    }

    public class BookingStep1ValidationResponseDto
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public string? DraftId { get; set; }
        public DateTime? DraftExpiresAt { get; set; }
    }

    // ─── Wizard Step 2 ───────────────────────────────────────────────

    public class BookingStep2Dto
    {
        [Required]
        public string DraftId { get; set; } = string.Empty;

        [Required]
        public List<BookingServiceDraftDto> Services { get; set; } = new();
    }

    public class BookingServiceDraftDto
    {
        [Required]
        public string ServiceTempId { get; set; } = string.Empty;

        [Required]
        public string Title { get; set; } = string.Empty;

        [Range(1, 1440)]
        public int DurationMinutes { get; set; }

        public bool HasCost { get; set; }

        public decimal? Price { get; set; }

        public decimal? ServiceCost { get; set; }

        public decimal? DepositAmount { get; set; }
    }

    public class BookingStep2ValidationResponseDto
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public int ServicesCount { get; set; }
    }

    // ─── Wizard Step 3 ───────────────────────────────────────────────

    public class BookingStep3Dto
    {
        [Required]
        public string DraftId { get; set; } = string.Empty;

        [Required]
        public List<BookingServiceScheduleDraftDto> ServiceSchedules { get; set; } = new();
    }

    public class BookingServiceScheduleDraftDto
    {
        [Required]
        public string ServiceTempId { get; set; } = string.Empty;

        [Required]
        public List<BookingDayScheduleDto> WeeklyDays { get; set; } = new();

        public List<BookingScheduleExceptionDto> Exceptions { get; set; } = new();
    }

    public class BookingDayScheduleDto
    {
        public DayOfWeek DayOfWeek { get; set; }
        public bool IsOpen { get; set; }

        /// <summary>
        /// ساعت شروع — UTC (مثال: "05:30:00" برای 09:00 تهران)
        /// </summary>
        public TimeSpan? StartTimeUtc { get; set; }

        /// <summary>
        /// ساعت پایان — UTC
        /// </summary>
        public TimeSpan? EndTimeUtc { get; set; }
    }

    public class BookingScheduleExceptionDto
    {
        public int? Id { get; set; }

        public DateOnly ExceptionDate { get; set; }

        /// <summary>
        /// Holiday | Leave
        /// </summary>
        public string Type { get; set; } = BookingScheduleExceptionTypes.Holiday;

        public string? Label { get; set; }
    }

    public class BookingStep3ValidationResponseDto
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    // ─── Wizard Step 4 ───────────────────────────────────────────────

    public class BookingStep4Dto
    {
        [Required]
        public string DraftId { get; set; } = string.Empty;

        [Required]
        public List<BookingServiceReminderDraftDto> ServiceSettings { get; set; } = new();
    }

    public class BookingServiceReminderDraftDto
    {
        [Required]
        public string ServiceTempId { get; set; } = string.Empty;

        [Range(0, 480)]
        public int BufferMinutesBetweenAppointments { get; set; }

        [Range(1, 10000)]
        public int? MaxDailyReservations { get; set; }

        /// <summary>
        /// چند دقیقه قبل از نوبت SMS یادآوری ارسال شود (مثلاً 1440 = یک روز قبل)
        /// </summary>
        [Range(1, 43200)]
        public int ReminderOffsetMinutes { get; set; }
    }

    public class BookingStep4ValidationResponseDto
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    // ─── Summary / Confirm ─────────────────────────────────────────

    public class GetBookingSummaryRequestDto
    {
        [Required]
        public string DraftId { get; set; } = string.Empty;
    }

    public class BookingSummaryDto
    {
        public BookingStep1Dto Step1 { get; set; } = new();
        public List<BookingServiceDraftDto> Services { get; set; } = new();
        public List<BookingServiceScheduleDraftDto> ServiceSchedules { get; set; } = new();
        public List<BookingServiceReminderDraftDto> ServiceSettings { get; set; } = new();
        public string? ResolvedSlug { get; set; }
        public string? PublicUrlPreview { get; set; }
    }

    public class ConfirmBookingSystemDto
    {
        [Required]
        public string DraftId { get; set; } = string.Empty;
    }

    public class ConfirmBookingSystemResponseDto
    {
        public BookingSystemDto System { get; set; } = new();
    }

    // ─── Update / Service CRUD ───────────────────────────────────────

    public class UpdateBookingSystemDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? ActivityType { get; set; }
        public bool? SaveToPhonebook { get; set; }
        public List<int>? NotebookIds { get; set; }
        public bool? IsActive { get; set; }
        public string? Slug { get; set; }
    }

    public class AddBookingServiceDto
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        [Range(1, 1440)]
        public int DurationMinutes { get; set; }

        public bool HasCost { get; set; }
        public decimal? Price { get; set; }
        public decimal? ServiceCost { get; set; }
        public decimal? DepositAmount { get; set; }

        [Range(0, 480)]
        public int BufferMinutesBetweenAppointments { get; set; }

        [Range(1, 10000)]
        public int? MaxDailyReservations { get; set; }

        [Range(1, 43200)]
        public int ReminderOffsetMinutes { get; set; }

        public List<BookingDayScheduleDto> WeeklyDays { get; set; } = new();
        public List<BookingScheduleExceptionDto> Exceptions { get; set; } = new();
    }

    public class UpdateBookingServiceDto
    {
        public string? Title { get; set; }

        [Range(1, 1440)]
        public int? DurationMinutes { get; set; }

        public bool? HasCost { get; set; }
        public decimal? Price { get; set; }
        public decimal? ServiceCost { get; set; }
        public decimal? DepositAmount { get; set; }

        [Range(0, 480)]
        public int? BufferMinutesBetweenAppointments { get; set; }

        [Range(1, 10000)]
        public int? MaxDailyReservations { get; set; }

        [Range(1, 43200)]
        public int? ReminderOffsetMinutes { get; set; }
    }

    public class SaveBookingServiceScheduleDto
    {
        [Required]
        public List<BookingDayScheduleDto> WeeklyDays { get; set; } = new();
    }

    public class AddBookingScheduleExceptionDto
    {
        [Required]
        public DateOnly ExceptionDate { get; set; }

        [Required]
        public string Type { get; set; } = BookingScheduleExceptionTypes.Holiday;

        public string? Label { get; set; }
    }
}
