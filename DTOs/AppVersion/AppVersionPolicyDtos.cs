using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.AppVersion
{
    public class AppVersionPolicyResponseDto
    {
        public int Id { get; set; }
        public string Platform { get; set; } = string.Empty;
        public string LatestVersion { get; set; } = string.Empty;
        public string MinSupportedVersion { get; set; } = string.Empty;
        public string? StoreUrl { get; set; }
        public string? Title { get; set; }
        public string? Message { get; set; }
        public List<string> Changelog { get; set; } = [];
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class UpdateAppVersionPolicyDto
    {
        [Required(ErrorMessage = "آخرین نسخه الزامی است")]
        [MaxLength(32, ErrorMessage = "آخرین نسخه نمی‌تواند بیشتر از ۳۲ کاراکتر باشد")]
        public string LatestVersion { get; set; } = string.Empty;

        [Required(ErrorMessage = "حداقل نسخه پشتیبانی‌شده الزامی است")]
        [MaxLength(32, ErrorMessage = "حداقل نسخه نمی‌تواند بیشتر از ۳۲ کاراکتر باشد")]
        public string MinSupportedVersion { get; set; } = string.Empty;

        [MaxLength(1000, ErrorMessage = "آدرس استور نمی‌تواند بیشتر از ۱۰۰۰ کاراکتر باشد")]
        public string? StoreUrl { get; set; }

        [MaxLength(200, ErrorMessage = "عنوان نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد")]
        public string? Title { get; set; }

        [MaxLength(1000, ErrorMessage = "پیام نمی‌تواند بیشتر از ۱۰۰۰ کاراکتر باشد")]
        public string? Message { get; set; }

        public List<string>? Changelog { get; set; }

        public bool? IsActive { get; set; }
    }
}
