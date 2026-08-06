using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Services.Admin
{
    public class AdminTemplateApprovalService : IAdminTemplateApprovalService
    {
        private readonly Api_Context _context;
        private readonly IAuditService _audit;
        private readonly IUserAppNotifier _appNotifier;
        private readonly ILogger<AdminTemplateApprovalService> _logger;

        public AdminTemplateApprovalService(
            Api_Context context,
            IAuditService audit,
            IUserAppNotifier appNotifier,
            ILogger<AdminTemplateApprovalService> logger)
        {
            _context = context;
            _audit = audit;
            _appNotifier = appNotifier;
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
                var template = await _context.MessageTemplates
                    .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

                if (template == null)
                    return ApiResponse<bool>.NotFound("قالب یافت نشد");

                if (template.ApprovalStatus != AdminApprovalStatuses.Pending)
                    return ApiResponse<bool>.BadRequest("این قالب قبلاً بررسی شده است یا یافت نشد");

                var before = new
                {
                    template.Id,
                    template.UserId,
                    template.ApprovalStatus,
                    template.Name
                };

                template.ApprovalStatus = AdminApprovalStatuses.Approved;
                template.ApprovedAt = DateTime.UtcNow;
                template.ApprovedByUserId = adminUserId;
                template.RejectionReason = null;
                template.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Approval,
                    Action = AuditActions.TemplateApprovalApproved,
                    EntityType = AuditEntityTypes.MessageTemplate,
                    EntityId = id.ToString(),
                    ActorUserId = adminUserId,
                    TargetUserId = before.UserId,
                    Before = before,
                    After = new
                    {
                        approvalStatus = AdminApprovalStatuses.Approved,
                        approvedByUserId = adminUserId
                    }
                });

                var copy = PushNotificationCopy.TemplateApproved(before.Name);
                await _appNotifier.NotifyAsync(
                    before.UserId,
                    NotificationCategory.Suggestions,
                    copy.Title,
                    copy.Body,
                    InAppNotificationTypes.TemplateApproved,
                    relatedEntityId: before.Id,
                    relatedEntityType: AuditEntityTypes.MessageTemplate,
                    actionUrl: "/sms/templates",
                    metadataJson: System.Text.Json.JsonSerializer.Serialize(new
                    {
                        decision = "Approved",
                        templateName = before.Name
                    }));

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
                var template = await _context.MessageTemplates
                    .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

                if (template == null)
                    return ApiResponse<bool>.NotFound("قالب یافت نشد");

                if (template.ApprovalStatus != AdminApprovalStatuses.Pending)
                    return ApiResponse<bool>.BadRequest("این قالب قبلاً بررسی شده است یا یافت نشد");

                var reason = dto.Reason.Trim();
                var before = new
                {
                    template.Id,
                    template.UserId,
                    template.ApprovalStatus,
                    template.Name
                };

                template.ApprovalStatus = AdminApprovalStatuses.Rejected;
                template.ApprovedAt = null;
                template.ApprovedByUserId = adminUserId;
                template.RejectionReason = reason;
                template.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Approval,
                    Action = AuditActions.TemplateApprovalRejected,
                    EntityType = AuditEntityTypes.MessageTemplate,
                    EntityId = id.ToString(),
                    ActorUserId = adminUserId,
                    TargetUserId = before.UserId,
                    Before = before,
                    After = new
                    {
                        approvalStatus = AdminApprovalStatuses.Rejected,
                        approvedByUserId = adminUserId,
                        rejectionReason = reason
                    }
                });

                var copy = PushNotificationCopy.TemplateRejected(before.Name, reason);
                await _appNotifier.NotifyAsync(
                    before.UserId,
                    NotificationCategory.Suggestions,
                    copy.Title,
                    copy.Body,
                    InAppNotificationTypes.TemplateRejected,
                    relatedEntityId: before.Id,
                    relatedEntityType: AuditEntityTypes.MessageTemplate,
                    actionUrl: "/sms/templates",
                    metadataJson: System.Text.Json.JsonSerializer.Serialize(new
                    {
                        decision = "Rejected",
                        templateName = before.Name,
                        rejectionReason = reason
                    }));

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
            IsDefault = template.IsDefault,
            IsActive = template.IsActive,
            ApprovalStatus = template.ApprovalStatus,
            RejectionReason = template.RejectionReason,
            CreatedAt = template.CreatedAt,
            ApprovedAt = template.ApprovedAt,
            UpdatedAt = template.UpdatedAt,
            SkipsMessageApprovalQueue = template.ApprovalStatus == AdminApprovalStatuses.Approved
                && template.IsActive
                && !template.IsDeleted
        };
    }
}
