using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Contact;
using Api_Vapp.DTOs.Message;
using Api_Vapp.DTOs.UserForm;

namespace Api_Vapp.Interfaces
{
    public interface IUserFormService
    {
        Task<ApiResponse<UserFormResponseDto>> CreateDraftAsync(int userId, CreateUserFormDto createDto);

        Task<ApiResponse<UserFormResponseDto>> UpdateInfoAsync(int id, int userId, UpdateUserFormInfoDto? updateDto);

        Task<ApiResponse<UserFormResponseDto>> UpdateFieldsAsync(int id, int userId, UpdateUserFormFieldsDto? updateDto);

        Task<ApiResponse<UserFormResponseDto>> PublishAsync(int id, int userId, PublishUserFormDto? publishDto = null);

        Task<ApiResponse<UserFormListResponseDto>> GetFormsAsync(int userId, int pageNumber = 1, int pageSize = 10, bool? isActive = null);

        Task<ApiResponse<UserFormResponseDto>> GetByIdAsync(int id, int userId);

        Task<ApiResponse<bool>> DeleteAsync(int id, int userId);

        Task<ApiResponse<UserFormResponseDto>> SetActiveStatusAsync(int id, int userId, bool isActive);

        Task<ApiResponse<DirectSendResultDto>> QuickSendUserFormAsync(int userId, QuickSendUserFormDto quickSendDto);

        Task<ApiResponse<UserFormSubmissionsPageDto>> GetSubmissionsAsync(
            int id,
            int userId,
            int pageNumber = 1,
            int pageSize = 10,
            string? searchTerm = null,
            DateTime? fromUtc = null,
            DateTime? toUtc = null);

        Task<ApiResponse<ExportExcelResultDto>> ExportSubmissionsToExcelAsync(int id, int userId);
    }
}
