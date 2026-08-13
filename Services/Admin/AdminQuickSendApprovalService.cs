using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api_Vapp.Services.Admin
{
    public class AdminQuickSendApprovalService : IAdminQuickSendApprovalService
    {
        private readonly Api_Context _context;
        private readonly IAuditService _audit;
        private readonly IUserAppNotifier _appNotifier;
        private readonly ILogger<AdminQuickSendApprovalService> _logger;
        private readonly BusinessCardOptions _businessCardOptions;
        private readonly BookingSystemOptions _bookingOptions;
        private readonly FormBuilderOptions _formOptions;
        private readonly LuckyWheelOptions _luckyWheelOptions;

        public AdminQuickSendApprovalService(
            Api_Context context,
            IAuditService audit,
            IUserAppNotifier appNotifier,
            ILogger<AdminQuickSendApprovalService> logger,
            IOptions<BusinessCardOptions> businessCardOptions,
            IOptions<BookingSystemOptions> bookingOptions,
            IOptions<FormBuilderOptions> formOptions,
            IOptions<LuckyWheelOptions> luckyWheelOptions)
        {
            _context = context;
            _audit = audit;
            _appNotifier = appNotifier;
            _logger = logger;
            _businessCardOptions = businessCardOptions.Value;
            _bookingOptions = bookingOptions.Value;
            _formOptions = formOptions.Value;
            _luckyWheelOptions = luckyWheelOptions.Value;
        }

        public Task<ApiResponse<PagedResponse<QuickSendApprovalResponseDto>>> GetPendingAsync(
            string? itemType = null,
            int page = 1,
            int pageSize = 20)
        {
            return GetAllAsync(AdminApprovalStatuses.Pending, itemType, page, pageSize);
        }

        public async Task<ApiResponse<PagedResponse<QuickSendApprovalResponseDto>>> GetAllAsync(
            string? status = null,
            string? itemType = null,
            int page = 1,
            int pageSize = 20)
        {
            try
            {
                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 1, 100);

                string? normalizedType = null;
                if (!string.IsNullOrWhiteSpace(itemType))
                {
                    if (!QuickSendItemTypes.IsValid(itemType))
                    {
                        return ApiResponse<PagedResponse<QuickSendApprovalResponseDto>>.BadRequest(
                            "نوع آیتم ارسال سریع نامعتبر است",
                            errorCode: ErrorCodes.InvalidInput);
                    }

                    normalizedType = QuickSendItemTypes.Normalize(itemType);
                }

                var query = BuildUnifiedQuery(status, normalizedType);
                var totalCount = await query.CountAsync();
                var items = await query
                    .OrderByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                foreach (var item in items)
                    EnrichPublicUrl(item);

                return ApiResponse<PagedResponse<QuickSendApprovalResponseDto>>.CreateSuccess(
                    PagedResponse<QuickSendApprovalResponseDto>.Create(items, totalCount, page, pageSize));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading quick-send approvals");
                return ApiResponse<PagedResponse<QuickSendApprovalResponseDto>>.InternalServerError(
                    ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<QuickSendApprovalResponseDto>> GetByIdAsync(string itemType, int id)
        {
            try
            {
                if (!QuickSendItemTypes.IsValid(itemType))
                {
                    return ApiResponse<QuickSendApprovalResponseDto>.BadRequest(
                        "نوع آیتم ارسال سریع نامعتبر است",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var normalized = QuickSendItemTypes.Normalize(itemType);
                var item = await BuildUnifiedQuery(null, normalized)
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (item == null)
                    return ApiResponse<QuickSendApprovalResponseDto>.NotFound("آیتم ارسال سریع یافت نشد");

                EnrichPublicUrl(item);
                return ApiResponse<QuickSendApprovalResponseDto>.CreateSuccess(item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading quick-send approval {ItemType}/{Id}", itemType, id);
                return ApiResponse<QuickSendApprovalResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<bool>> ApproveAsync(string itemType, int id, int adminUserId)
        {
            try
            {
                if (!QuickSendItemTypes.IsValid(itemType))
                {
                    return ApiResponse<bool>.BadRequest(
                        "نوع آیتم ارسال سریع نامعتبر است",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var normalized = QuickSendItemTypes.Normalize(itemType);
                var entity = await LoadTrackedEntityAsync(normalized, id);
                if (entity == null)
                    return ApiResponse<bool>.NotFound("آیتم ارسال سریع یافت نشد");

                if (entity.Approval.ApprovalStatus != AdminApprovalStatuses.Pending)
                    return ApiResponse<bool>.BadRequest("این آیتم قبلاً بررسی شده است");

                var before = new
                {
                    entity.Id,
                    entity.UserId,
                    entity.Title,
                    approvalStatus = entity.Approval.ApprovalStatus,
                    itemType = normalized
                };

                entity.Approval.ApprovalStatus = AdminApprovalStatuses.Approved;
                entity.Approval.ApprovedAt = DateTime.UtcNow;
                entity.Approval.ApprovedByUserId = adminUserId;
                entity.Approval.RejectionReason = null;
                entity.Approval.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Approval,
                    Action = AuditActions.QuickSendApprovalApproved,
                    EntityType = normalized,
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

                var persian = QuickSendItemTypes.ToPersian(normalized);
                var copy = PushNotificationCopy.QuickSendApproved(persian, before.Title);
                await _appNotifier.NotifyAsync(
                    before.UserId,
                    NotificationCategory.Suggestions,
                    copy.Title,
                    copy.Body,
                    InAppNotificationTypes.QuickSendApproved,
                    relatedEntityId: before.Id,
                    relatedEntityType: normalized,
                    actionUrl: QuickSendItemTypes.ActionUrl(normalized),
                    metadataJson: System.Text.Json.JsonSerializer.Serialize(new
                    {
                        decision = "Approved",
                        itemType = normalized,
                        title = before.Title
                    }));

                return ApiResponse<bool>.CreateSuccess(true, $"{persian} تأیید شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving quick-send {ItemType}/{Id}", itemType, id);
                return ApiResponse<bool>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<bool>> RejectAsync(string itemType, int id, int adminUserId, RejectApprovalDto dto)
        {
            try
            {
                if (!QuickSendItemTypes.IsValid(itemType))
                {
                    return ApiResponse<bool>.BadRequest(
                        "نوع آیتم ارسال سریع نامعتبر است",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var normalized = QuickSendItemTypes.Normalize(itemType);
                var entity = await LoadTrackedEntityAsync(normalized, id);
                if (entity == null)
                    return ApiResponse<bool>.NotFound("آیتم ارسال سریع یافت نشد");

                if (entity.Approval.ApprovalStatus != AdminApprovalStatuses.Pending)
                    return ApiResponse<bool>.BadRequest("این آیتم قبلاً بررسی شده است");

                var reason = dto.Reason.Trim();
                var before = new
                {
                    entity.Id,
                    entity.UserId,
                    entity.Title,
                    approvalStatus = entity.Approval.ApprovalStatus,
                    itemType = normalized
                };

                entity.Approval.ApprovalStatus = AdminApprovalStatuses.Rejected;
                entity.Approval.ApprovedAt = null;
                entity.Approval.ApprovedByUserId = adminUserId;
                entity.Approval.RejectionReason = reason;
                entity.Approval.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Approval,
                    Action = AuditActions.QuickSendApprovalRejected,
                    EntityType = normalized,
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

                var persian = QuickSendItemTypes.ToPersian(normalized);
                var copy = PushNotificationCopy.QuickSendRejected(persian, before.Title, reason);
                await _appNotifier.NotifyAsync(
                    before.UserId,
                    NotificationCategory.Suggestions,
                    copy.Title,
                    copy.Body,
                    InAppNotificationTypes.QuickSendRejected,
                    relatedEntityId: before.Id,
                    relatedEntityType: normalized,
                    actionUrl: QuickSendItemTypes.ActionUrl(normalized),
                    metadataJson: System.Text.Json.JsonSerializer.Serialize(new
                    {
                        decision = "Rejected",
                        itemType = normalized,
                        title = before.Title,
                        rejectionReason = reason
                    }));

                return ApiResponse<bool>.CreateSuccess(true, $"{persian} رد شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting quick-send {ItemType}/{Id}", itemType, id);
                return ApiResponse<bool>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<int> CountPendingAsync()
        {
            var cards = await _context.BusinessCards.AsNoTracking()
                .CountAsync(c => !c.IsDeleted
                    && c.Status == BusinessCardStatus.Published
                    && c.ApprovalStatus == AdminApprovalStatuses.Pending);

            var bookings = await _context.BookingSystems.AsNoTracking()
                .CountAsync(b => !b.IsDeleted
                    && b.Status == BookingSystemStatus.Published
                    && b.ApprovalStatus == AdminApprovalStatuses.Pending);

            var forms = await _context.UserForms.AsNoTracking()
                .CountAsync(f => !f.IsDeleted
                    && f.Status == UserFormStatus.Published
                    && f.ApprovalStatus == AdminApprovalStatuses.Pending);

            var wheels = await _context.LuckyWheels.AsNoTracking()
                .CountAsync(w => !w.IsDeleted
                    && w.Status == LuckyWheelStatus.Published
                    && w.ApprovalStatus == AdminApprovalStatuses.Pending);

            var links = await _context.SocialMediaLinks.AsNoTracking()
                .CountAsync(l => !l.IsDeleted && l.ApprovalStatus == AdminApprovalStatuses.Pending);

            var actions = await _context.QuickActions.AsNoTracking()
                .CountAsync(a => !a.IsDeleted && a.ApprovalStatus == AdminApprovalStatuses.Pending);

            return cards + bookings + forms + wheels + links + actions;
        }

        private IQueryable<QuickSendApprovalResponseDto> BuildUnifiedQuery(string? status, string? itemType)
        {
            IQueryable<QuickSendApprovalResponseDto>? query = null;

            void Append(IQueryable<QuickSendApprovalResponseDto> part)
            {
                query = query == null ? part : query.Concat(part);
            }

            if (itemType == null || itemType == QuickSendItemTypes.BusinessCard)
            {
                Append(
                    from c in _context.BusinessCards.AsNoTracking()
                    join u in _context.Users.AsNoTracking() on c.UserId equals u.Id
                    where !c.IsDeleted && c.Status == BusinessCardStatus.Published
                        && (status == null || c.ApprovalStatus == status)
                    select new QuickSendApprovalResponseDto
                    {
                        ItemType = QuickSendItemTypes.BusinessCard,
                        ItemTypeTitle = "کارت ویزیت",
                        Id = c.Id,
                        UserId = c.UserId,
                        UserPhoneNumber = u.PhoneNumber,
                        UserFullName = u.FullName,
                        Title = c.Title,
                        ContentPreview = c.Slug,
                        PublicUrl = c.Slug,
                        IsActive = c.IsActive,
                        ApprovalStatus = c.ApprovalStatus,
                        RejectionReason = c.RejectionReason,
                        CreatedAt = c.CreatedAt,
                        ApprovedAt = c.ApprovedAt,
                        UpdatedAt = c.UpdatedAt,
                        SkipsMessageApprovalQueue = c.ApprovalStatus == AdminApprovalStatuses.Approved && c.IsActive
                    });
            }

            if (itemType == null || itemType == QuickSendItemTypes.BookingSystem)
            {
                Append(
                    from b in _context.BookingSystems.AsNoTracking()
                    join u in _context.Users.AsNoTracking() on b.UserId equals u.Id
                    where !b.IsDeleted && b.Status == BookingSystemStatus.Published
                        && (status == null || b.ApprovalStatus == status)
                    select new QuickSendApprovalResponseDto
                    {
                        ItemType = QuickSendItemTypes.BookingSystem,
                        ItemTypeTitle = "رزرو نوبت",
                        Id = b.Id,
                        UserId = b.UserId,
                        UserPhoneNumber = u.PhoneNumber,
                        UserFullName = u.FullName,
                        Title = b.Title,
                        ContentPreview = b.Slug,
                        PublicUrl = b.Slug,
                        IsActive = b.IsActive,
                        ApprovalStatus = b.ApprovalStatus,
                        RejectionReason = b.RejectionReason,
                        CreatedAt = b.CreatedAt,
                        ApprovedAt = b.ApprovedAt,
                        UpdatedAt = b.UpdatedAt,
                        SkipsMessageApprovalQueue = b.ApprovalStatus == AdminApprovalStatuses.Approved && b.IsActive
                    });
            }

            if (itemType == null || itemType == QuickSendItemTypes.UserForm)
            {
                Append(
                    from f in _context.UserForms.AsNoTracking()
                    join u in _context.Users.AsNoTracking() on f.UserId equals u.Id
                    where !f.IsDeleted && f.Status == UserFormStatus.Published
                        && (status == null || f.ApprovalStatus == status)
                    select new QuickSendApprovalResponseDto
                    {
                        ItemType = QuickSendItemTypes.UserForm,
                        ItemTypeTitle = "فرم",
                        Id = f.Id,
                        UserId = f.UserId,
                        UserPhoneNumber = u.PhoneNumber,
                        UserFullName = u.FullName,
                        Title = f.Title,
                        ContentPreview = f.Slug,
                        PublicUrl = f.Slug,
                        IsActive = f.IsActive,
                        ApprovalStatus = f.ApprovalStatus,
                        RejectionReason = f.RejectionReason,
                        CreatedAt = f.CreatedAt,
                        ApprovedAt = f.ApprovedAt,
                        UpdatedAt = f.UpdatedAt,
                        SkipsMessageApprovalQueue = f.ApprovalStatus == AdminApprovalStatuses.Approved && f.IsActive
                    });
            }

            if (itemType == null || itemType == QuickSendItemTypes.LuckyWheel)
            {
                Append(
                    from w in _context.LuckyWheels.AsNoTracking()
                    join u in _context.Users.AsNoTracking() on w.UserId equals u.Id
                    where !w.IsDeleted && w.Status == LuckyWheelStatus.Published
                        && (status == null || w.ApprovalStatus == status)
                    select new QuickSendApprovalResponseDto
                    {
                        ItemType = QuickSendItemTypes.LuckyWheel,
                        ItemTypeTitle = "گردونه شانس",
                        Id = w.Id,
                        UserId = w.UserId,
                        UserPhoneNumber = u.PhoneNumber,
                        UserFullName = u.FullName,
                        Title = w.Title,
                        ContentPreview = w.Slug,
                        PublicUrl = w.Slug,
                        IsActive = w.IsActive,
                        ApprovalStatus = w.ApprovalStatus,
                        RejectionReason = w.RejectionReason,
                        CreatedAt = w.CreatedAt,
                        ApprovedAt = w.ApprovedAt,
                        UpdatedAt = w.UpdatedAt,
                        SkipsMessageApprovalQueue = w.ApprovalStatus == AdminApprovalStatuses.Approved && w.IsActive
                    });
            }

            if (itemType == null || itemType == QuickSendItemTypes.SocialMediaLink)
            {
                Append(
                    from l in _context.SocialMediaLinks.AsNoTracking()
                    join u in _context.Users.AsNoTracking() on l.UserId equals u.Id
                    where !l.IsDeleted
                        && (status == null || l.ApprovalStatus == status)
                    select new QuickSendApprovalResponseDto
                    {
                        ItemType = QuickSendItemTypes.SocialMediaLink,
                        ItemTypeTitle = "لینک شبکه اجتماعی",
                        Id = l.Id,
                        UserId = l.UserId,
                        UserPhoneNumber = u.PhoneNumber,
                        UserFullName = u.FullName,
                        Title = l.Platform,
                        ContentPreview = l.LinkUrl,
                        PublicUrl = l.LinkUrl,
                        IsActive = l.IsActive,
                        ApprovalStatus = l.ApprovalStatus,
                        RejectionReason = l.RejectionReason,
                        CreatedAt = l.CreatedAt,
                        ApprovedAt = l.ApprovedAt,
                        UpdatedAt = l.UpdatedAt,
                        SkipsMessageApprovalQueue = l.ApprovalStatus == AdminApprovalStatuses.Approved && l.IsActive
                    });
            }

            if (itemType == null || itemType == QuickSendItemTypes.QuickAction)
            {
                Append(
                    from a in _context.QuickActions.AsNoTracking()
                    join u in _context.Users.AsNoTracking() on a.UserId equals u.Id
                    where !a.IsDeleted
                        && (status == null || a.ApprovalStatus == status)
                    select new QuickSendApprovalResponseDto
                    {
                        ItemType = QuickSendItemTypes.QuickAction,
                        ItemTypeTitle = "اقدام سریع",
                        Id = a.Id,
                        UserId = a.UserId,
                        UserPhoneNumber = u.PhoneNumber,
                        UserFullName = u.FullName,
                        Title = a.Name,
                        ContentPreview = a.Content,
                        PublicUrl = a.Content,
                        IsActive = a.IsActive,
                        ApprovalStatus = a.ApprovalStatus,
                        RejectionReason = a.RejectionReason,
                        CreatedAt = a.CreatedAt,
                        ApprovedAt = a.ApprovedAt,
                        UpdatedAt = a.UpdatedAt,
                        SkipsMessageApprovalQueue = a.ApprovalStatus == AdminApprovalStatuses.Approved && a.IsActive
                    });
            }

            return query ?? Enumerable.Empty<QuickSendApprovalResponseDto>().AsQueryable();
        }

        private void EnrichPublicUrl(QuickSendApprovalResponseDto item)
        {
            static string? JoinBase(string? baseUrl, string? slug)
            {
                if (string.IsNullOrWhiteSpace(slug))
                    return null;
                if (string.IsNullOrWhiteSpace(baseUrl))
                    return slug;
                return $"{baseUrl.TrimEnd('/')}/{slug.TrimStart('/')}";
            }

            item.PublicUrl = item.ItemType switch
            {
                QuickSendItemTypes.BusinessCard => JoinBase(_businessCardOptions.PublicBaseUrl, item.ContentPreview),
                QuickSendItemTypes.BookingSystem => JoinBase(_bookingOptions.PublicBaseUrl, item.ContentPreview),
                QuickSendItemTypes.UserForm => JoinBase(_formOptions.PublicBaseUrl, item.ContentPreview),
                QuickSendItemTypes.LuckyWheel => JoinBase(_luckyWheelOptions.PublicBaseUrl, item.ContentPreview),
                QuickSendItemTypes.SocialMediaLink => item.ContentPreview,
                QuickSendItemTypes.QuickAction => item.ContentPreview,
                _ => item.PublicUrl
            };
        }

        private async Task<TrackedQuickSendEntity?> LoadTrackedEntityAsync(string itemType, int id)
        {
            switch (itemType)
            {
                case QuickSendItemTypes.BusinessCard:
                {
                    var card = await _context.BusinessCards
                        .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted && c.Status == BusinessCardStatus.Published);
                    return card == null ? null : new TrackedQuickSendEntity(card.Id, card.UserId, card.Title, card);
                }
                case QuickSendItemTypes.BookingSystem:
                {
                    var booking = await _context.BookingSystems
                        .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted && b.Status == BookingSystemStatus.Published);
                    return booking == null ? null : new TrackedQuickSendEntity(booking.Id, booking.UserId, booking.Title, booking);
                }
                case QuickSendItemTypes.UserForm:
                {
                    var form = await _context.UserForms
                        .FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted && f.Status == UserFormStatus.Published);
                    return form == null ? null : new TrackedQuickSendEntity(form.Id, form.UserId, form.Title, form);
                }
                case QuickSendItemTypes.LuckyWheel:
                {
                    var wheel = await _context.LuckyWheels
                        .FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted && w.Status == LuckyWheelStatus.Published);
                    return wheel == null ? null : new TrackedQuickSendEntity(wheel.Id, wheel.UserId, wheel.Title, wheel);
                }
                case QuickSendItemTypes.SocialMediaLink:
                {
                    var link = await _context.SocialMediaLinks
                        .FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted);
                    return link == null ? null : new TrackedQuickSendEntity(link.Id, link.UserId, link.Platform, link);
                }
                case QuickSendItemTypes.QuickAction:
                {
                    var action = await _context.QuickActions
                        .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
                    return action == null ? null : new TrackedQuickSendEntity(action.Id, action.UserId, action.Name, action);
                }
                default:
                    return null;
            }
        }

        private sealed class TrackedQuickSendEntity
        {
            public TrackedQuickSendEntity(int id, int userId, string title, IQuickSendApprovable approval)
            {
                Id = id;
                UserId = userId;
                Title = title;
                Approval = approval;
            }

            public int Id { get; }
            public int UserId { get; }
            public string Title { get; }
            public IQuickSendApprovable Approval { get; }
        }
    }
}
