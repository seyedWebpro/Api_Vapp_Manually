using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Wallet;
using Api_Vapp.Models;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// سرویس سیستم معرفی (رفرال) برای شارژ کیف پول
    /// </summary>
    public interface IWalletReferralService
    {
        /// <summary>اطلاعات بخش رفرال برای کاربر فعلی</summary>
        Task<ApiResponse<WalletReferralInfoDto>> GetReferralInfoAsync(int userId);

        /// <summary>اعتبارسنجی کد و پیش‌نمایش مبالغ</summary>
        Task<ApiResponse<ValidateWalletReferralResponseDto>> ValidateReferralAsync(int userId, ValidateWalletReferralRequestDto request);

        /// <summary>اعمال رفرال هنگام ایجاد درخواست شارژ (یا null اگر کدی ارسال نشده)</summary>
        Task<ApiResponse<WalletReferralPaymentMetaDto?>> ResolveReferralForChargeAsync(int userId, decimal requestedAmount, string? referralCode);

        /// <summary>
        /// پس از Verify موفق: واریز مبلغ درخواستی به ذینفع و پاداش به معرف (idempotent)
        /// </summary>
        Task FulfillWalletChargeWithReferralAsync(Payment payment);

        /// <summary>تنظیمات ادمین</summary>
        Task<ApiResponse<WalletReferralSettingResponseDto>> GetAdminSettingsAsync();

        /// <summary>به‌روزرسانی تنظیمات ادمین</summary>
        Task<ApiResponse<WalletReferralSettingResponseDto>> UpdateAdminSettingsAsync(UpdateWalletReferralSettingDto dto);

        /// <summary>اطمینان از وجود کد معرفی برای کاربر</summary>
        Task<string> EnsureReferralCodeAsync(User user);

        /// <summary>ساخت متن توضیحات از قالب تنظیمات</summary>
        string BuildDescription(WalletReferralSetting setting);
    }
}
