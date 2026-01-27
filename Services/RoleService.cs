using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Role;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services
{
    /// <summary>
    /// پیاده‌سازی سرویس مدیریت نقش‌ها
    /// </summary>
    public class RoleService : IRoleService
    {
        private readonly IRoleRepository _roleRepository;
        private readonly Api_Vapp.Data.Api_Context _context;
        private readonly ILogger<RoleService> _logger;

        public RoleService(IRoleRepository roleRepository, Api_Vapp.Data.Api_Context context, ILogger<RoleService> logger)
        {
            _roleRepository = roleRepository;
            _context = context;
            _logger = logger;
        }

        public async Task<ApiResponse<RoleResponseDto>> CreateRoleAsync(CreateRoleDto createRoleDto)
        {
            try
            {
                // بررسی وجود نقش با نام
                var existingRole = await _roleRepository.GetByNameAsync(createRoleDto.Name);
                if (existingRole != null && !existingRole.IsDeleted)
                {
                    _logger.LogWarning("Attempt to create role with existing name: {RoleName}", createRoleDto.Name);
                    return ApiResponse<RoleResponseDto>.BadRequest("نقشی با این نام قبلاً ثبت شده است");
                }

                // ایجاد نقش جدید
                var role = new Role
                {
                    Name = createRoleDto.Name,
                    DisplayName = createRoleDto.DisplayName,
                    Description = createRoleDto.Description,
                    IsActive = createRoleDto.IsActive,
                    CreatedAt = DateTime.UtcNow
                };

                var createdRole = await _roleRepository.AddAsync(role);

                _logger.LogInformation("Role created successfully with ID: {RoleId}", createdRole.Id);

                return ApiResponse<RoleResponseDto>.CreateSuccess(
                    MapToRoleResponseDto(createdRole),
                    "نقش با موفقیت ایجاد شد",
                    201
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating role with name: {RoleName}", createRoleDto.Name);
                throw;
            }
        }

        public async Task<ApiResponse<RoleResponseDto>> GetRoleByIdAsync(int id)
        {
            try
            {
                var role = await _roleRepository.GetByIdAsync(id);

                if (role == null)
                {
                    _logger.LogWarning("Role not found with ID: {RoleId}", id);
                    return ApiResponse<RoleResponseDto>.NotFound("نقش یافت نشد");
                }

                return ApiResponse<RoleResponseDto>.CreateSuccess(MapToRoleResponseDto(role));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting role with ID: {RoleId}", id);
                throw;
            }
        }

        public async Task<ApiResponse<RoleListResponseDto>> GetRolesAsync(int pageNumber = 1, int pageSize = 10, bool? isActive = null, bool? isDeleted = null)
        {
            try
            {
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                var query = _context.Roles.AsQueryable();

                // اعمال فیلترها
                if (isActive.HasValue)
                {
                    query = query.Where(r => r.IsActive == isActive.Value);
                }

                if (isDeleted.HasValue)
                {
                    query = query.Where(r => r.IsDeleted == isDeleted.Value);
                }
                else
                {
                    query = query.Where(r => !r.IsDeleted);
                }

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var roles = await query
                    .OrderBy(r => r.Name)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var roleListResponse = new RoleListResponseDto
                {
                    Roles = roles.Select(MapToRoleResponseDto).ToList(),
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                return ApiResponse<RoleListResponseDto>.CreateSuccess(roleListResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting roles list");
                throw;
            }
        }

        public async Task<ApiResponse<List<RoleResponseDto>>> GetActiveRolesAsync()
        {
            try
            {
                var roles = await _roleRepository.GetActiveRolesAsync();
                return ApiResponse<List<RoleResponseDto>>.CreateSuccess(
                    roles.Select(MapToRoleResponseDto).ToList()
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active roles");
                throw;
            }
        }

        public async Task<ApiResponse<RoleResponseDto>> UpdateRoleAsync(int id, UpdateRoleDto updateRoleDto)
        {
            try
            {
                var role = await _roleRepository.GetByIdAsync(id);

                if (role == null)
                {
                    _logger.LogWarning("Role not found for update with ID: {RoleId}", id);
                    return ApiResponse<RoleResponseDto>.NotFound("نقش یافت نشد");
                }

                // به‌روزرسانی فیلدها - فقط اگر مقدار داده شده باشد
                if (!string.IsNullOrWhiteSpace(updateRoleDto.Name))
                {
                    // بررسی تکراری نبودن نام نقش
                    var existingRole = await _roleRepository.GetByNameAsync(updateRoleDto.Name);
                    if (existingRole != null && existingRole.Id != id && !existingRole.IsDeleted)
                    {
                        return ApiResponse<RoleResponseDto>.BadRequest("نقشی با این نام قبلاً ثبت شده است");
                    }
                    role.Name = updateRoleDto.Name;
                }

                if (!string.IsNullOrWhiteSpace(updateRoleDto.DisplayName))
                {
                    role.DisplayName = updateRoleDto.DisplayName;
                }

                if (updateRoleDto.Description != null)
                {
                    role.Description = updateRoleDto.Description;
                }

                if (updateRoleDto.IsActive.HasValue)
                {
                    role.IsActive = updateRoleDto.IsActive.Value;
                }

                // به‌روزرسانی زمان آخرین تغییر
                role.UpdatedAt = DateTime.UtcNow;

                var updatedRole = await _roleRepository.UpdateAsync(role);

                _logger.LogInformation("Role updated successfully with ID: {RoleId}", id);

                return ApiResponse<RoleResponseDto>.CreateSuccess(
                    MapToRoleResponseDto(updatedRole),
                    "اطلاعات نقش با موفقیت به‌روزرسانی شد"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating role with ID: {RoleId}", id);
                throw;
            }
        }

        public async Task<ApiResponse<bool>> DeleteRoleAsync(int id)
        {
            try
            {
                var role = await _roleRepository.GetByIdAsync(id);

                if (role == null)
                {
                    _logger.LogWarning("Role not found for delete with ID: {RoleId}", id);
                    return ApiResponse<bool>.NotFound("نقش یافت نشد");
                }

                // بررسی وجود UserRole های فعال برای این نقش
                var activeUserRoles = await _context.UserRoles
                    .Where(ur => ur.RoleId == id && ur.IsActive && !ur.IsDeleted)
                    .AnyAsync();

                if (activeUserRoles)
                {
                    return ApiResponse<bool>.BadRequest("نمی‌توان نقش را حذف کرد زیرا کاربرانی با این نقش فعال هستند");
                }

                // Soft Delete
                role.IsDeleted = true;
                role.UpdatedAt = DateTime.UtcNow;
                await _roleRepository.UpdateAsync(role);

                _logger.LogInformation("Role soft deleted successfully with ID: {RoleId}", id);

                return ApiResponse<bool>.CreateSuccess(true, "نقش با موفقیت حذف شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting role with ID: {RoleId}", id);
                throw;
            }
        }

        public async Task<ApiResponse<bool>> HardDeleteRoleAsync(int id)
        {
            try
            {
                var role = await _context.Roles
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (role == null)
                {
                    _logger.LogWarning("Role not found for hard delete with ID: {RoleId}", id);
                    return ApiResponse<bool>.NotFound("نقش یافت نشد");
                }

                // بررسی وجود UserRole های فعال برای این نقش
                var activeUserRoles = await _context.UserRoles
                    .Where(ur => ur.RoleId == id && ur.IsActive && !ur.IsDeleted)
                    .AnyAsync();

                if (activeUserRoles)
                {
                    return ApiResponse<bool>.BadRequest("نمی‌توان نقش را حذف کرد زیرا کاربرانی با این نقش فعال هستند");
                }

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // حذف UserRole های مربوط به این نقش
                    var userRoles = await _context.UserRoles
                        .Where(ur => ur.RoleId == id)
                        .ToListAsync();

                    if (userRoles.Any())
                    {
                        _context.UserRoles.RemoveRange(userRoles);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Deleted {Count} user roles before hard delete of role {RoleId}", 
                            userRoles.Count, id);
                    }

                    // Hard Delete
                    await _roleRepository.DeleteAsync(role);

                    await transaction.CommitAsync();

                    _logger.LogWarning("Role hard deleted successfully with ID: {RoleId}. UserRoles: {UserRoleCount}", 
                        id, userRoles.Count);

                    return ApiResponse<bool>.CreateSuccess(true, 
                        $"نقش به طور کامل از دیتابیس حذف شد. {userRoles.Count} رابطه کاربر-نقش حذف شد.");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error during hard delete transaction for role {RoleId}. Transaction rolled back.", id);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hard deleting role with ID: {RoleId}", id);
                throw;
            }
        }

        public async Task<ApiResponse<RoleResponseDto>> ToggleRoleActiveStatusAsync(int id, bool isActive)
        {
            try
            {
                var role = await _roleRepository.GetByIdAsync(id);

                if (role == null)
                {
                    _logger.LogWarning("Role not found for toggle active status with ID: {RoleId}", id);
                    return ApiResponse<RoleResponseDto>.NotFound("نقش یافت نشد");
                }

                role.IsActive = isActive;
                role.UpdatedAt = DateTime.UtcNow;
                var updatedRole = await _roleRepository.UpdateAsync(role);

                var message = isActive ? "نقش با موفقیت فعال شد" : "نقش با موفقیت غیرفعال شد";

                _logger.LogInformation("Role active status toggled to {Status} for ID: {RoleId}", isActive, id);

                return ApiResponse<RoleResponseDto>.CreateSuccess(
                    MapToRoleResponseDto(updatedRole),
                    message
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling role active status with ID: {RoleId}", id);
                throw;
            }
        }

        /// <summary>
        /// تبدیل Role به RoleResponseDto
        /// </summary>
        private RoleResponseDto MapToRoleResponseDto(Role role)
        {
            return new RoleResponseDto
            {
                Id = role.Id,
                Name = role.Name,
                DisplayName = role.DisplayName,
                Description = role.Description,
                IsActive = role.IsActive,
                IsDeleted = role.IsDeleted,
                CreatedAt = role.CreatedAt,
                UpdatedAt = role.UpdatedAt
            };
        }
    }
}

