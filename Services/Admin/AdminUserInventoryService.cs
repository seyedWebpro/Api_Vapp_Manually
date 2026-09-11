using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api_Vapp.Services.Admin
{
    /// <summary>
    /// موجودی قالب و محتوای یک کاربر برای پنل ادمین (فاز ۱)
    /// </summary>
    public class AdminUserInventoryService : IAdminUserInventoryService
    {
        private readonly Api_Context _context;
        private readonly IQuickSendAdminPreviewService _previewService;
        private readonly BusinessCardOptions _businessCardOptions;
        private readonly BookingSystemOptions _bookingOptions;
        private readonly FormBuilderOptions _formOptions;
        private readonly LuckyWheelOptions _luckyWheelOptions;
        private readonly ILogger<AdminUserInventoryService> _logger;

        public AdminUserInventoryService(
            Api_Context context,
            IQuickSendAdminPreviewService previewService,
            IOptions<BusinessCardOptions> businessCardOptions,
            IOptions<BookingSystemOptions> bookingOptions,
            IOptions<FormBuilderOptions> formOptions,
            IOptions<LuckyWheelOptions> luckyWheelOptions,
            ILogger<AdminUserInventoryService> logger)
        {
            _context = context;
            _previewService = previewService;
            _businessCardOptions = businessCardOptions.Value;
            _bookingOptions = bookingOptions.Value;
            _formOptions = formOptions.Value;
            _luckyWheelOptions = luckyWheelOptions.Value;
            _logger = logger;
        }

        public async Task<ApiResponse<AdminUserInventorySummaryDto>> GetSummaryAsync(int userId)
        {
            try
            {
                if (userId <= 0)
                {
                    return ApiResponse<AdminUserInventorySummaryDto>.BadRequest(
                        "شناسه کاربر نامعتبر است",
                        errorCode: ErrorCodes.InvalidUserId);
                }

                var user = await _context.Users.AsNoTracking()
                    .Where(u => u.Id == userId && !u.IsDeleted)
                    .Select(u => new { u.Id, u.FullName, u.PhoneNumber, u.IsActive })
                    .FirstOrDefaultAsync();

                if (user == null)
                    return ApiResponse<AdminUserInventorySummaryDto>.NotFound("کاربر یافت نشد");

                var templatesCount = await _context.MessageTemplates.AsNoTracking()
                    .CountAsync(t => t.UserId == userId && !t.IsDeleted);

                var businessCardsCount = await _context.BusinessCards.AsNoTracking()
                    .CountAsync(c => c.UserId == userId && !c.IsDeleted);
                var userFormsCount = await _context.UserForms.AsNoTracking()
                    .CountAsync(f => f.UserId == userId && !f.IsDeleted);
                var luckyWheelsCount = await _context.LuckyWheels.AsNoTracking()
                    .CountAsync(w => w.UserId == userId && !w.IsDeleted);
                var bookingSystemsCount = await _context.BookingSystems.AsNoTracking()
                    .CountAsync(b => b.UserId == userId && !b.IsDeleted);
                var socialLinksCount = await _context.SocialMediaLinks.AsNoTracking()
                    .CountAsync(l => l.UserId == userId && !l.IsDeleted);
                var quickActionsCount = await _context.QuickActions.AsNoTracking()
                    .CountAsync(a => a.UserId == userId && !a.IsDeleted);
                var bankAccountsCount = await _context.BankAccounts.AsNoTracking()
                    .CountAsync(b => b.UserId == userId && !b.IsDeleted);

                var contentsCount = businessCardsCount + userFormsCount + luckyWheelsCount
                    + bookingSystemsCount + socialLinksCount + quickActionsCount + bankAccountsCount;

                return ApiResponse<AdminUserInventorySummaryDto>.CreateSuccess(new AdminUserInventorySummaryDto
                {
                    UserId = user.Id,
                    FullName = user.FullName,
                    PhoneNumber = user.PhoneNumber,
                    IsActive = user.IsActive,
                    TemplatesCount = templatesCount,
                    ContentsCount = contentsCount,
                    BusinessCardsCount = businessCardsCount,
                    UserFormsCount = userFormsCount,
                    LuckyWheelsCount = luckyWheelsCount,
                    BookingSystemsCount = bookingSystemsCount,
                    SocialMediaLinksCount = socialLinksCount,
                    QuickActionsCount = quickActionsCount,
                    BankAccountsCount = bankAccountsCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user inventory summary for {UserId}", userId);
                return ApiResponse<AdminUserInventorySummaryDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<PagedResponse<AdminUserTemplateItemDto>>> GetTemplatesAsync(
            int userId,
            int page = 1,
            int pageSize = 20)
        {
            try
            {
                if (userId <= 0)
                {
                    return ApiResponse<PagedResponse<AdminUserTemplateItemDto>>.BadRequest(
                        "شناسه کاربر نامعتبر است",
                        errorCode: ErrorCodes.InvalidUserId);
                }

                var userExists = await _context.Users.AsNoTracking()
                    .AnyAsync(u => u.Id == userId && !u.IsDeleted);
                if (!userExists)
                    return ApiResponse<PagedResponse<AdminUserTemplateItemDto>>.NotFound("کاربر یافت نشد");

                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 1, 100);

                var query = _context.MessageTemplates.AsNoTracking()
                    .Where(t => t.UserId == userId && !t.IsDeleted);

                var totalCount = await query.CountAsync();
                var items = await query
                    .OrderByDescending(t => t.CreatedAt)
                    .ThenByDescending(t => t.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(t => new AdminUserTemplateItemDto
                    {
                        Id = t.Id,
                        Name = t.Name,
                        Category = t.Category,
                        IsActive = t.IsActive,
                        IsDefault = t.IsDefault,
                        ApprovalStatus = t.ApprovalStatus,
                        CreatedAt = t.CreatedAt,
                        UpdatedAt = t.UpdatedAt
                    })
                    .ToListAsync();

                var adminViewPath = $"/admin/template-approvals?userSearch={userId}";
                foreach (var item in items)
                    item.AdminViewPath = adminViewPath;

                return ApiResponse<PagedResponse<AdminUserTemplateItemDto>>.CreateSuccess(
                    PagedResponse<AdminUserTemplateItemDto>.Create(items, totalCount, page, pageSize));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading templates for user inventory {UserId}", userId);
                return ApiResponse<PagedResponse<AdminUserTemplateItemDto>>.InternalServerError(
                    ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<PagedResponse<AdminUserContentItemDto>>> GetContentsAsync(
            int userId,
            string? itemType = null,
            int page = 1,
            int pageSize = 20)
        {
            try
            {
                if (userId <= 0)
                {
                    return ApiResponse<PagedResponse<AdminUserContentItemDto>>.BadRequest(
                        "شناسه کاربر نامعتبر است",
                        errorCode: ErrorCodes.InvalidUserId);
                }

                var userExists = await _context.Users.AsNoTracking()
                    .AnyAsync(u => u.Id == userId && !u.IsDeleted);
                if (!userExists)
                    return ApiResponse<PagedResponse<AdminUserContentItemDto>>.NotFound("کاربر یافت نشد");

                string? normalizedType = null;
                if (!string.IsNullOrWhiteSpace(itemType))
                {
                    if (!QuickSendItemTypes.IsValid(itemType))
                    {
                        return ApiResponse<PagedResponse<AdminUserContentItemDto>>.BadRequest(
                            "نوع آیتم ارسال سریع نامعتبر است",
                            errorCode: ErrorCodes.InvalidInput);
                    }

                    normalizedType = QuickSendItemTypes.Normalize(itemType);
                }

                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 1, 100);

                var all = await LoadContentItemsAsync(userId, normalizedType);
                var totalCount = all.Count;
                var pageItems = all
                    .OrderByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                foreach (var item in pageItems)
                    FinalizeContentItem(item);

                return ApiResponse<PagedResponse<AdminUserContentItemDto>>.CreateSuccess(
                    PagedResponse<AdminUserContentItemDto>.Create(pageItems, totalCount, page, pageSize));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading contents for user inventory {UserId}", userId);
                return ApiResponse<PagedResponse<AdminUserContentItemDto>>.InternalServerError(
                    ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<AdminUserContentViewLinkDto>> CreateContentViewLinkAsync(
            int userId,
            string itemType,
            int id,
            int adminUserId)
        {
            try
            {
                if (userId <= 0)
                {
                    return ApiResponse<AdminUserContentViewLinkDto>.BadRequest(
                        "شناسه کاربر نامعتبر است",
                        errorCode: ErrorCodes.InvalidUserId);
                }

                if (!QuickSendItemTypes.IsValid(itemType))
                {
                    return ApiResponse<AdminUserContentViewLinkDto>.BadRequest(
                        "نوع آیتم ارسال سریع نامعتبر است",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var normalized = QuickSendItemTypes.Normalize(itemType);
                var item = (await LoadContentItemsAsync(userId, normalized))
                    .FirstOrDefault(x => x.Id == id);

                if (item == null)
                    return ApiResponse<AdminUserContentViewLinkDto>.NotFound("محتوا یافت نشد");

                FinalizeContentItem(item);

                if (!item.CanView || string.IsNullOrWhiteSpace(item.ViewMode))
                {
                    return ApiResponse<AdminUserContentViewLinkDto>.BadRequest(
                        "برای این محتوا لینک مشاهده در دسترس نیست",
                        errorCode: ErrorCodes.InvalidInput);
                }

                if (item.ViewMode == AdminContentViewModes.Public)
                {
                    return ApiResponse<AdminUserContentViewLinkDto>.CreateSuccess(new AdminUserContentViewLinkDto
                    {
                        ItemType = normalized,
                        Id = id,
                        ViewMode = AdminContentViewModes.Public,
                        Url = item.PublicUrl
                    });
                }

                if (item.ViewMode == AdminContentViewModes.External)
                {
                    return ApiResponse<AdminUserContentViewLinkDto>.CreateSuccess(new AdminUserContentViewLinkDto
                    {
                        ItemType = normalized,
                        Id = id,
                        ViewMode = AdminContentViewModes.External,
                        Url = item.PublicUrl
                    });
                }

                if (item.ViewMode == AdminContentViewModes.AdminPage)
                {
                    return ApiResponse<AdminUserContentViewLinkDto>.CreateSuccess(new AdminUserContentViewLinkDto
                    {
                        ItemType = normalized,
                        Id = id,
                        ViewMode = AdminContentViewModes.AdminPage,
                        AdminPath = $"/admin/quick-send-approvals?itemType={normalized}"
                    });
                }

                // AdminPreview — تأییدنشده / غیرفعال / هر حالتی که لینک عمومی کار نکند
                var tokenResult = await _previewService.CreatePreviewTokenAsync(normalized, id, adminUserId);
                if (!tokenResult.Success || tokenResult.Data == null)
                {
                    return ApiResponse<AdminUserContentViewLinkDto>.Error(
                        tokenResult.Message,
                        tokenResult.StatusCode,
                        tokenResult.Errors,
                        tokenResult.ErrorCode);
                }

                return ApiResponse<AdminUserContentViewLinkDto>.CreateSuccess(new AdminUserContentViewLinkDto
                {
                    ItemType = normalized,
                    Id = id,
                    ViewMode = AdminContentViewModes.AdminPreview,
                    PreviewPath = tokenResult.Data.PreviewPath,
                    Url = item.PublicUrl,
                    ExpiresAt = tokenResult.Data.ExpiresAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error creating content view link for user {UserId} {ItemType}/{Id}",
                    userId,
                    itemType,
                    id);
                return ApiResponse<AdminUserContentViewLinkDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private async Task<List<AdminUserContentItemDto>> LoadContentItemsAsync(int userId, string? itemType)
        {
            var items = new List<AdminUserContentItemDto>();

            if (itemType == null || itemType == QuickSendItemTypes.BusinessCard)
            {
                var cards = await _context.BusinessCards.AsNoTracking()
                    .Where(c => c.UserId == userId && !c.IsDeleted)
                    .Select(c => new AdminUserContentItemDto
                    {
                        ItemType = QuickSendItemTypes.BusinessCard,
                        ItemTypeTitle = "کارت ویزیت",
                        Id = c.Id,
                        Title = c.Title,
                        PublishStatus = c.Status == BusinessCardStatus.Published ? "Published" : "Draft",
                        IsActive = c.IsActive,
                        ApprovalStatus = c.ApprovalStatus,
                        RejectionReason = c.RejectionReason,
                        PublicUrl = c.Slug,
                        CreatedAt = c.CreatedAt,
                        UpdatedAt = c.UpdatedAt
                    })
                    .ToListAsync();
                items.AddRange(cards);
            }

            if (itemType == null || itemType == QuickSendItemTypes.UserForm)
            {
                var forms = await _context.UserForms.AsNoTracking()
                    .Where(f => f.UserId == userId && !f.IsDeleted)
                    .Select(f => new AdminUserContentItemDto
                    {
                        ItemType = QuickSendItemTypes.UserForm,
                        ItemTypeTitle = "فرم",
                        Id = f.Id,
                        Title = f.Title,
                        PublishStatus = f.Status == UserFormStatus.Published ? "Published" : "Draft",
                        IsActive = f.IsActive,
                        ApprovalStatus = f.ApprovalStatus,
                        RejectionReason = f.RejectionReason,
                        PublicUrl = f.Slug,
                        CreatedAt = f.CreatedAt,
                        UpdatedAt = f.UpdatedAt
                    })
                    .ToListAsync();
                items.AddRange(forms);
            }

            if (itemType == null || itemType == QuickSendItemTypes.LuckyWheel)
            {
                var wheels = await _context.LuckyWheels.AsNoTracking()
                    .Where(w => w.UserId == userId && !w.IsDeleted)
                    .Select(w => new AdminUserContentItemDto
                    {
                        ItemType = QuickSendItemTypes.LuckyWheel,
                        ItemTypeTitle = "گردونه شانس",
                        Id = w.Id,
                        Title = w.Title,
                        PublishStatus = w.Status == LuckyWheelStatus.Published ? "Published" : "Draft",
                        IsActive = w.IsActive,
                        ApprovalStatus = w.ApprovalStatus,
                        RejectionReason = w.RejectionReason,
                        PublicUrl = w.Slug,
                        CreatedAt = w.CreatedAt,
                        UpdatedAt = w.UpdatedAt
                    })
                    .ToListAsync();
                items.AddRange(wheels);
            }

            if (itemType == null || itemType == QuickSendItemTypes.BookingSystem)
            {
                var bookings = await _context.BookingSystems.AsNoTracking()
                    .Where(b => b.UserId == userId && !b.IsDeleted)
                    .Select(b => new AdminUserContentItemDto
                    {
                        ItemType = QuickSendItemTypes.BookingSystem,
                        ItemTypeTitle = "رزرو نوبت",
                        Id = b.Id,
                        Title = b.Title,
                        PublishStatus = b.Status == BookingSystemStatus.Published ? "Published" : "Draft",
                        IsActive = b.IsActive,
                        ApprovalStatus = b.ApprovalStatus,
                        RejectionReason = b.RejectionReason,
                        PublicUrl = b.Slug,
                        CreatedAt = b.CreatedAt,
                        UpdatedAt = b.UpdatedAt
                    })
                    .ToListAsync();
                items.AddRange(bookings);
            }

            if (itemType == null || itemType == QuickSendItemTypes.SocialMediaLink)
            {
                var links = await _context.SocialMediaLinks.AsNoTracking()
                    .Where(l => l.UserId == userId && !l.IsDeleted)
                    .Select(l => new AdminUserContentItemDto
                    {
                        ItemType = QuickSendItemTypes.SocialMediaLink,
                        ItemTypeTitle = "لینک شبکه اجتماعی",
                        Id = l.Id,
                        Title = string.IsNullOrWhiteSpace(l.Platform) ? "لینک" : l.Platform,
                        PublishStatus = null,
                        IsActive = l.IsActive,
                        ApprovalStatus = l.ApprovalStatus,
                        RejectionReason = l.RejectionReason,
                        PublicUrl = l.LinkUrl,
                        CreatedAt = l.CreatedAt,
                        UpdatedAt = l.UpdatedAt
                    })
                    .ToListAsync();
                items.AddRange(links);
            }

            if (itemType == null || itemType == QuickSendItemTypes.QuickAction)
            {
                var actions = await _context.QuickActions.AsNoTracking()
                    .Where(a => a.UserId == userId && !a.IsDeleted)
                    .Select(a => new AdminUserContentItemDto
                    {
                        ItemType = QuickSendItemTypes.QuickAction,
                        ItemTypeTitle = "اقدام سریع",
                        Id = a.Id,
                        Title = a.Name,
                        PublishStatus = null,
                        IsActive = a.IsActive,
                        ApprovalStatus = a.ApprovalStatus,
                        RejectionReason = a.RejectionReason,
                        PublicUrl = null,
                        CreatedAt = a.CreatedAt,
                        UpdatedAt = a.UpdatedAt
                    })
                    .ToListAsync();
                items.AddRange(actions);
            }

            if (itemType == null || itemType == QuickSendItemTypes.BankAccount)
            {
                var accounts = await _context.BankAccounts.AsNoTracking()
                    .Where(b => b.UserId == userId && !b.IsDeleted)
                    .Select(b => new AdminUserContentItemDto
                    {
                        ItemType = QuickSendItemTypes.BankAccount,
                        ItemTypeTitle = "شماره حساب",
                        Id = b.Id,
                        Title = b.Title,
                        PublishStatus = null,
                        IsActive = b.IsActive,
                        ApprovalStatus = b.ApprovalStatus,
                        RejectionReason = b.RejectionReason,
                        PublicUrl = null,
                        CreatedAt = b.CreatedAt,
                        UpdatedAt = b.UpdatedAt
                    })
                    .ToListAsync();
                items.AddRange(accounts);
            }

            return items;
        }

        private void FinalizeContentItem(AdminUserContentItemDto item)
        {
            if (IsVisualLinkType(item.ItemType))
            {
                var slug = item.PublicUrl;
                item.PublicUrl = JoinBase(ResolvePublicBase(item.ItemType), slug);

                var isPublished = string.Equals(item.PublishStatus, "Published", StringComparison.Ordinal);
                if (!isPublished)
                {
                    item.CanView = false;
                    item.ViewMode = null;
                    return;
                }

                item.CanView = true;
                // تأییدشده و فعال → همان لینک عمومی کاربر؛ وگرنه پیش‌نمایش ادمین (غیرفعال / در انتظار / رد)
                if (item.ApprovalStatus == AdminApprovalStatuses.Approved
                    && item.IsActive
                    && !string.IsNullOrWhiteSpace(item.PublicUrl))
                {
                    item.ViewMode = AdminContentViewModes.Public;
                }
                else
                {
                    item.ViewMode = AdminContentViewModes.AdminPreview;
                }

                return;
            }

            if (item.ItemType == QuickSendItemTypes.SocialMediaLink)
            {
                item.CanView = !string.IsNullOrWhiteSpace(item.PublicUrl);
                item.ViewMode = item.CanView ? AdminContentViewModes.External : null;
                return;
            }

            // شماره حساب / اقدام سریع — بدون سطح عمومی؛ ارجاع به صف تأیید
            item.CanView = true;
            item.ViewMode = AdminContentViewModes.AdminPage;
        }

        private static bool IsVisualLinkType(string itemType) =>
            itemType is QuickSendItemTypes.BusinessCard
                or QuickSendItemTypes.UserForm
                or QuickSendItemTypes.LuckyWheel
                or QuickSendItemTypes.BookingSystem;

        private string? ResolvePublicBase(string itemType) => itemType switch
        {
            QuickSendItemTypes.BusinessCard => _businessCardOptions.PublicBaseUrl,
            QuickSendItemTypes.BookingSystem => _bookingOptions.PublicBaseUrl,
            QuickSendItemTypes.UserForm => _formOptions.PublicBaseUrl,
            QuickSendItemTypes.LuckyWheel => _luckyWheelOptions.PublicBaseUrl,
            _ => null
        };

        private static string? JoinBase(string? baseUrl, string? slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return null;
            if (string.IsNullOrWhiteSpace(baseUrl))
                return slug;
            return $"{baseUrl.TrimEnd('/')}/{slug.TrimStart('/')}";
        }
    }
}
