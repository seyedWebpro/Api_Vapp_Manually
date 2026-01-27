using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.UserRole;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط سرویس برای مدیریت روابط کاربر-نقش
    /// </summary>
    public interface IUserRoleService
    {
        /// <summary>
        /// ایجاد رابطه کاربر-نقش
        /// </summary>
        Task<ApiResponse<UserRoleResponseDto>> CreateUserRoleAsync(CreateUserRoleDto createUserRoleDto);

        /// <summary>
        /// دریافت رابطه کاربر-نقش بر اساس شناسه
        /// </summary>
        Task<ApiResponse<UserRoleResponseDto>> GetUserRoleByIdAsync(int id);

        /// <summary>
        /// دریافت نقش‌های یک کاربر
        /// </summary>
        Task<ApiResponse<List<UserRoleResponseDto>>> GetUserRolesAsync(int userId);

        /// <summary>
        /// دریافت کاربران یک نقش
        /// </summary>
        Task<ApiResponse<List<UserRoleResponseDto>>> GetRoleUsersAsync(int roleId);

        /// <summary>
        /// دریافت لیست روابط کاربر-نقش با pagination
        /// </summary>
        Task<ApiResponse<UserRoleListResponseDto>> GetUserRolesListAsync(int pageNumber = 1, int pageSize = 10, int? userId = null, int? roleId = null, bool? isActive = null);

        /// <summary>
        /// به‌روزرسانی رابطه کاربر-نقش
        /// فیلدهای null یا empty تغییر نمی‌کنند
        /// </summary>
        Task<ApiResponse<UserRoleResponseDto>> UpdateUserRoleAsync(int id, UpdateUserRoleDto updateUserRoleDto);

        /// <summary>
        /// حذف نرم رابطه کاربر-نقش (Soft Delete)
        /// </summary>
        Task<ApiResponse<bool>> DeleteUserRoleAsync(int id);

        /// <summary>
        /// حذف سخت رابطه کاربر-نقش (Hard Delete)
        /// </summary>
        Task<ApiResponse<bool>> HardDeleteUserRoleAsync(int id);

        /// <summary>
        /// فعال/غیرفعال کردن رابطه کاربر-نقش
        /// </summary>
        Task<ApiResponse<UserRoleResponseDto>> ToggleUserRoleActiveStatusAsync(int id, bool isActive);
    }
}

