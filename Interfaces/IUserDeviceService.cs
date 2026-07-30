using Api_Vapp.DTOs.Common;

namespace Api_Vapp.Interfaces
{
    public interface IUserDeviceService
    {
        /// <summary>
        /// ثبت یا به‌روزرسانی توکن FCM — ارسال دوباره خطا نمی‌دهد (upsert)
        /// </summary>
        Task<ApiResponse<object>> RegisterFcmTokenAsync(int userId, string token);
    }
}
