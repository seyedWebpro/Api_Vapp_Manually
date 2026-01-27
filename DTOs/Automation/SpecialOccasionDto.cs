using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Automation
{
    /// <summary>
    /// DTO برای ایجاد مناسبت خاص
    /// </summary>
    public class CreateSpecialOccasionDto
    {
        [Required(ErrorMessage = "نام مناسبت الزامی است")]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Type { get; set; } = "Custom"; // Holiday, Death, Custom

        [Required(ErrorMessage = "تاریخ مناسبت الزامی است")]
        public DateTime OccasionDate { get; set; }

        public string? DefaultMessage { get; set; }
    }

    /// <summary>
    /// DTO برای به‌روزرسانی مناسبت خاص
    /// </summary>
    public class UpdateSpecialOccasionDto
    {
        [MaxLength(200)]
        public string? Name { get; set; }

        [MaxLength(50)]
        public string? Type { get; set; }

        public DateTime? OccasionDate { get; set; }

        public string? DefaultMessage { get; set; }

        public bool? IsActive { get; set; }
    }

    /// <summary>
    /// DTO برای نمایش مناسبت خاص
    /// </summary>
    public class SpecialOccasionResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime OccasionDate { get; set; }
        public string? DefaultMessage { get; set; }
        public bool IsSystem { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

