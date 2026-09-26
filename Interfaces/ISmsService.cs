using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Sms;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط سرویس ارسال پیامک
    /// </summary>
    public interface ISmsService
    {
        // متدهای موجود برای OTP
        Task<bool> SendOtpAsync(string phoneNumber, string otpCode, string templateType = "VerifyOtp");
        Task<string> GenerateOtpAsync();

        // متدهای جدید برای ارسال پیامک
        Task<ApiResponse<SendSmsResponseDto>> SendSmsAsync(SendSmsRequestDto request);
        Task<ApiResponse<SendBulkResponseDto>> SendBulkSmsAsync(SendBulkRequestDto request);
        Task<ApiResponse<SendArrayResponseDto>> SendArraySmsAsync(SendArrayRequestDto request);

        /// <summary>ارسال دستی از کنترلر SMS همراه ثبت delivery tracking</summary>
        Task<ApiResponse<SendSmsResponseDto>> SendManualSmsAsync(int userId, SendSmsRequestDto request);
        Task<ApiResponse<SendBulkResponseDto>> SendManualBulkSmsAsync(int userId, SendBulkRequestDto request);
        Task<ApiResponse<SendArrayResponseDto>> SendManualArraySmsAsync(int userId, SendArrayRequestDto request);

        Task<ApiResponse<DeliveryResponseDto>> GetDeliveryStatusAsync(long sid);
        Task<ApiResponse<InboxResponseDto>> GetInboxAsync(InboxRequestDto request);
        Task<ApiResponse<InfoResponseDto>> GetWalletInfoAsync();
    }
}



