using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.File;
using Api_Vapp.DTOs.User;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.IO;

namespace Api_Vapp.Services
{
    /// <summary>
    /// پیاده‌سازی سرویس مدیریت کاربران
    /// </summary>
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly Api_Vapp.Data.Api_Context _context;
        private readonly ILogger<UserService> _logger;
        private readonly IFileUploadService _fileUploadService;
        private readonly IRefreshTokenService _refreshTokenService;

        public UserService(
            IUserRepository userRepository, 
            Api_Vapp.Data.Api_Context context, 
            ILogger<UserService> logger,
            IFileUploadService fileUploadService,
            IRefreshTokenService refreshTokenService)
        {
            _userRepository = userRepository;
            _context = context;
            _logger = logger;
            _fileUploadService = fileUploadService;
            _refreshTokenService = refreshTokenService;
        }

        private async Task InvalidateUserSessionsAsync(int userId, string reason)
        {
            await _refreshTokenService.RevokeAllUserTokensAsync(userId);
            _logger.LogInformation(
                "All refresh tokens revoked for user {UserId}. Reason: {Reason}",
                userId,
                reason);
        }

        public async Task<ApiResponse<UserResponseDto>> CreateUserAsync(CreateUserDto createUserDto)
        {
            try
            {
                // بررسی وجود کاربر با شماره تلفن
                var existingUser = await _userRepository.GetByPhoneNumberAsync(createUserDto.PhoneNumber);
                if (existingUser != null && !existingUser.IsDeleted)
                {
                    _logger.LogWarning("Attempt to create user with existing phone number: {PhoneNumber}", createUserDto.PhoneNumber);
                    return ApiResponse<UserResponseDto>.BadRequest("کاربری با این شماره تلفن قبلاً ثبت شده است");
                }

                // ایجاد کاربر جدید
                var user = new User
                {
                    PhoneNumber = createUserDto.PhoneNumber,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(createUserDto.Password),
                    FullName = createUserDto.FullName,
                    NationalId = createUserDto.NationalId,
                    Email = createUserDto.Email,
                    IsActive = createUserDto.IsActive,
                    IsPhoneVerified = createUserDto.IsPhoneVerified,
                    CreatedAt = DateTime.UtcNow
                };

                var createdUser = await _userRepository.AddAsync(user);

                _logger.LogInformation("User created successfully with ID: {UserId}", createdUser.Id);

                return ApiResponse<UserResponseDto>.CreateSuccess(
                    MapToUserResponseDto(createdUser),
                    "کاربر با موفقیت ایجاد شد",
                    201
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user with phone number: {PhoneNumber}", createUserDto.PhoneNumber);
                throw; // اجازه می‌دهیم Global Exception Handler آن را مدیریت کند
            }
        }

        public async Task<ApiResponse<UserResponseDto>> GetUserByIdAsync(int id)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(id);

                if (user == null)
                {
                    _logger.LogWarning("User not found with ID: {UserId}", id);
                    return ApiResponse<UserResponseDto>.NotFound("کاربر یافت نشد");
                }

                return ApiResponse<UserResponseDto>.CreateSuccess(MapToUserResponseDto(user));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user with ID: {UserId}", id);
                throw;
            }
        }

        public async Task<ApiResponse<UserListResponseDto>> GetUsersAsync(int pageNumber = 1, int pageSize = 10, bool? isActive = null, bool? isDeleted = null)
        {
            try
            {
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                // استفاده از Query برای فیلتر و pagination
                var query = _context.Users.AsQueryable();

                // اعمال فیلترها
                if (isActive.HasValue)
                {
                    query = query.Where(u => u.IsActive == isActive.Value);
                }

                if (isDeleted.HasValue)
                {
                    query = query.Where(u => u.IsDeleted == isDeleted.Value);
                }
                else
                {
                    // به صورت پیش‌فرض فقط کاربران حذف نشده را نشان می‌دهیم
                    query = query.Where(u => !u.IsDeleted);
                }

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var users = await query
                    .OrderByDescending(u => u.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var userListResponse = new UserListResponseDto
                {
                    Users = users.Select(MapToUserResponseDto).ToList(),
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                return ApiResponse<UserListResponseDto>.CreateSuccess(userListResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users list");
                throw;
            }
        }

        public async Task<ApiResponse<UserResponseDto>> UpdateUserAsync(int id, UpdateUserDto updateUserDto)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(id);

                if (user == null)
                {
                    _logger.LogWarning("User not found for update with ID: {UserId}", id);
                    return ApiResponse<UserResponseDto>.NotFound("کاربر یافت نشد");
                }

                // به‌روزرسانی فیلدها - فقط اگر مقدار داده شده باشد (null یا empty نباشد)
                if (!string.IsNullOrWhiteSpace(updateUserDto.PhoneNumber))
                {
                    // بررسی تکراری نبودن شماره تلفن
                    var existingUser = await _userRepository.GetByPhoneNumberAsync(updateUserDto.PhoneNumber);
                    if (existingUser != null && existingUser.Id != id && !existingUser.IsDeleted)
                    {
                        return ApiResponse<UserResponseDto>.BadRequest("کاربری با این شماره تلفن قبلاً ثبت شده است");
                    }
                    user.PhoneNumber = updateUserDto.PhoneNumber;
                }

                if (!string.IsNullOrWhiteSpace(updateUserDto.Password))
                {
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(updateUserDto.Password);
                }

                if (updateUserDto.FullName != null)
                {
                    user.FullName = updateUserDto.FullName;
                }

                if (!string.IsNullOrWhiteSpace(updateUserDto.NationalId))
                {
                    user.NationalId = updateUserDto.NationalId;
                }

                if (updateUserDto.Email != null)
                {
                    user.Email = updateUserDto.Email;
                }

                var wasActive = user.IsActive;

                if (updateUserDto.IsActive.HasValue)
                {
                    user.IsActive = updateUserDto.IsActive.Value;
                }

                if (updateUserDto.IsPhoneVerified.HasValue)
                {
                    user.IsPhoneVerified = updateUserDto.IsPhoneVerified.Value;
                }

                // به‌روزرسانی زمان آخرین تغییر
                user.UpdatedAt = DateTime.UtcNow;

                var updatedUser = await _userRepository.UpdateAsync(user);

                if (wasActive && !updatedUser.IsActive)
                {
                    await InvalidateUserSessionsAsync(id, "deactivate");
                }

                _logger.LogInformation("User updated successfully with ID: {UserId}", id);

                return ApiResponse<UserResponseDto>.CreateSuccess(
                    MapToUserResponseDto(updatedUser),
                    "اطلاعات کاربر با موفقیت به‌روزرسانی شد"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user with ID: {UserId}", id);
                throw;
            }
        }

        public async Task<ApiResponse<bool>> DeleteUserAsync(int id)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(id);

                if (user == null)
                {
                    _logger.LogWarning("User not found for delete with ID: {UserId}", id);
                    return ApiResponse<bool>.NotFound("کاربر یافت نشد");
                }

                // Soft Delete
                user.IsDeleted = true;
                user.IsActive = false;
                user.UpdatedAt = DateTime.UtcNow;
                await _userRepository.UpdateAsync(user);
                await InvalidateUserSessionsAsync(id, "soft-delete");

                _logger.LogInformation("User soft deleted successfully with ID: {UserId}", id);

                return ApiResponse<bool>.CreateSuccess(true, "کاربر با موفقیت حذف شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user with ID: {UserId}", id);
                throw;
            }
        }

        public async Task<ApiResponse<bool>> HardDeleteUserAsync(int id)
        {
            try
            {
                // برای حذف سخت باید از context استفاده کنیم چون GetByIdAsync فقط کاربران حذف نشده را برمی‌گرداند
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null)
                {
                    _logger.LogWarning("User not found for hard delete with ID: {UserId}", id);
                    return ApiResponse<bool>.NotFound("کاربر یافت نشد");
                }

                // استفاده از Transaction برای اطمینان از یکپارچگی داده‌ها
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // 1. حذف RefreshToken ها (به صورت خودکار با Cascade حذف می‌شوند، اما برای اطمینان)
                    var refreshTokens = await _context.RefreshTokens
                        .Where(rt => rt.UserId == id)
                        .ToListAsync();

                    if (refreshTokens.Any())
                    {
                        _context.RefreshTokens.RemoveRange(refreshTokens);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Deleted {Count} refresh tokens before hard delete of user {UserId}", 
                            refreshTokens.Count, id);
                    }

                    // 2. Hard Delete - حذف کامل کاربر از دیتابیس
                    await _userRepository.DeleteAsync(user);

                    await transaction.CommitAsync();

                    _logger.LogWarning("User hard deleted successfully with ID: {UserId}. RefreshTokens: {TokenCount}", 
                        id, refreshTokens.Count);

                    return ApiResponse<bool>.CreateSuccess(true, 
                        $"کاربر به طور کامل از دیتابیس حذف شد. {refreshTokens.Count} توکن حذف شد.");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error during hard delete transaction for user {UserId}. Transaction rolled back.", id);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hard deleting user with ID: {UserId}", id);
                throw;
            }
        }

        public async Task<ApiResponse<UserResponseDto>> BanUserAsync(int id, BanUserDto banUserDto)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(id);

                if (user == null)
                {
                    _logger.LogWarning("User not found for ban/unban with ID: {UserId}", id);
                    return ApiResponse<UserResponseDto>.NotFound("کاربر یافت نشد");
                }

                // بن کردن = غیرفعال کردن
                user.IsActive = !banUserDto.IsBanned;
                user.UpdatedAt = DateTime.UtcNow;
                var updatedUser = await _userRepository.UpdateAsync(user);

                if (banUserDto.IsBanned)
                {
                    await InvalidateUserSessionsAsync(id, "ban");
                }

                var message = banUserDto.IsBanned ? "کاربر با موفقیت بن شد" : "بن کاربر با موفقیت رفع شد";

                _logger.LogInformation("User {Action} with ID: {UserId}", banUserDto.IsBanned ? "banned" : "unbanned", id);

                return ApiResponse<UserResponseDto>.CreateSuccess(
                    MapToUserResponseDto(updatedUser),
                    message
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error banning/unbanning user with ID: {UserId}", id);
                throw;
            }
        }

        public async Task<ApiResponse<UserResponseDto>> ToggleUserActiveStatusAsync(int id, bool isActive)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(id);

                if (user == null)
                {
                    _logger.LogWarning("User not found for toggle active status with ID: {UserId}", id);
                    return ApiResponse<UserResponseDto>.NotFound("کاربر یافت نشد");
                }

                user.IsActive = isActive;
                user.UpdatedAt = DateTime.UtcNow;
                var updatedUser = await _userRepository.UpdateAsync(user);

                if (!isActive)
                {
                    await InvalidateUserSessionsAsync(id, "deactivate");
                }

                var message = isActive ? "کاربر با موفقیت فعال شد" : "کاربر با موفقیت غیرفعال شد";

                _logger.LogInformation("User active status toggled to {Status} for ID: {UserId}", isActive, id);

                return ApiResponse<UserResponseDto>.CreateSuccess(
                    MapToUserResponseDto(updatedUser),
                    message
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling user active status with ID: {UserId}", id);
                throw;
            }
        }

        public async Task<ApiResponse<UserProfileDto>> GetUserProfileAsync(int userId)
        {
            try
            {
                if (userId <= 0)
                {
                    return ApiResponse<UserProfileDto>.BadRequest("شناسه کاربر نامعتبر است");
                }

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null || user.IsDeleted)
                {
                    _logger.LogWarning("درخواست پروفایل برای کاربر نامعتبر یا حذف شده: {UserId}", userId);
                    return ApiResponse<UserProfileDto>.NotFound("کاربر یافت نشد");
                }

                if (!user.IsActive)
                {
                    _logger.LogWarning("درخواست پروفایل برای کاربر غیرفعال: {UserId}", userId);
                    return ApiResponse<UserProfileDto>.Forbidden(ControlledErrorHelper.InactiveUserAccount);
                }

                // دریافت موجودی کیف پول مستقیماً از مدل User
                decimal walletBalance = user.WalletBalance;

                string? profileImageUrl = null;
                try
                {
                    if (!string.IsNullOrWhiteSpace(user.ProfileImagePath))
                    {
                        profileImageUrl = _fileUploadService.GetFileUrl(user.ProfileImagePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "خطا در تولید URL عکس پروفایل کاربر {UserId}", userId);
                    // در صورت خطا، URL null باقی می‌ماند
                }

                var profileDto = new UserProfileDto
                {
                    Id = user.Id,
                    PhoneNumber = user.PhoneNumber ?? string.Empty,
                    FullName = user.FullName,
                    NationalId = user.NationalId,
                    Email = user.Email,
                    ProfileImagePath = user.ProfileImagePath,
                    ProfileImageUrl = profileImageUrl,
                    WalletBalance = walletBalance,
                    FormattedWalletBalance = $"{walletBalance:N0} تومان",
                    IsActive = user.IsActive,
                    IsPhoneVerified = user.IsPhoneVerified,
                    CreatedAt = user.CreatedAt,
                    UpdatedAt = user.UpdatedAt,
                    LastLoginAt = user.LastLoginAt
                };

                return ApiResponse<UserProfileDto>.CreateSuccess(profileDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت پروفایل کاربر {UserId}", userId);
                throw;
            }
        }

        public async Task<ApiResponse<UserProfileDto>> UpdateUserProfileAsync(int userId, UpdateUserProfileDto updateDto)
        {
            try
            {
                if (userId <= 0)
                {
                    return ApiResponse<UserProfileDto>.BadRequest("شناسه کاربر نامعتبر است");
                }

                if (updateDto == null)
                {
                    return ApiResponse<UserProfileDto>.BadRequest("اطلاعات به‌روزرسانی ارسال نشده است");
                }

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null || user.IsDeleted)
                {
                    _logger.LogWarning("درخواست به‌روزرسانی پروفایل برای کاربر نامعتبر یا حذف شده: {UserId}", userId);
                    return ApiResponse<UserProfileDto>.NotFound("کاربر یافت نشد");
                }

                bool hasChanges = false;

                // به‌روزرسانی فیلدها
                if (!string.IsNullOrWhiteSpace(updateDto.FullName))
                {
                    var trimmedFullName = updateDto.FullName.Trim();
                    if (user.FullName != trimmedFullName)
                    {
                        user.FullName = trimmedFullName;
                        hasChanges = true;
                    }
                }

                if (!string.IsNullOrWhiteSpace(updateDto.NationalId))
                {
                    var trimmedNationalId = updateDto.NationalId.Trim();
                    // بررسی فرمت کد ملی
                    if (trimmedNationalId.Length != 10 || !trimmedNationalId.All(char.IsDigit))
                    {
                        return ApiResponse<UserProfileDto>.BadRequest("کد ملی باید 10 رقم باشد");
                    }

                    // بررسی تکراری نبودن کد ملی
                    if (user.NationalId != trimmedNationalId)
                    {
                        var existingUser = await _context.Users
                            .FirstOrDefaultAsync(u => u.NationalId == trimmedNationalId && u.Id != userId && !u.IsDeleted);
                        if (existingUser != null)
                        {
                            return ApiResponse<UserProfileDto>.BadRequest("کاربری با این کد ملی قبلاً ثبت شده است");
                        }
                        user.NationalId = trimmedNationalId;
                        hasChanges = true;
                    }
                }

                if (!string.IsNullOrWhiteSpace(updateDto.PhoneNumber))
                {
                    var trimmedPhoneNumber = updateDto.PhoneNumber.Trim();
                    // بررسی فرمت شماره تلفن
                    if (!System.Text.RegularExpressions.Regex.IsMatch(trimmedPhoneNumber, @"^09\d{9}$"))
                    {
                        return ApiResponse<UserProfileDto>.BadRequest("فرمت شماره تلفن صحیح نیست. شماره باید با 09 شروع شود و 11 رقم باشد");
                    }

                    // بررسی تکراری نبودن شماره تلفن
                    if (user.PhoneNumber != trimmedPhoneNumber)
                    {
                        var existingUser = await _userRepository.GetByPhoneNumberAsync(trimmedPhoneNumber);
                        if (existingUser != null && existingUser.Id != userId && !existingUser.IsDeleted)
                        {
                            return ApiResponse<UserProfileDto>.BadRequest("کاربری با این شماره تلفن قبلاً ثبت شده است");
                        }
                        user.PhoneNumber = trimmedPhoneNumber;
                        hasChanges = true;
                    }
                }

                if (!hasChanges)
                {
                    _logger.LogInformation("هیچ تغییری در پروفایل کاربر {UserId} اعمال نشد", userId);
                    // بازگرداندن پروفایل فعلی
                    return await GetUserProfileAsync(userId);
                }

                user.UpdatedAt = DateTime.UtcNow;
                await _userRepository.UpdateAsync(user);

                _logger.LogInformation("پروفایل کاربر {UserId} با موفقیت به‌روزرسانی شد", userId);

                // دریافت پروفایل به‌روزرسانی شده
                return await GetUserProfileAsync(userId);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "خطا در به‌روزرسانی دیتابیس برای کاربر {UserId}", userId);
                return ApiResponse<UserProfileDto>.InternalServerError("خطا در ذخیره‌سازی اطلاعات. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در به‌روزرسانی پروفایل کاربر {UserId}", userId);
                throw;
            }
        }

        public async Task<ApiResponse<string>> UploadProfileImageAsync(int userId, Microsoft.AspNetCore.Http.IFormFile imageFile)
        {
            try
            {
                if (userId <= 0)
                {
                    return ApiResponse<string>.BadRequest("شناسه کاربر نامعتبر است");
                }

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null || user.IsDeleted)
                {
                    _logger.LogWarning("درخواست آپلود عکس پروفایل برای کاربر نامعتبر یا حذف شده: {UserId}", userId);
                    return ApiResponse<string>.NotFound("کاربر یافت نشد");
                }

                // اعتبارسنجی فایل
                if (imageFile == null || imageFile.Length == 0)
                {
                    return ApiResponse<string>.BadRequest("فایل عکس ارسال نشده است. لطفاً یک فایل تصویری انتخاب کنید");
                }

                // بررسی نام فایل
                if (string.IsNullOrWhiteSpace(imageFile.FileName))
                {
                    return ApiResponse<string>.BadRequest("نام فایل نامعتبر است");
                }

                // بررسی نوع فایل
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
                var contentType = imageFile.ContentType?.ToLower() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(contentType) || !allowedTypes.Contains(contentType))
                {
                    return ApiResponse<string>.BadRequest("فرمت فایل نامعتبر است. فقط تصاویر JPEG, PNG, GIF و WebP مجاز است");
                }

                // بررسی پسوند فایل
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var fileExtension = Path.GetExtension(imageFile.FileName)?.ToLower();
                if (string.IsNullOrWhiteSpace(fileExtension) || !allowedExtensions.Contains(fileExtension))
                {
                    return ApiResponse<string>.BadRequest($"پسوند فایل '{fileExtension}' مجاز نیست. فقط {string.Join(", ", allowedExtensions)} مجاز است");
                }

                // بررسی اندازه فایل (حداکثر 5 مگابایت)
                const long maxFileSize = 5 * 1024 * 1024; // 5 MB
                if (imageFile.Length > maxFileSize)
                {
                    var fileSizeMB = Math.Round(imageFile.Length / (1024.0 * 1024.0), 2);
                    return ApiResponse<string>.BadRequest($"حجم فایل ({fileSizeMB} مگابایت) بیشتر از حد مجاز (5 مگابایت) است. لطفاً فایل کوچکتری انتخاب کنید");
                }

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // حذف عکس قبلی در صورت وجود
                    if (!string.IsNullOrWhiteSpace(user.ProfileImagePath))
                    {
                        try
                        {
                            await _fileUploadService.DeleteFileAsync(
                                user.ProfileImagePath,
                                FileUploadConstants.EntityType_User,
                                userId,
                                FileUploadConstants.SubFolder_Profile);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "خطا در حذف عکس قبلی کاربر {UserId}", userId);
                            // ادامه می‌دهیم حتی اگر حذف عکس قبلی با خطا مواجه شود
                        }
                    }

                    // آپلود عکس جدید
                    var relativePath = await _fileUploadService.UploadFileAsync(
                        imageFile,
                        FileUploadConstants.EntityType_User,
                        userId,
                        FileUploadConstants.SubFolder_Profile);

                    // به‌روزرسانی مسیر عکس در دیتابیس
                    user.ProfileImagePath = relativePath;
                    user.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    // Commit transaction
                    await transaction.CommitAsync();

                    var imageUrl = _fileUploadService.GetFileUrl(relativePath);

                    _logger.LogInformation("عکس پروفایل برای کاربر {UserId} با موفقیت آپلود شد", userId);

                    return ApiResponse<string>.CreateSuccess(imageUrl, "عکس پروفایل با موفقیت آپلود شد");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "خطا در آپلود عکس پروفایل برای کاربر {UserId}", userId);
                    
                    if (ex is ArgumentException)
                    {
                        return ApiResponse<string>.BadRequest(ControlledErrorHelper.SanitizeArgumentMessage(ex.Message, ControlledErrorHelper.FileUploadFailed));
                    }
                    
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در آپلود عکس پروفایل برای کاربر {UserId}", userId);
                throw;
            }
        }

        public async Task<ApiResponse<bool>> DeleteProfileImageAsync(int userId)
        {
            try
            {
                if (userId <= 0)
                {
                    return ApiResponse<bool>.BadRequest("شناسه کاربر نامعتبر است");
                }

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null || user.IsDeleted)
                {
                    _logger.LogWarning("درخواست حذف عکس پروفایل برای کاربر نامعتبر یا حذف شده: {UserId}", userId);
                    return ApiResponse<bool>.NotFound("کاربر یافت نشد");
                }

                if (string.IsNullOrWhiteSpace(user.ProfileImagePath))
                {
                    return ApiResponse<bool>.BadRequest("عکس پروفایلی برای حذف وجود ندارد");
                }

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var oldImagePath = user.ProfileImagePath;

                    // حذف فایل
                    try
                    {
                        await _fileUploadService.DeleteFileAsync(
                            oldImagePath,
                            FileUploadConstants.EntityType_User,
                            userId,
                            FileUploadConstants.SubFolder_Profile);
                    }
                    catch (FileNotFoundException)
                    {
                        _logger.LogWarning("فایل عکس پروفایل کاربر {UserId} در مسیر {Path} یافت نشد، اما از دیتابیس حذف می‌شود", userId, oldImagePath);
                        // ادامه می‌دهیم حتی اگر فایل وجود نداشته باشد
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "خطا در حذف فایل عکس پروفایل کاربر {UserId} از مسیر {Path}", userId, oldImagePath);
                        // ادامه می‌دهیم حتی اگر حذف فایل با خطا مواجه شود
                    }

                    // حذف مسیر از دیتابیس
                    user.ProfileImagePath = null;
                    user.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    _logger.LogInformation("عکس پروفایل کاربر {UserId} با موفقیت حذف شد", userId);

                    return ApiResponse<bool>.CreateSuccess(true, "عکس پروفایل با موفقیت حذف شد");
                }
                catch (DbUpdateException ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "خطا در به‌روزرسانی دیتابیس هنگام حذف عکس پروفایل کاربر {UserId}", userId);
                    return ApiResponse<bool>.InternalServerError("خطا در ذخیره‌سازی تغییرات. لطفاً دوباره تلاش کنید");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "خطا در حذف عکس پروفایل کاربر {UserId}", userId);
                    return ApiResponse<bool>.InternalServerError("خطا در حذف عکس. لطفاً دوباره تلاش کنید");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطای غیرمنتظره در حذف عکس پروفایل کاربر {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// تبدیل User به UserResponseDto
        /// </summary>
        private UserResponseDto MapToUserResponseDto(User user)
        {
            return new UserResponseDto
            {
                Id = user.Id,
                PhoneNumber = user.PhoneNumber,
                FullName = user.FullName,
                NationalId = user.NationalId,
                Email = user.Email,
                ProfileImagePath = user.ProfileImagePath,
                ProfileImageUrl = !string.IsNullOrWhiteSpace(user.ProfileImagePath) 
                    ? _fileUploadService.GetFileUrl(user.ProfileImagePath) 
                    : null,
                IsActive = user.IsActive,
                IsPhoneVerified = user.IsPhoneVerified,
                IsDeleted = user.IsDeleted,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                LastLoginAt = user.LastLoginAt
            };
        }
    }
}

