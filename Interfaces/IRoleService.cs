using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Role;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط سرویس برای مدیریت نقش‌ها
    /// </summary>
    public interface IRoleService
    {
        /// <summary>
        /// ایجاد نقش جدید
        /// </summary>
        Task<ApiResponse<RoleResponseDto>> CreateRoleAsync(CreateRoleDto createRoleDto);

        /// <summary>
        /// دریافت نقش بر اساس شناسه
        /// </summary>
        Task<ApiResponse<RoleResponseDto>> GetRoleByIdAsync(int id);

        /// <summary>
        /// دریافت لیست نقش‌ها با pagination
        /// </summary>
        Task<ApiResponse<RoleListResponseDto>> GetRolesAsync(int pageNumber = 1, int pageSize = 10, bool? isActive = null, bool? isDeleted = null);

        /// <summary>
        /// دریافت نقش‌های فعال
        /// </summary>
        Task<ApiResponse<List<RoleResponseDto>>> GetActiveRolesAsync();

        /// <summary>
        /// به‌روزرسانی اطلاعات نقش
        /// فیلدهای null یا empty تغییر نمی‌کنند
        /// </summary>
        Task<ApiResponse<RoleResponseDto>> UpdateRoleAsync(int id, UpdateRoleDto updateRoleDto);

        /// <summary>
        /// حذف نرم نقش (Soft Delete)
        /// </summary>
        Task<ApiResponse<bool>> DeleteRoleAsync(int id);

        /// <summary>
        /// حذف سخت نقش (Hard Delete) - حذف کامل از دیتابیس
        /// </summary>
        Task<ApiResponse<bool>> HardDeleteRoleAsync(int id);

        /// <summary>
        /// فعال/غیرفعال کردن نقش
        /// </summary>
        Task<ApiResponse<RoleResponseDto>> ToggleRoleActiveStatusAsync(int id, bool isActive);
    }
}

