using Api_Vapp.DTOs.BusinessCard;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Message;
using Microsoft.AspNetCore.Http;

namespace Api_Vapp.Interfaces
{
    public interface IBusinessCardService
    {
        Task<ApiResponse<BusinessCardResponseDto>> CreateDraftAsync(int userId, CreateBusinessCardDto createDto);

        Task<ApiResponse<BusinessCardResponseDto>> UpdateInfoAsync(int id, int userId, UpdateBusinessCardInfoDto? updateDto);

        Task<ApiResponse<BusinessCardResponseDto>> UpdateSectionsAsync(int id, int userId, UpdateBusinessCardSectionsDto? updateDto);

        Task<ApiResponse<BusinessCardResponseDto>> PublishAsync(int id, int userId, PublishBusinessCardDto? publishDto = null);

        Task<ApiResponse<BusinessCardListResponseDto>> GetCardsAsync(int userId, int pageNumber = 1, int pageSize = 10, bool? isActive = null);

        Task<ApiResponse<BusinessCardResponseDto>> GetByIdAsync(int id, int userId);

        Task<ApiResponse<bool>> DeleteAsync(int id, int userId);

        Task<ApiResponse<BusinessCardResponseDto>> SetActiveStatusAsync(int id, int userId, bool isActive);

        Task<ApiResponse<string>> UploadImageAsync(int id, int userId, IFormFile imageFile, string? imageType = null);

        /// <summary>
        /// ارسال سریع لینک عمومی کارت ویزیت به یک مخاطب (SMS)
        /// </summary>
        Task<ApiResponse<DirectSendResultDto>> QuickSendBusinessCardAsync(int userId, QuickSendBusinessCardDto quickSendDto);
    }
}
