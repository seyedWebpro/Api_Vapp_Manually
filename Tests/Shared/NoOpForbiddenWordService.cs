using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;

namespace Api_Vapp.Tests.Shared;

/// <summary>پیاده‌سازی no-op برای تست‌ها — هیچ متنی را مسدود نمی‌کند.</summary>
internal sealed class NoOpForbiddenWordService : IForbiddenWordService
{
    public Task<ApiResponse<List<ForbiddenWordResponseDto>>> GetAllAsync(bool includeInactive = true, string? search = null) =>
        Task.FromResult(ApiResponse<List<ForbiddenWordResponseDto>>.CreateSuccess(new List<ForbiddenWordResponseDto>()));

    public Task<ApiResponse<List<string>>> GetActiveWordsAsync() =>
        Task.FromResult(ApiResponse<List<string>>.CreateSuccess(new List<string>()));

    public Task<ApiResponse<ForbiddenWordBulkCreateResultDto>> CreateAsync(CreateForbiddenWordDto dto) =>
        Task.FromResult(ApiResponse<ForbiddenWordBulkCreateResultDto>.CreateSuccess(new ForbiddenWordBulkCreateResultDto()));

    public Task<ApiResponse<ForbiddenWordResponseDto>> UpdateAsync(int id, UpdateForbiddenWordDto dto) =>
        Task.FromResult(ApiResponse<ForbiddenWordResponseDto>.NotFound("not used in tests"));

    public Task<ApiResponse<bool>> DeleteAsync(int id) =>
        Task.FromResult(ApiResponse<bool>.CreateSuccess(true));

    public Task<ApiResponse<ForbiddenWordValidateResultDto>> ValidateTextAsync(ForbiddenWordValidateRequestDto dto) =>
        Task.FromResult(ApiResponse<ForbiddenWordValidateResultDto>.CreateSuccess(new ForbiddenWordValidateResultDto()));

    public Task<ApiResponse<T>?> TryBlockIfContainsAsync<T>(params string?[] texts) =>
        Task.FromResult<ApiResponse<T>?>(null);
}
