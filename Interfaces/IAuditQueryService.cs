using Api_Vapp.DTOs.Audit;
using Api_Vapp.DTOs.Common;

namespace Api_Vapp.Interfaces
{
    /// <summary>جستجو و خواندن لاگ‌های audit از DB.</summary>
    public interface IAuditQueryService
    {
        Task<ApiResponse<PagedResponse<AuditLogDto>>> SearchAsync(
            AuditSearchRequestDto request,
            CancellationToken cancellationToken = default);

        Task<ApiResponse<AuditLogDto>> GetByIdAsync(
            long id,
            CancellationToken cancellationToken = default);
    }
}
