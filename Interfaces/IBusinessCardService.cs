using Api_Vapp.DTOs.BusinessCard;
using Api_Vapp.DTOs.Common;

namespace Api_Vapp.Interfaces
{
    public interface IBusinessCardService
    {
        Task<ApiResponse<BusinessCardResponseDto>> CreateDraftAsync(int userId, CreateBusinessCardDto createDto);

        Task<ApiResponse<BusinessCardResponseDto>> UpdateInfoAsync(int id, int userId, UpdateBusinessCardInfoDto? updateDto);

        Task<ApiResponse<BusinessCardResponseDto>> UpdateSectionsAsync(int id, int userId, UpdateBusinessCardSectionsDto? updateDto);

        Task<ApiResponse<BusinessCardResponseDto>> PublishAsync(int id, int userId, PublishBusinessCardDto? publishDto = null);

        Task<ApiResponse<BusinessCardListResponseDto>> GetCardsAsync(int userId, int pageNumber = 1, int pageSize = 10);

        Task<ApiResponse<BusinessCardResponseDto>> GetByIdAsync(int id, int userId);

        Task<ApiResponse<bool>> DeleteAsync(int id, int userId);

        Task<ApiResponse<BusinessCardResponseDto>> SetActiveStatusAsync(int id, int userId, bool isActive);
    }
}
