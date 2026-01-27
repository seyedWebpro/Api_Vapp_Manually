using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Role;
using Api_Vapp.DTOs.UserRole;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services
{
    /// <summary>
    /// پیاده‌سازی سرویس مدیریت روابط کاربر-نقش
    /// </summary>
    public class UserRoleService : IUserRoleService
    {
        private readonly IUserRoleRepository _userRoleRepository;
        private readonly IUserRepository _userRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly Api_Vapp.Data.Api_Context _context;
        private readonly ILogger<UserRoleService> _logger;

        public UserRoleService(
            IUserRoleRepository userRoleRepository,
            IUserRepository userRepository,
            IRoleRepository roleRepository,
            Api_Vapp.Data.Api_Context context,
            ILogger<UserRoleService> logger)
        {
            _userRoleRepository = userRoleRepository;
            _userRepository = userRepository;
            _roleRepository = roleRepository;
            _context = context;
            _logger = logger;
        }

        public async Task<ApiResponse<UserRoleResponseDto>> CreateUserRoleAsync(CreateUserRoleDto createUserRoleDto)
        {
            try
            {
                // بررسی وجود کاربر
                var user = await _userRepository.GetByIdAsync(createUserRoleDto.UserId);
                if (user == null)
                {
                    _logger.LogWarning("User not found with ID: {UserId}", createUserRoleDto.UserId);
                    return ApiResponse<UserRoleResponseDto>.NotFound("کاربر یافت نشد");
                }

                // بررسی وجود نقش
                var role = await _roleRepository.GetByIdAsync(createUserRoleDto.RoleId);
                if (role == null)
                {
                    _logger.LogWarning("Role not found with ID: {RoleId}", createUserRoleDto.RoleId);
                    return ApiResponse<UserRoleResponseDto>.NotFound("نقش یافت نشد");
                }

                // بررسی وجود رابطه قبلی
                var existingUserRole = await _userRoleRepository.GetUserRoleAsync(createUserRoleDto.UserId, createUserRoleDto.RoleId);
                if (existingUserRole != null && !existingUserRole.IsDeleted)
                {
                    // اگر رابطه حذف شده باشد، آن را فعال می‌کنیم
                    if (existingUserRole.IsDeleted)
                    {
                        existingUserRole.IsDeleted = false;
                        existingUserRole.IsActive = createUserRoleDto.IsActive;
                        existingUserRole.UpdatedAt = DateTime.UtcNow;
                        var updatedUserRole = await _userRoleRepository.UpdateAsync(existingUserRole);
                        return ApiResponse<UserRoleResponseDto>.CreateSuccess(
                            MapToUserRoleResponseDto(updatedUserRole),
                            "رابطه کاربر-نقش با موفقیت فعال شد",
                            200
                        );
                    }
                    return ApiResponse<UserRoleResponseDto>.BadRequest("این نقش قبلاً به کاربر اختصاص داده شده است");
                }

                // ایجاد رابطه جدید
                var userRole = new UserRole
                {
                    UserId = createUserRoleDto.UserId,
                    RoleId = createUserRoleDto.RoleId,
                    IsActive = createUserRoleDto.IsActive,
                    CreatedAt = DateTime.UtcNow
                };

                var createdUserRole = await _userRoleRepository.AddAsync(userRole);

                _logger.LogInformation("UserRole created successfully with ID: {UserRoleId}", createdUserRole.Id);

                return ApiResponse<UserRoleResponseDto>.CreateSuccess(
                    MapToUserRoleResponseDto(createdUserRole),
                    "رابطه کاربر-نقش با موفقیت ایجاد شد",
                    201
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user role for UserId: {UserId}, RoleId: {RoleId}", 
                    createUserRoleDto.UserId, createUserRoleDto.RoleId);
                throw;
            }
        }

        public async Task<ApiResponse<UserRoleResponseDto>> GetUserRoleByIdAsync(int id)
        {
            try
            {
                var userRole = await _userRoleRepository.GetByIdAsync(id);

                if (userRole == null)
                {
                    _logger.LogWarning("UserRole not found with ID: {UserRoleId}", id);
                    return ApiResponse<UserRoleResponseDto>.NotFound("رابطه کاربر-نقش یافت نشد");
                }

                // بارگذاری اطلاعات نقش
                await _context.Entry(userRole).Reference(ur => ur.Role).LoadAsync();

                return ApiResponse<UserRoleResponseDto>.CreateSuccess(MapToUserRoleResponseDto(userRole));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user role with ID: {UserRoleId}", id);
                throw;
            }
        }

        public async Task<ApiResponse<List<UserRoleResponseDto>>> GetUserRolesAsync(int userId)
        {
            try
            {
                // بررسی وجود کاربر
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found with ID: {UserId}", userId);
                    return ApiResponse<List<UserRoleResponseDto>>.NotFound("کاربر یافت نشد");
                }

                var userRoles = await _userRoleRepository.GetUserRolesAsync(userId);
                return ApiResponse<List<UserRoleResponseDto>>.CreateSuccess(
                    userRoles.Select(MapToUserRoleResponseDto).ToList()
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user roles for UserId: {UserId}", userId);
                throw;
            }
        }

        public async Task<ApiResponse<List<UserRoleResponseDto>>> GetRoleUsersAsync(int roleId)
        {
            try
            {
                // بررسی وجود نقش
                var role = await _roleRepository.GetByIdAsync(roleId);
                if (role == null)
                {
                    _logger.LogWarning("Role not found with ID: {RoleId}", roleId);
                    return ApiResponse<List<UserRoleResponseDto>>.NotFound("نقش یافت نشد");
                }

                var roleUsers = await _userRoleRepository.GetRoleUsersAsync(roleId);
                return ApiResponse<List<UserRoleResponseDto>>.CreateSuccess(
                    roleUsers.Select(MapToUserRoleResponseDto).ToList()
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting role users for RoleId: {RoleId}", roleId);
                throw;
            }
        }

        public async Task<ApiResponse<UserRoleListResponseDto>> GetUserRolesListAsync(int pageNumber = 1, int pageSize = 10, int? userId = null, int? roleId = null, bool? isActive = null)
        {
            try
            {
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                var query = _context.UserRoles.AsQueryable();

                // اعمال فیلترها
                if (userId.HasValue)
                {
                    query = query.Where(ur => ur.UserId == userId.Value);
                }

                if (roleId.HasValue)
                {
                    query = query.Where(ur => ur.RoleId == roleId.Value);
                }

                if (isActive.HasValue)
                {
                    query = query.Where(ur => ur.IsActive == isActive.Value);
                }

                // فقط روابط حذف نشده
                query = query.Where(ur => !ur.IsDeleted);

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var userRoles = await query
                    .Include(ur => ur.Role)
                    .OrderByDescending(ur => ur.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var userRoleListResponse = new UserRoleListResponseDto
                {
                    UserRoles = userRoles.Select(MapToUserRoleResponseDto).ToList(),
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                return ApiResponse<UserRoleListResponseDto>.CreateSuccess(userRoleListResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user roles list");
                throw;
            }
        }

        public async Task<ApiResponse<UserRoleResponseDto>> UpdateUserRoleAsync(int id, UpdateUserRoleDto updateUserRoleDto)
        {
            try
            {
                var userRole = await _userRoleRepository.GetByIdAsync(id);

                if (userRole == null)
                {
                    _logger.LogWarning("UserRole not found for update with ID: {UserRoleId}", id);
                    return ApiResponse<UserRoleResponseDto>.NotFound("رابطه کاربر-نقش یافت نشد");
                }

                // به‌روزرسانی فیلدها
                if (updateUserRoleDto.IsActive.HasValue)
                {
                    userRole.IsActive = updateUserRoleDto.IsActive.Value;
                }

                // به‌روزرسانی زمان آخرین تغییر
                userRole.UpdatedAt = DateTime.UtcNow;

                var updatedUserRole = await _userRoleRepository.UpdateAsync(userRole);

                // بارگذاری اطلاعات نقش
                await _context.Entry(updatedUserRole).Reference(ur => ur.Role).LoadAsync();

                _logger.LogInformation("UserRole updated successfully with ID: {UserRoleId}", id);

                return ApiResponse<UserRoleResponseDto>.CreateSuccess(
                    MapToUserRoleResponseDto(updatedUserRole),
                    "اطلاعات رابطه کاربر-نقش با موفقیت به‌روزرسانی شد"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user role with ID: {UserRoleId}", id);
                throw;
            }
        }

        public async Task<ApiResponse<bool>> DeleteUserRoleAsync(int id)
        {
            try
            {
                var userRole = await _userRoleRepository.GetByIdAsync(id);

                if (userRole == null)
                {
                    _logger.LogWarning("UserRole not found for delete with ID: {UserRoleId}", id);
                    return ApiResponse<bool>.NotFound("رابطه کاربر-نقش یافت نشد");
                }

                // Soft Delete
                userRole.IsDeleted = true;
                userRole.UpdatedAt = DateTime.UtcNow;
                await _userRoleRepository.UpdateAsync(userRole);

                _logger.LogInformation("UserRole soft deleted successfully with ID: {UserRoleId}", id);

                return ApiResponse<bool>.CreateSuccess(true, "رابطه کاربر-نقش با موفقیت حذف شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user role with ID: {UserRoleId}", id);
                throw;
            }
        }

        public async Task<ApiResponse<bool>> HardDeleteUserRoleAsync(int id)
        {
            try
            {
                var userRole = await _context.UserRoles
                    .FirstOrDefaultAsync(ur => ur.Id == id);

                if (userRole == null)
                {
                    _logger.LogWarning("UserRole not found for hard delete with ID: {UserRoleId}", id);
                    return ApiResponse<bool>.NotFound("رابطه کاربر-نقش یافت نشد");
                }

                await _userRoleRepository.DeleteAsync(userRole);

                _logger.LogWarning("UserRole hard deleted successfully with ID: {UserRoleId}", id);

                return ApiResponse<bool>.CreateSuccess(true, "رابطه کاربر-نقش به طور کامل از دیتابیس حذف شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hard deleting user role with ID: {UserRoleId}", id);
                throw;
            }
        }

        public async Task<ApiResponse<UserRoleResponseDto>> ToggleUserRoleActiveStatusAsync(int id, bool isActive)
        {
            try
            {
                var userRole = await _userRoleRepository.GetByIdAsync(id);

                if (userRole == null)
                {
                    _logger.LogWarning("UserRole not found for toggle active status with ID: {UserRoleId}", id);
                    return ApiResponse<UserRoleResponseDto>.NotFound("رابطه کاربر-نقش یافت نشد");
                }

                userRole.IsActive = isActive;
                userRole.UpdatedAt = DateTime.UtcNow;
                var updatedUserRole = await _userRoleRepository.UpdateAsync(userRole);

                // بارگذاری اطلاعات نقش
                await _context.Entry(updatedUserRole).Reference(ur => ur.Role).LoadAsync();

                var message = isActive ? "رابطه کاربر-نقش با موفقیت فعال شد" : "رابطه کاربر-نقش با موفقیت غیرفعال شد";

                _logger.LogInformation("UserRole active status toggled to {Status} for ID: {UserRoleId}", isActive, id);

                return ApiResponse<UserRoleResponseDto>.CreateSuccess(
                    MapToUserRoleResponseDto(updatedUserRole),
                    message
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling user role active status with ID: {UserRoleId}", id);
                throw;
            }
        }

        /// <summary>
        /// تبدیل UserRole به UserRoleResponseDto
        /// </summary>
        private UserRoleResponseDto MapToUserRoleResponseDto(UserRole userRole)
        {
            var dto = new UserRoleResponseDto
            {
                Id = userRole.Id,
                UserId = userRole.UserId,
                RoleId = userRole.RoleId,
                IsActive = userRole.IsActive,
                IsDeleted = userRole.IsDeleted,
                CreatedAt = userRole.CreatedAt,
                UpdatedAt = userRole.UpdatedAt
            };

            // اگر نقش بارگذاری شده باشد، آن را اضافه می‌کنیم
            if (_context.Entry(userRole).Reference(ur => ur.Role).IsLoaded && userRole.Role != null)
            {
                dto.Role = new RoleResponseDto
                {
                    Id = userRole.Role.Id,
                    Name = userRole.Role.Name,
                    DisplayName = userRole.Role.DisplayName,
                    Description = userRole.Role.Description,
                    IsActive = userRole.Role.IsActive,
                    IsDeleted = userRole.Role.IsDeleted,
                    CreatedAt = userRole.Role.CreatedAt,
                    UpdatedAt = userRole.Role.UpdatedAt
                };
            }

            return dto;
        }
    }
}

