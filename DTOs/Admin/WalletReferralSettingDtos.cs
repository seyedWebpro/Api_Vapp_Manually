using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Admin
{
    public class WalletReferralSettingResponseDto
    {
        public int Id { get; set; }
        public bool IsEnabled { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal BonusPercent { get; set; }
        public string DescriptionTemplate { get; set; } = string.Empty;
        public string DescriptionPreview { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class UpdateWalletReferralSettingDto
    {
        [Required(ErrorMessage = "وضعیت فعال بودن الزامی است")]
        public bool IsEnabled { get; set; }

        [Required(ErrorMessage = "درصد تخفیف الزامی است")]
        [Range(0, 100, ErrorMessage = "درصد تخفیف باید بین ۰ تا ۱۰۰ باشد")]
        public decimal DiscountPercent { get; set; }

        [Required(ErrorMessage = "درصد پاداش الزامی است")]
        [Range(0, 100, ErrorMessage = "درصد پاداش باید بین ۰ تا ۱۰۰ باشد")]
        public decimal BonusPercent { get; set; }

        [MaxLength(1000, ErrorMessage = "متن توضیحات نمی‌تواند بیشتر از ۱۰۰۰ کاراکتر باشد")]
        public string? DescriptionTemplate { get; set; }
    }
}
