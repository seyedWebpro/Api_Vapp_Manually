using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Services.Admin
{
    public class AdminTemplateApprovalService : IAdminTemplateApprovalService
    {
        private readonly Api_Context _context;
        private readonly ILogger<AdminTemplateApprovalService> _logger;

        public AdminTemplateApprovalService(Api_Context context, ILogger<AdminTemplateApprovalService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public Task<ApiResponse<PagedResponse<TemplateApprovalResponseDto>>> GetPendingAsync(int page = 1, int pageSize = 20)
        {
            return GetAllAsync(AdminApprovalStatuses.Pending, page, pageSize);
        }

        public async Task<ApiResponse<PagedResponse<TemplateApprovalResponseDto>>> GetAllAsync(string? status = null, int page = 1, int pageSize = 20)
        {
            try
            {
                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 1, 100);

                var query = _context.MessageTemplates.AsNoTracking()
                    .Include(t => t.User)
                    .Where(t => !t.IsDeleted);

                if (!string.IsNullOrWhiteSpace(status))
                    query = query.Where(t => t.ApprovalStatus == status);

                var totalCount = await query.CountAsync();
                var templates = await query
                    .OrderByDescending(t => t.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return ApiResponse<PagedResponse<TemplateApprovalResponseDto>>.CreateSuccess(
                    PagedResponse<TemplateApprovalResponseDto>.Create(
                        templates.Select(Map).ToList(), totalCount, page, pageSize));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading template approvals");
                return ApiResponse<PagedResponse<TemplateApprovalResponseDto>>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<TemplateApprovalResponseDto>> GetByIdAsync(int id)
        {
            try
            {
                var template = await _context.MessageTemplates.AsNoTracking()
                    .Include(t => t.User)
                    .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

                if (template == null)
                    return ApiResponse<TemplateApprovalResponseDto>.NotFound("قالب یافت نشد");

                return ApiResponse<TemplateApprovalResponseDto>.CreateSuccess(Map(template));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading template approval {TemplateId}", id);
                return ApiResponse<TemplateApprovalResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<bool>> ApproveAsync(int id, int adminUserId)
        {
            try
            {
                var updated = await _context.MessageTemplates
                    .Where(t => t.Id == id && t.ApprovalStatus == AdminApprovalStatuses.Pending && !t.IsDeleted)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(t => t.ApprovalStatus, AdminApprovalStatuses.Approved)
                        .SetProperty(t => t.ApprovedAt, DateTime.UtcNow)
                        .SetProperty(t => t.ApprovedByUserId, adminUserId)
                        .SetProperty(t => t.RejectionReason, (string?)null)
                        .SetProperty(t => t.UpdatedAt, DateTime.UtcNow));

                if (updated == 0)
                    return ApiResponse<bool>.BadRequest("این قالب قبلاً بررسی شده است یا یافت نشد");

                return ApiResponse<bool>.CreateSuccess(true, "قالب تأیید شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving template {TemplateId}", id);
                return ApiResponse<bool>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<bool>> RejectAsync(int id, int adminUserId, RejectApprovalDto dto)
        {
            try
            {
                var updated = await _context.MessageTemplates
                    .Where(t => t.Id == id && t.ApprovalStatus == AdminApprovalStatuses.Pending && !t.IsDeleted)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(t => t.ApprovalStatus, AdminApprovalStatuses.Rejected)
                        .SetProperty(t => t.ApprovedAt, (DateTime?)null)
                        .SetProperty(t => t.ApprovedByUserId, adminUserId)
                        .SetProperty(t => t.RejectionReason, dto.Reason.Trim())
                        .SetProperty(t => t.UpdatedAt, DateTime.UtcNow));

                if (updated == 0)
                    return ApiResponse<bool>.BadRequest("این قالب قبلاً بررسی شده است یا یافت نشد");

                return ApiResponse<bool>.CreateSuccess(true, "قالب رد شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting template {TemplateId}", id);
                return ApiResponse<bool>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private static TemplateApprovalResponseDto Map(MessageTemplate template) => new()
        {
            Id = template.Id,
            UserId = template.UserId,
            UserPhoneNumber = template.User?.PhoneNumber,
            UserFullName = template.User?.FullName,
            Name = template.Name,
            Content = template.Content,
            Category = template.Category,
            ApprovalStatus = template.ApprovalStatus,
            RejectionReason = template.RejectionReason,
            CreatedAt = template.CreatedAt,
            ApprovedAt = template.ApprovedAt
        };
    }
}
