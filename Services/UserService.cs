using Api_Vapp.Constants;
using Api_Vapp.DTOs.Auth;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.File;
using Api_Vapp.DTOs.User;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;

namespace Api_Vapp.Services
{
    /// <summary>
    /// پیاده‌سازی سرویس مدیریت کاربران
    /// </summary>
    public class UserService : IUserService
    {
        private const int ChangePhoneOtpExpirationMinutes = 5;
        private const int ChangePhoneMaxOtpAttempts = 5;
        private const int ChangePhoneOtpLockoutMinutes = 15;
        private const int ChangePhoneOtpRateLimitMinutes = 2;

        private readonly IUserRepository _userRepository;
        private readonly Api_Vapp.Data.Api_Context _context;
        private readonly ILogger<UserService> _logger;
        private readonly IFileUploadService _fileUploadService;
        private readonly IRefreshTokenService _refreshTokenService;
        private readonly IWalletReferralService _walletReferralService;
        private readonly IAuditService _audit;
        private readonly IUserPushNotifier _pushNotifier;
        private readonly IMemoryCache _cache;
        private readonly ISmsService _smsService;
        private readonly IHostEnvironment _environment;

        public UserService(
            IUserRepository userRepository, 
            Api_Vapp.Data.Api_Context context, 
            ILogger<UserService> logger,
            IFileUploadService fileUploadService,
            IRefreshTokenService refreshTokenService,
            IWalletReferralService walletReferralService,
            IAuditService audit,
            IUserPushNotifier pushNotifier,
            IMemoryCache cache,
            ISmsService smsService,
            IHostEnvironment environment)
        {
            _userRepository = userRepository;
            _context = context;
            _logger = logger;
            _fileUploadService = fileUploadService;
            _refreshTokenService = refreshTokenService;
            _walletReferralService = walletReferralService;
            _audit = audit;
            _pushNotifier = pushNotifier;
            _cache = cache;
            _smsService = smsService;
            _environment = environment;
        }

        private static object SafeUserSnapshot(User user) => new
        {
            id = user.Id,
            phoneNumber = user.PhoneNumber,
            fullName = user.FullName,
            nationalId = user.NationalId,
            email = user.Email,
            isActive = user.IsActive,
            isPhoneVerified = user.IsPhoneVerified,
            canViewNumberSeekerPhones = user.CanViewNumberSeekerPhones
        };

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
                    CanViewNumberSeekerPhones = createUserDto.CanViewNumberSeekerPhones,
                    CreatedAt = DateTime.UtcNow
                };

                var createdUser = await _userRepository.AddAsync(user);
                await _walletReferralService.EnsureReferralCodeAsync(createdUser);

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

                var beforeSnapshot = SafeUserSnapshot(user);

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

                if (updateUserDto.CanViewNumberSeekerPhones.HasValue)
                {
                    user.CanViewNumberSeekerPhones = updateUserDto.CanViewNumberSeekerPhones.Value;
                }

                // به‌روزرسانی زمان آخرین تغییر
                user.UpdatedAt = DateTime.UtcNow;

                var updatedUser = await _userRepository.UpdateAsync(user);

                if (wasActive && !updatedUser.IsActive)
                {
                    await InvalidateUserSessionsAsync(id, "deactivate");
                }

                _logger.LogInformation("User updated successfully with ID: {UserId}", id);

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.User,
                    Action = AuditActions.UserUpdated,
                    EntityType = AuditEntityTypes.User,
                    EntityId = updatedUser.Id.ToString(),
                    TargetUserId = updatedUser.Id,
                    Before = beforeSnapshot,
                    After = SafeUserSnapshot(updatedUser)
                });

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
                // آزادسازی شماره/کدملی برای ثبت‌نام مجدد بعد از حذف نرم
                // تا با unique index روی Users.PhoneNumber/NationalId تداخل ایجاد نشود.
                var deletedMarker = $"deleted-{user.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}";
                user.PhoneNumber = deletedMarker;
                if (!string.IsNullOrWhiteSpace(user.NationalId))
                {
                    user.NationalId = $"{user.NationalId}-deleted-{user.Id}";
                }
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

                var beforeSnapshot = SafeUserSnapshot(user);

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

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.User,
                    Action = banUserDto.IsBanned ? AuditActions.UserDeactivated : AuditActions.UserActivated,
                    EntityType = AuditEntityTypes.User,
                    EntityId = updatedUser.Id.ToString(),
                    TargetUserId = updatedUser.Id,
                    Before = beforeSnapshot,
                    After = SafeUserSnapshot(updatedUser),
                    Metadata = new { isBanned = banUserDto.IsBanned }
                });

                var banPush = PushNotificationCopy.AccountStatusChanged(updatedUser.IsActive);
                await _pushNotifier.NotifyAsync(
                    id,
                    NotificationCategory.ImportantNotifications,
                    banPush.Title,
                    banPush.Body);

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

                var beforeSnapshot = SafeUserSnapshot(user);

                user.IsActive = isActive;
                user.UpdatedAt = DateTime.UtcNow;
                var updatedUser = await _userRepository.UpdateAsync(user);

                if (!isActive)
                {
                    await InvalidateUserSessionsAsync(id, "deactivate");
                }

                var message = isActive ? "کاربر با موفقیت فعال شد" : "کاربر با موفقیت غیرفعال شد";

                _logger.LogInformation("User active status toggled to {Status} for ID: {UserId}", isActive, id);

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.User,
                    Action = isActive ? AuditActions.UserActivated : AuditActions.UserDeactivated,
                    EntityType = AuditEntityTypes.User,
                    EntityId = updatedUser.Id.ToString(),
                    TargetUserId = updatedUser.Id,
                    Before = beforeSnapshot,
                    After = SafeUserSnapshot(updatedUser)
                });

                var statusPush = PushNotificationCopy.AccountStatusChanged(isActive);
                await _pushNotifier.NotifyAsync(
                    id,
                    NotificationCategory.ImportantNotifications,
                    statusPush.Title,
                    statusPush.Body);

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

                var referralCode = await _walletReferralService.EnsureReferralCodeAsync(user);
                var referralInfo = await _walletReferralService.GetReferralInfoAsync(userId);

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
                    ReferralCode = referralCode,
                    ReferralEnabled = referralInfo.Data?.IsEnabled ?? false,
                    ReferralDiscountPercent = referralInfo.Data?.DiscountPercent ?? 0,
                    ReferralBonusPercent = referralInfo.Data?.BonusPercent ?? 0,
                    ReferralDescription = referralInfo.Data?.Description,
                    IsActive = user.IsActive,
                    IsPhoneVerified = user.IsPhoneVerified,
                    CanViewNumberSeekerPhones = user.CanViewNumberSeekerPhones,
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

                // تغییر شماره فقط از طریق OTP (profile/change-phone/*)
                if (!string.IsNullOrWhiteSpace(updateDto.PhoneNumber))
                {
                    var trimmedPhoneNumber = updateDto.PhoneNumber.Trim();
                    if (!string.Equals(user.PhoneNumber, trimmedPhoneNumber, StringComparison.Ordinal))
                    {
                        return ApiResponse<UserProfileDto>.BadRequest(
                            "برای تغییر شماره موبایل باید کد تایید دریافت کنید",
                            errorCode: ErrorCodes.InvalidInput);
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
                return ApiResponse<UserProfileDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در به‌روزرسانی پروفایل کاربر {UserId}", userId);
                throw;
            }
        }

        public async Task<ApiResponse<ChangePhoneOtpResponseDto>> RequestChangePhoneAsync(
            int userId,
            RequestChangePhoneDto dto,
            string? ipAddress = null)
        {
            try
            {
                var validation = await ValidateChangePhoneRequestAsync(userId, dto.PhoneNumber);
                if (validation.ErrorResponse != null)
                    return validation.ErrorResponse;

                return await SendChangePhoneOtpInternalAsync(
                    validation.User!,
                    validation.NormalizedPhone!,
                    requireExistingPending: false,
                    ipAddress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting change-phone OTP for user {UserId}", userId);
                return ApiResponse<ChangePhoneOtpResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<ChangePhoneOtpResponseDto>> ResendChangePhoneOtpAsync(
            int userId,
            RequestChangePhoneDto dto,
            string? ipAddress = null)
        {
            try
            {
                var validation = await ValidateChangePhoneRequestAsync(userId, dto.PhoneNumber);
                if (validation.ErrorResponse != null)
                    return validation.ErrorResponse;

                return await SendChangePhoneOtpInternalAsync(
                    validation.User!,
                    validation.NormalizedPhone!,
                    requireExistingPending: true,
                    ipAddress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resending change-phone OTP for user {UserId}", userId);
                return ApiResponse<ChangePhoneOtpResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<UserProfileDto>> VerifyChangePhoneAsync(
            int userId,
            VerifyChangePhoneDto dto,
            string? ipAddress = null)
        {
            try
            {
                if (userId <= 0)
                    return ApiResponse<UserProfileDto>.BadRequest("شناسه کاربر نامعتبر است", errorCode: ErrorCodes.InvalidUserId);

                var newPhone = dto.PhoneNumber?.Trim() ?? string.Empty;
                if (!System.Text.RegularExpressions.Regex.IsMatch(newPhone, @"^09\d{9}$"))
                {
                    return ApiResponse<UserProfileDto>.BadRequest(
                        "فرمت شماره تلفن صحیح نیست. شماره باید با 09 شروع شود و 11 رقم باشد",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null || user.IsDeleted)
                    return ApiResponse<UserProfileDto>.NotFound("کاربر یافت نشد");

                if (string.Equals(user.PhoneNumber, newPhone, StringComparison.Ordinal))
                {
                    return ApiResponse<UserProfileDto>.BadRequest(
                        "شماره جدید با شماره فعلی یکسان است",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var attemptKey = ChangePhoneAttemptKey(userId);
                var attemptData = _cache.Get<OtpAttemptCacheDto>(attemptKey);
                if (attemptData?.LockedUntil != null && attemptData.LockedUntil > DateTime.UtcNow)
                {
                    var remainingMinutes = Math.Max(
                        1,
                        (int)Math.Ceiling((attemptData.LockedUntil.Value - DateTime.UtcNow).TotalMinutes));

                    _logger.LogWarning(
                        "Change-phone OTP locked for user {UserId} from IP {IpAddress}",
                        userId,
                        ipAddress);

                    return ApiResponse<UserProfileDto>.Error(
                        $"به دلیل تلاش‌های ناموفق، تا {remainingMinutes} دقیقه امکان تأیید وجود ندارد",
                        423,
                        errorCode: ErrorCodes.OtpLocked);
                }

                var otpCacheKey = ChangePhoneOtpKey(userId);
                if (!_cache.TryGetValue(otpCacheKey, out ChangePhoneOtpCacheDto? cached) || cached == null)
                {
                    return ApiResponse<UserProfileDto>.BadRequest(
                        ControlledErrorHelper.OtpExpired,
                        errorCode: ErrorCodes.OtpExpired);
                }

                if (cached.UserId != userId ||
                    !string.Equals(cached.NewPhoneNumber, newPhone, StringComparison.Ordinal))
                {
                    return ApiResponse<UserProfileDto>.BadRequest(
                        "شماره موبایل با درخواست تغییر مطابقت ندارد. لطفاً مجدداً کد تایید دریافت کنید",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var cachedOtp = cached.OtpCode?.Trim() ?? string.Empty;
                var userOtp = dto.OtpCode?.Trim() ?? string.Empty;

                // DEV ONLY — TODO(production): قبل از release حذف شود (جستجو: DEV-OTP-VERIFY)
                _logger.LogInformation(
                    "[DEV-OTP-VERIFY] ChangePhone Cached OTP: {CachedOtp}, User Input: {UserOtp}, UserId: {UserId}",
                    cachedOtp,
                    userOtp,
                    userId);

                if (!string.Equals(cachedOtp, userOtp, StringComparison.Ordinal))
                {
                    attemptData ??= new OtpAttemptCacheDto
                    {
                        AttemptCount = 0,
                        FirstAttemptTime = DateTime.UtcNow
                    };
                    attemptData.AttemptCount++;

                    if (attemptData.AttemptCount >= ChangePhoneMaxOtpAttempts)
                    {
                        attemptData.LockedUntil = DateTime.UtcNow.AddMinutes(ChangePhoneOtpLockoutMinutes);
                        _logger.LogWarning(
                            "Change-phone OTP attempts exceeded for user {UserId} from IP {IpAddress}",
                            userId,
                            ipAddress);
                    }

                    SetChangePhoneCacheData(attemptKey, attemptData, ChangePhoneOtpLockoutMinutes + 5);

                    return ApiResponse<UserProfileDto>.BadRequest(
                        ControlledErrorHelper.OtpIncorrect,
                        errorCode: ErrorCodes.OtpIncorrect);
                }

                // بررسی مجدد تکراری نبودن (ممکن است بین request و verify ثبت شده باشد)
                var existingUser = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.PhoneNumber == newPhone && u.Id != userId);
                if (existingUser != null)
                {
                    _cache.Remove(otpCacheKey);
                    _cache.Remove(attemptKey);
                    return ApiResponse<UserProfileDto>.BadRequest(
                        "این شماره تلفن قابل استفاده نیست",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var oldPhone = user.PhoneNumber;
                user.PhoneNumber = newPhone;
                user.IsPhoneVerified = true;
                user.UpdatedAt = DateTime.UtcNow;
                await _userRepository.UpdateAsync(user);

                _cache.Remove(otpCacheKey);
                _cache.Remove(attemptKey);

                _logger.LogInformation(
                    "Phone changed for user {UserId} from {OldPhone} to {NewPhone} from IP {IpAddress}",
                    userId,
                    oldPhone,
                    newPhone,
                    ipAddress);

                var profileResult = await GetUserProfileAsync(userId);
                if (profileResult.Success && profileResult.Data != null)
                {
                    profileResult.Message = "شماره موبایل با موفقیت تغییر کرد";
                }

                return profileResult;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error verifying change-phone for user {UserId}", userId);
                return ApiResponse<UserProfileDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying change-phone for user {UserId}", userId);
                return ApiResponse<UserProfileDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private async Task<(
            User? User,
            string? NormalizedPhone,
            ApiResponse<ChangePhoneOtpResponseDto>? ErrorResponse)> ValidateChangePhoneRequestAsync(
            int userId,
            string? phoneNumber)
        {
            if (userId <= 0)
            {
                return (null, null, ApiResponse<ChangePhoneOtpResponseDto>.BadRequest(
                    "شناسه کاربر نامعتبر است",
                    errorCode: ErrorCodes.InvalidUserId));
            }

            var newPhone = phoneNumber?.Trim() ?? string.Empty;
            if (!System.Text.RegularExpressions.Regex.IsMatch(newPhone, @"^09\d{9}$"))
            {
                return (null, null, ApiResponse<ChangePhoneOtpResponseDto>.BadRequest(
                    "فرمت شماره تلفن صحیح نیست. شماره باید با 09 شروع شود و 11 رقم باشد",
                    errorCode: ErrorCodes.ValidationFailed));
            }

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null || user.IsDeleted)
            {
                return (null, null, ApiResponse<ChangePhoneOtpResponseDto>.NotFound("کاربر یافت نشد"));
            }

            if (string.Equals(user.PhoneNumber, newPhone, StringComparison.Ordinal))
            {
                return (null, null, ApiResponse<ChangePhoneOtpResponseDto>.BadRequest(
                    "شماره جدید با شماره فعلی یکسان است",
                    errorCode: ErrorCodes.InvalidInput));
            }

            var existingUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.PhoneNumber == newPhone && u.Id != userId);
            if (existingUser != null)
            {
                return (null, null, ApiResponse<ChangePhoneOtpResponseDto>.BadRequest(
                    "این شماره تلفن قابل استفاده نیست",
                    errorCode: ErrorCodes.InvalidInput));
            }

            return (user, newPhone, null);
        }

        private async Task<ApiResponse<ChangePhoneOtpResponseDto>> SendChangePhoneOtpInternalAsync(
            User user,
            string newPhone,
            bool requireExistingPending,
            string? ipAddress)
        {
            var (isRateLimited, retryAfterSeconds) = CheckChangePhoneRateLimit(newPhone);
            if (isRateLimited)
            {
                return new ApiResponse<ChangePhoneOtpResponseDto>
                {
                    StatusCode = 429,
                    Success = false,
                    Message = $"لطفاً {retryAfterSeconds} ثانیه صبر کنید و مجدداً تلاش کنید",
                    ErrorCode = ErrorCodes.OtpRateLimited,
                    Data = new ChangePhoneOtpResponseDto
                    {
                        ExpiresInSeconds = 0,
                        RetryAfterSeconds = retryAfterSeconds
                    }
                };
            }

            var otpCacheKey = ChangePhoneOtpKey(user.Id);

            if (requireExistingPending)
            {
                if (!_cache.TryGetValue(otpCacheKey, out ChangePhoneOtpCacheDto? existing) || existing == null)
                {
                    return ApiResponse<ChangePhoneOtpResponseDto>.BadRequest(
                        "درخواست تغییر شماره منقضی شده است. لطفاً مجدداً درخواست دهید",
                        errorCode: ErrorCodes.OtpExpired);
                }

                if (!string.Equals(existing.NewPhoneNumber, newPhone, StringComparison.Ordinal))
                {
                    return ApiResponse<ChangePhoneOtpResponseDto>.BadRequest(
                        "شماره موبایل با درخواست قبلی مطابقت ندارد",
                        errorCode: ErrorCodes.InvalidInput);
                }
            }

            var otpCode = await _smsService.GenerateOtpAsync();
            var cacheData = new ChangePhoneOtpCacheDto
            {
                OtpCode = otpCode,
                NewPhoneNumber = newPhone,
                UserId = user.Id
            };

            SetChangePhoneCacheData(otpCacheKey, cacheData, ChangePhoneOtpExpirationMinutes);
            SetChangePhoneRateLimit(newPhone, ChangePhoneOtpRateLimitMinutes);
            _cache.Remove(ChangePhoneAttemptKey(user.Id));

            var sent = await _smsService.SendOtpAsync(newPhone, otpCode, "VerifyOtp");
            if (!sent)
            {
                if (!_environment.IsDevelopment())
                {
                    _cache.Remove(otpCacheKey);
                    _cache.Remove(ChangePhoneRateLimitKey(newPhone));
                    _logger.LogError(
                        "Failed to send change-phone OTP SMS for user {UserId}, phone {PhoneNumber}",
                        user.Id,
                        newPhone);
                    return ApiResponse<ChangePhoneOtpResponseDto>.Error(
                        ControlledErrorHelper.SmsFailed,
                        503,
                        errorCode: ErrorCodes.SmsFailed);
                }

                _logger.LogWarning(
                    "Change-phone OTP SMS failed in Development — continuing with cached OTP for user {UserId}, phone {PhoneNumber}",
                    user.Id,
                    newPhone);
            }

            DevOtpLogger.Write(_logger, newPhone, otpCode, "ChangePhone");

            _logger.LogInformation(
                "Change-phone OTP ready for user {UserId}, phone {PhoneNumber}, smsSent {SmsSent}, from IP {IpAddress}",
                user.Id,
                newPhone,
                sent,
                ipAddress);

            return ApiResponse<ChangePhoneOtpResponseDto>.CreateSuccess(
                new ChangePhoneOtpResponseDto
                {
                    ExpiresInSeconds = ChangePhoneOtpExpirationMinutes * 60,
                    RetryAfterSeconds = ChangePhoneOtpRateLimitMinutes * 60,
                    OtpCode = otpCode
                },
                sent ? "کد تایید به شماره جدید ارسال شد" : "کد تایید آماده است (پیامک در Development ارسال نشد)");
        }

        private (bool isRateLimited, int? retryAfterSeconds) CheckChangePhoneRateLimit(string phoneNumber)
        {
            var rateLimitKey = ChangePhoneRateLimitKey(phoneNumber);
            if (_cache.TryGetValue(rateLimitKey, out RateLimitInfoDto? rateLimitInfo) && rateLimitInfo != null)
            {
                if (rateLimitInfo.ExpiresAt > DateTime.UtcNow)
                {
                    var remainingSeconds = (int)Math.Ceiling((rateLimitInfo.ExpiresAt - DateTime.UtcNow).TotalSeconds);
                    if (remainingSeconds > 0)
                        return (true, remainingSeconds);
                }

                _cache.Remove(rateLimitKey);
            }

            return (false, null);
        }

        private void SetChangePhoneRateLimit(string phoneNumber, int minutes)
        {
            var rateLimitInfo = new RateLimitInfoDto
            {
                ExpiresAt = DateTime.UtcNow.AddMinutes(minutes),
                IsActive = true
            };
            SetChangePhoneCacheData(ChangePhoneRateLimitKey(phoneNumber), rateLimitInfo, minutes);
        }

        private void SetChangePhoneCacheData<T>(string key, T data, int expirationMinutes)
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(expirationMinutes),
                Priority = CacheItemPriority.Normal,
                Size = 1
            };
            _cache.Set(key, data, cacheOptions);
        }

        private static string ChangePhoneOtpKey(int userId) => $"ChangePhoneOtp_{userId}";
        private static string ChangePhoneAttemptKey(int userId) => $"ChangePhoneOtpAttempt_{userId}";
        private static string ChangePhoneRateLimitKey(string phoneNumber) => $"ChangePhoneOtpRateLimit_{phoneNumber}";

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

                // اعتبارسنجی امن فایل (نوع، پسوند، magic bytes، حجم)
                var validationError = SecureFileValidator.ValidateImage(
                    imageFile,
                    SecureFileValidator.ProfileImageMaxBytes,
                    "۵ مگابایت");
                if (validationError != null)
                    return ApiResponse<string>.BadRequest(validationError);

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
                    return ApiResponse<bool>.InternalServerError(ControlledErrorHelper.Unexpected);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "خطا در حذف عکس پروفایل کاربر {UserId}", userId);
                    return ApiResponse<bool>.InternalServerError(ControlledErrorHelper.Unexpected);
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
                CanViewNumberSeekerPhones = user.CanViewNumberSeekerPhones,
                IsDeleted = user.IsDeleted,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                LastLoginAt = user.LastLoginAt
            };
        }
    }
}

