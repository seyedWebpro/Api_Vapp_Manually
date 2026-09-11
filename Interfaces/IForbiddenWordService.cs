using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;

namespace Api_Vapp.Interfaces
{
    public interface IForbiddenWordService
    {
        Task<ApiResponse<List<ForbiddenWordResponseDto>>> GetAllAsync(bool includeInactive = true, string? search = null);
        Task<ApiResponse<List<string>>> GetActiveWordsAsync();
        Task<ApiResponse<ForbiddenWordBulkCreateResultDto>> CreateAsync(CreateForbiddenWordDto dto);
        Task<ApiResponse<ForbiddenWordResponseDto>> UpdateAsync(int id, UpdateForbiddenWordDto dto);
        Task<ApiResponse<bool>> DeleteAsync(int id);
        Task<ApiResponse<ForbiddenWordValidateResultDto>> ValidateTextAsync(ForbiddenWordValidateRequestDto dto);

        /// <summary>
        /// اگر متن‌ها شامل کلمه فیلتر باشند، پاسخ BadRequest برمی‌گرداند؛ در غیر این صورت null.
        /// </summary>
        Task<ApiResponse<T>?> TryBlockIfContainsAsync<T>(params string?[] texts);
    }
}
