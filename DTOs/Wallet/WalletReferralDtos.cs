using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Api_Vapp.DTOs.Wallet
{
    /// <summary>
    /// اطلاعات بخش معرفی در پروفایل / کیف پول
    /// </summary>
    public class WalletReferralInfoDto
    {
        public string ReferralCode { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal BonusPercent { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// درخواست اعتبارسنجی کد معرفی قبل از شارژ
    /// </summary>
    public class ValidateWalletReferralRequestDto
    {
        [Required(ErrorMessage = "مبلغ شارژ الزامی است")]
        [Range(10000, 100000000, ErrorMessage = "مبلغ شارژ باید بین 10,000 تا 100,000,000 تومان باشد")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "کد معرفی الزامی است")]
        [MaxLength(50, ErrorMessage = "کد معرفی نمی‌تواند بیشتر از ۵۰ کاراکتر باشد")]
        public string ReferralCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// نتیجه اعتبارسنجی و پیش‌نمایش مبالغ
    /// </summary>
    public class ValidateWalletReferralResponseDto
    {
        public bool IsValid { get; set; }
        public string ReferralCode { get; set; } = string.Empty;
        public decimal RequestedAmount { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal PayableAmount { get; set; }
        public decimal BonusPercent { get; set; }
        public decimal BonusAmount { get; set; }
        public string FormattedRequestedAmount { get; set; } = string.Empty;
        public string FormattedDiscountAmount { get; set; } = string.Empty;
        public string FormattedPayableAmount { get; set; } = string.Empty;
        public string FormattedBonusAmount { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// اسنپ‌شات رفرال ذخیره‌شده در MetaData پرداخت (قفل‌شده در لحظه شارژ)
    /// </summary>
    public class WalletReferralPaymentMetaDto
    {
        [JsonPropertyName("referralCode")]
        public string ReferralCode { get; set; } = string.Empty;

        [JsonPropertyName("referrerUserId")]
        public int ReferrerUserId { get; set; }

        [JsonPropertyName("requestedAmount")]
        public decimal RequestedAmount { get; set; }

        [JsonPropertyName("payableAmount")]
        public decimal PayableAmount { get; set; }

        [JsonPropertyName("discountAmount")]
        public decimal DiscountAmount { get; set; }

        [JsonPropertyName("discountPercent")]
        public decimal DiscountPercent { get; set; }

        [JsonPropertyName("bonusAmount")]
        public decimal BonusAmount { get; set; }

        [JsonPropertyName("bonusPercent")]
        public decimal BonusPercent { get; set; }
    }

    /// <summary>
    /// پوشش MetaData پرداخت برای شارژ کیف پول
    /// </summary>
    public class WalletChargePaymentMetaDto
    {
        [JsonPropertyName("walletReferral")]
        public WalletReferralPaymentMetaDto? WalletReferral { get; set; }
    }
}
