using Api_Vapp.DTOs.BusinessCard;
using Api_Vapp.DTOs.Common;

namespace Api_Vapp.Interfaces
{
    public interface IBusinessCardPublicService
    {
        Task<ApiResponse<BusinessCardPublicDto>> GetPublicCardAsync(string slug);

        /// <summary>پیش‌نمایش ادمین — بدون بررسی وضعیت تأیید</summary>
        Task<ApiResponse<BusinessCardPublicDto>> GetAdminPreviewByIdAsync(int id);
    }
}
