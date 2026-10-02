using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Message;
using Api_Vapp.Services;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// سرویس تعرفه و قواعد محاسبه پارت پیامک (کش‌شده برای مسیرهای داغ).
    /// </summary>
    public interface ISmsPricingService
    {
        /// <summary>اسنپ‌شات runtime با MemoryCache — مناسب ارسال انبوه</summary>
        Task<SmsPricingRuntime> GetRuntimeAsync(CancellationToken cancellationToken = default);

        Task<ApiResponse<SmsPricingSettingResponseDto>> GetAdminSettingsAsync();

        Task<ApiResponse<SmsPricingSettingResponseDto>> UpdateAdminSettingsAsync(UpdateSmsPricingSettingDto dto);

        Task<ApiResponse<SmsPricingPreviewResponseDto>> PreviewAsync(SmsPricingPreviewRequestDto dto);

        /// <summary>
        /// تخمین کاراکتر/صفحه برای کلاینت کاربر (موبایل) با تنظیمات ذخیره‌شده —
        /// متن خالی مجاز است (شمارنده زنده).
        /// </summary>
        Task<ApiResponse<SmsPartsEstimateResponseDto>> EstimatePartsForUserAsync(SmsPartsEstimateRequestDto dto);
    }
}
