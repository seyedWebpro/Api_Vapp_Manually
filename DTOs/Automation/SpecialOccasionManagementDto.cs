using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Automation
{
    /// <summary>
    /// DTO برای مدیریت مناسبت‌های خاص
    /// </summary>
    public class SpecialOccasionManagementDto
    {
        [Required(ErrorMessage = "نوع عملیات الزامی است")]
        [RegularExpression("^(Add|Remove)$", ErrorMessage = "نوع عملیات باید Add یا Remove باشد")]
        public string Action { get; set; } = string.Empty; // Add, Remove

        // برای افزودن مناسبت
        [MaxLength(200, ErrorMessage = "نام مناسبت نمی‌تواند بیشتر از 200 کاراکتر باشد")]
        public string? OccasionName { get; set; }

        [DataType(DataType.Date)]
        public DateTime? OccasionDate { get; set; }

        // برای حذف مناسبت
        public int? OccasionId { get; set; }
    }
}

