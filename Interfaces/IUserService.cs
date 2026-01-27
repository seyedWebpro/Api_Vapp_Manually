using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.User;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط سرویس برای مدیریت کاربران
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// ایجاد کاربر جدید
        /// </summary>
        Task<ApiResponse<UserResponseDto>> CreateUserAsync(CreateUserDto createUserDto);

        /// <summary>
        /// دریافت کاربر بر اساس شناسه
        /// </summary>
        Task<ApiResponse<UserResponseDto>> GetUserByIdAsync(int id);

        /// <summary>
        /// دریافت لیست کاربران با pagination
        /// </summary>
        Task<ApiResponse<UserListResponseDto>> GetUsersAsync(int pageNumber = 1, int pageSize = 10, bool? isActive = null, bool? isDeleted = null);

        /// <summary>
        /// به‌روزرسانی اطلاعات کاربر
        /// فیلدهای null یا empty تغییر نمی‌کنند
        /// </summary>
        Task<ApiResponse<UserResponseDto>> UpdateUserAsync(int id, UpdateUserDto updateUserDto);

        /// <summary>
        /// حذف نرم کاربر (Soft Delete)
        /// </summary>
        Task<ApiResponse<bool>> DeleteUserAsync(int id);

        /// <summary>
        /// حذف سخت کاربر (Hard Delete) - حذف کامل از دیتابیس
        /// </summary>
        Task<ApiResponse<bool>> HardDeleteUserAsync(int id);

        /// <summary>
        /// بن کردن یا رفع بن کاربر
        /// </summary>
        Task<ApiResponse<UserResponseDto>> BanUserAsync(int id, BanUserDto banUserDto);

        /// <summary>
        /// فعال/غیرفعال کردن کاربر
        /// </summary>
        Task<ApiResponse<UserResponseDto>> ToggleUserActiveStatusAsync(int id, bool isActive);

        /// <summary>
        /// دریافت اطلاعات کامل پروفایل کاربر (شامل موجودی کیف پول)
        /// </summary>
        Task<ApiResponse<UserProfileDto>> GetUserProfileAsync(int userId);

        /// <summary>
        /// به‌روزرسانی پروفایل کاربر
        /// </summary>
        Task<ApiResponse<UserProfileDto>> UpdateUserProfileAsync(int userId, UpdateUserProfileDto updateDto);

        /// <summary>
        /// آپلود عکس پروفایل کاربر
        /// </summary>
        Task<ApiResponse<string>> UploadProfileImageAsync(int userId, Microsoft.AspNetCore.Http.IFormFile imageFile);

        /// <summary>
        /// حذف عکس پروفایل کاربر
        /// </summary>
        Task<ApiResponse<bool>> DeleteProfileImageAsync(int userId);
    }
}

