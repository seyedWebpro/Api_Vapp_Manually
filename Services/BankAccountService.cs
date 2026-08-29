using System.Text;
using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.BankAccount;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Message;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services
{
    /// <summary>
    /// سرویس مدیریت شماره حساب برای ارسال سریع
    /// </summary>
    public class BankAccountService : IBankAccountService
    {
        private static readonly TimeSpan ListCacheDuration = TimeSpan.FromMinutes(5);

        private readonly IBankAccountRepository _bankAccountRepository;
        private readonly IContactRepository _contactRepository;
        private readonly IContactNotebookRepository _notebookRepository;
        private readonly IMessageService _messageService;
        private readonly Api_Context _context;
        private readonly IAuditService _audit;
        private readonly IMemoryCache _cache;
        private readonly ILogger<BankAccountService> _logger;

        public BankAccountService(
            IBankAccountRepository bankAccountRepository,
            IContactRepository contactRepository,
            IContactNotebookRepository notebookRepository,
            IMessageService messageService,
            Api_Context context,
            IAuditService audit,
            IMemoryCache cache,
            ILogger<BankAccountService> logger)
        {
            _bankAccountRepository = bankAccountRepository;
            _contactRepository = contactRepository;
            _notebookRepository = notebookRepository;
            _messageService = messageService;
            _context = context;
            _audit = audit;
            _cache = cache;
            _logger = logger;
        }

        public async Task<ApiResponse<BankAccountResponseDto>> CreateBankAccountAsync(
            int userId,
            CreateBankAccountDto createDto)
        {
            _logger.LogInformation("شروع ایجاد شماره حساب — UserId: {UserId}", userId);

            try
            {
                var title = NormalizeTitle(createDto.Title);
                if (string.IsNullOrWhiteSpace(title))
                {
                    return ApiResponse<BankAccountResponseDto>.BadRequest(
                        "عنوان الزامی است",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var (accountNumber, cardNumber, shebaNumber, fieldError) = NormalizeBankFields(
                    createDto.AccountNumber,
                    createDto.CardNumber,
                    createDto.ShebaNumber);

                if (fieldError != null)
                {
                    return ApiResponse<BankAccountResponseDto>.BadRequest(
                        fieldError,
                        errorCode: ErrorCodes.InvalidInput);
                }

                if (string.IsNullOrWhiteSpace(accountNumber)
                    && string.IsNullOrWhiteSpace(cardNumber)
                    && string.IsNullOrWhiteSpace(shebaNumber))
                {
                    return ApiResponse<BankAccountResponseDto>.BadRequest(
                        "حداقل یکی از شماره حساب، کارت یا شبا الزامی است",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var userExists = await _context.Users.AsNoTracking()
                    .AnyAsync(u => u.Id == userId && !u.IsDeleted);
                if (!userExists)
                    return ApiResponse<BankAccountResponseDto>.NotFound("کاربر یافت نشد");

                var activeCount = await _bankAccountRepository.CountActiveByUserIdAsync(userId);
                var setAsDefault = createDto.IsDefault == true || activeCount == 0;

                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    if (setAsDefault)
                        await UnsetDefaultsAsync(userId);

                    var entity = new BankAccount
                    {
                        UserId = userId,
                        Title = title,
                        SmsCaption = QuickSendLinkSmsHelper.NormalizeCaption(createDto.SmsDescription),
                        AccountNumber = accountNumber,
                        CardNumber = cardNumber,
                        ShebaNumber = shebaNumber,
                        IsDefault = setAsDefault,
                        IsActive = true,
                        IsDeleted = false,
                        CreatedAt = DateTime.UtcNow,
                        ApprovalStatus = AdminApprovalStatuses.Pending
                    };

                    await _bankAccountRepository.AddAsync(entity);
                    await transaction.CommitAsync();

                    InvalidateUserCache(userId);

                    await _audit.WriteAsync(new AuditEntry
                    {
                        Category = AuditCategories.Message,
                        Action = AuditActions.BankAccountCreated,
                        EntityType = AuditEntityTypes.BankAccount,
                        EntityId = entity.Id.ToString(),
                        ActorUserId = userId,
                        After = new
                        {
                            entity.Title,
                            HasAccount = !string.IsNullOrEmpty(entity.AccountNumber),
                            HasCard = !string.IsNullOrEmpty(entity.CardNumber),
                            HasSheba = !string.IsNullOrEmpty(entity.ShebaNumber),
                            entity.IsDefault
                        }
                    });

                    _logger.LogInformation("شماره حساب ایجاد شد — Id: {Id}, UserId: {UserId}", entity.Id, userId);

                    return ApiResponse<BankAccountResponseDto>.CreateSuccess(
                        MapToDto(entity),
                        "شماره حساب با موفقیت ایجاد شد",
                        201);
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ایجاد شماره حساب — UserId: {UserId}", userId);
                return ApiResponse<BankAccountResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<BankAccountListResponseDto>> GetBankAccountsAsync(
            int userId,
            int pageNumber = 1,
            int pageSize = 10)
        {
            try
            {
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                var cacheKey = BuildListCacheKey(userId, pageNumber, pageSize);
                if (_cache.TryGetValue(cacheKey, out BankAccountListResponseDto? cached) && cached != null)
                    return ApiResponse<BankAccountListResponseDto>.CreateSuccess(cached);

                var (items, totalCount) = await _bankAccountRepository.GetPagedByUserIdAsync(userId, pageNumber, pageSize);
                var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

                var response = new BankAccountListResponseDto
                {
                    BankAccounts = items.Select(MapToDto).ToList(),
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                _cache.Set(
                    cacheKey,
                    response,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = ListCacheDuration,
                        Size = 1
                    });

                return ApiResponse<BankAccountListResponseDto>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت لیست شماره حساب — UserId: {UserId}", userId);
                return ApiResponse<BankAccountListResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<BankAccountResponseDto>> GetBankAccountByIdAsync(int id, int userId)
        {
            try
            {
                var entity = await _bankAccountRepository.GetOwnedByIdAsync(id, userId, asNoTracking: true);
                if (entity == null)
                    return ApiResponse<BankAccountResponseDto>.NotFound("شماره حساب مورد نظر یافت نشد");

                return ApiResponse<BankAccountResponseDto>.CreateSuccess(MapToDto(entity));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت شماره حساب — Id: {Id}", id);
                return ApiResponse<BankAccountResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<BankAccountResponseDto>> UpdateBankAccountAsync(
            int id,
            int userId,
            UpdateBankAccountDto updateDto)
        {
            _logger.LogInformation("شروع به‌روزرسانی شماره حساب — Id: {Id}, UserId: {UserId}", id, userId);

            try
            {
                var entity = await _bankAccountRepository.GetOwnedByIdAsync(id, userId, asNoTracking: false);
                if (entity == null)
                    return ApiResponse<BankAccountResponseDto>.NotFound("شماره حساب مورد نظر یافت نشد");

                var originalTitle = entity.Title;
                var originalSmsDescription = entity.SmsCaption;
                var originalAccount = entity.AccountNumber;
                var originalCard = entity.CardNumber;
                var originalSheba = entity.ShebaNumber;

                if (updateDto.Title != null)
                {
                    var title = NormalizeTitle(updateDto.Title);
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        return ApiResponse<BankAccountResponseDto>.BadRequest(
                            "عنوان نامعتبر است",
                            errorCode: ErrorCodes.InvalidInput);
                    }

                    entity.Title = title;
                }

                // قرارداد Update: null = بدون تغییر؛ "" = حذف توضیحات ارسال
                if (updateDto.SmsDescription != null)
                    entity.SmsCaption = QuickSendLinkSmsHelper.NormalizeCaption(updateDto.SmsDescription);

                // قرارداد Update: null = بدون تغییر؛ رشته خالی/whitespace = پاک‌کردن فیلد
                if (updateDto.AccountNumber != null)
                {
                    var (normalized, error) = BusinessCardSocialNetworkHelper.NormalizeAccountNumber(updateDto.AccountNumber);
                    if (error != null)
                    {
                        return ApiResponse<BankAccountResponseDto>.BadRequest(
                            error,
                            errorCode: ErrorCodes.InvalidInput);
                    }

                    entity.AccountNumber = normalized;
                }

                if (updateDto.CardNumber != null)
                {
                    var (normalized, error) = BusinessCardSocialNetworkHelper.NormalizeCardNumber(updateDto.CardNumber);
                    if (error != null)
                    {
                        return ApiResponse<BankAccountResponseDto>.BadRequest(
                            error,
                            errorCode: ErrorCodes.InvalidInput);
                    }

                    entity.CardNumber = normalized;
                }

                if (updateDto.ShebaNumber != null)
                {
                    var (normalized, error) = BusinessCardSocialNetworkHelper.NormalizeSheba(updateDto.ShebaNumber);
                    if (error != null)
                    {
                        return ApiResponse<BankAccountResponseDto>.BadRequest(
                            error,
                            errorCode: ErrorCodes.InvalidInput);
                    }

                    entity.ShebaNumber = normalized;
                }

                if (updateDto.AccountNumber != null
                    || updateDto.CardNumber != null
                    || updateDto.ShebaNumber != null)
                {
                    if (string.IsNullOrWhiteSpace(entity.AccountNumber)
                        && string.IsNullOrWhiteSpace(entity.CardNumber)
                        && string.IsNullOrWhiteSpace(entity.ShebaNumber))
                    {
                        return ApiResponse<BankAccountResponseDto>.BadRequest(
                            "حداقل یکی از شماره حساب، کارت یا شبا الزامی است",
                            errorCode: ErrorCodes.InvalidInput);
                    }
                }

                if (updateDto.IsActive.HasValue)
                {
                    if (!updateDto.IsActive.Value && entity.IsDefault)
                    {
                        return ApiResponse<BankAccountResponseDto>.BadRequest(
                            "شماره حساب پیش‌فرض را نمی‌توان غیرفعال کرد. ابتدا مورد دیگری را پیش‌فرض کنید",
                            errorCode: ErrorCodes.InvalidInput);
                    }

                    entity.IsActive = updateDto.IsActive.Value;
                }

                entity.UpdatedAt = DateTime.UtcNow;

                var contentChanged =
                    !string.Equals(originalTitle, entity.Title, StringComparison.Ordinal) ||
                    !string.Equals(originalSmsDescription, entity.SmsCaption, StringComparison.Ordinal) ||
                    !string.Equals(originalAccount, entity.AccountNumber, StringComparison.Ordinal) ||
                    !string.Equals(originalCard, entity.CardNumber, StringComparison.Ordinal) ||
                    !string.Equals(originalSheba, entity.ShebaNumber, StringComparison.Ordinal);

                if (contentChanged)
                    QuickSendContentApprovalHelper.ResetToPending(entity);

                await _context.SaveChangesAsync();
                InvalidateUserCache(userId);

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Message,
                    Action = AuditActions.BankAccountUpdated,
                    EntityType = AuditEntityTypes.BankAccount,
                    EntityId = entity.Id.ToString(),
                    ActorUserId = userId,
                    After = new
                    {
                        entity.Title,
                        HasAccount = !string.IsNullOrEmpty(entity.AccountNumber),
                        HasCard = !string.IsNullOrEmpty(entity.CardNumber),
                        HasSheba = !string.IsNullOrEmpty(entity.ShebaNumber),
                        entity.IsActive,
                        entity.IsDefault,
                        entity.ApprovalStatus
                    }
                });

                _logger.LogInformation("شماره حساب به‌روزرسانی شد — Id: {Id}", id);
                return ApiResponse<BankAccountResponseDto>.CreateSuccess(
                    MapToDto(entity),
                    "شماره حساب با موفقیت به‌روزرسانی شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در به‌روزرسانی شماره حساب — Id: {Id}", id);
                return ApiResponse<BankAccountResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<bool>> DeleteBankAccountAsync(int id, int userId)
        {
            _logger.LogInformation("شروع حذف شماره حساب — Id: {Id}, UserId: {UserId}", id, userId);

            try
            {
                var entity = await _bankAccountRepository.GetOwnedByIdAsync(id, userId, asNoTracking: false);
                if (entity == null)
                    return ApiResponse<bool>.NotFound("شماره حساب مورد نظر یافت نشد");

                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var wasDefault = entity.IsDefault;
                    entity.IsDeleted = true;
                    entity.IsDefault = false;
                    entity.IsActive = false;
                    entity.UpdatedAt = DateTime.UtcNow;

                    if (wasDefault)
                    {
                        var nextDefault = await _context.BankAccounts
                            .Where(b => b.UserId == userId && !b.IsDeleted && b.IsActive && b.Id != id)
                            .OrderByDescending(b => b.CreatedAt)
                            .FirstOrDefaultAsync();

                        if (nextDefault != null)
                        {
                            nextDefault.IsDefault = true;
                            nextDefault.UpdatedAt = DateTime.UtcNow;
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }

                InvalidateUserCache(userId);

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Message,
                    Action = AuditActions.BankAccountDeleted,
                    EntityType = AuditEntityTypes.BankAccount,
                    EntityId = id.ToString(),
                    ActorUserId = userId
                });

                _logger.LogInformation("شماره حساب حذف شد — Id: {Id}", id);
                return ApiResponse<bool>.CreateSuccess(true, "شماره حساب با موفقیت حذف شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در حذف شماره حساب — Id: {Id}", id);
                return ApiResponse<bool>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<BankAccountResponseDto>> SetUserDefaultBankAccountAsync(int userId, int bankAccountId)
        {
            _logger.LogInformation(
                "تنظیم شماره حساب پیش‌فرض — BankAccountId: {BankAccountId}, UserId: {UserId}",
                bankAccountId,
                userId);

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var entity = await _context.BankAccounts
                    .FirstOrDefaultAsync(b =>
                        b.Id == bankAccountId &&
                        b.UserId == userId &&
                        b.IsActive &&
                        !b.IsDeleted);

                if (entity == null)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<BankAccountResponseDto>.NotFound("شماره حساب مورد نظر یافت نشد یا فعال نیست");
                }

                await UnsetDefaultsAsync(userId, exceptId: bankAccountId);

                entity.IsDefault = true;
                entity.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                InvalidateUserCache(userId);

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Message,
                    Action = AuditActions.BankAccountSetDefault,
                    EntityType = AuditEntityTypes.BankAccount,
                    EntityId = entity.Id.ToString(),
                    ActorUserId = userId
                });

                _logger.LogInformation("شماره حساب پیش‌فرض تنظیم شد — BankAccountId: {BankAccountId}", bankAccountId);
                return ApiResponse<BankAccountResponseDto>.CreateSuccess(
                    MapToDto(entity),
                    "شماره حساب پیش‌فرض با موفقیت تنظیم شد");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(ex, "تداخل همزمانی در تنظیم شماره حساب پیش‌فرض — Id: {Id}", bankAccountId);
                return ApiResponse<BankAccountResponseDto>.BadRequest(
                    "این شماره حساب در حال استفاده توسط درخواست دیگری است. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "خطا در تنظیم شماره حساب پیش‌فرض — Id: {Id}", bankAccountId);
                return ApiResponse<BankAccountResponseDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<DirectSendResultDto>> QuickSendBankAccountAsync(
            int userId,
            QuickSendBankAccountDto quickSendDto)
        {
            _logger.LogInformation(
                "ارسال سریع شماره حساب — UserId: {UserId}, ContactId: {ContactId}, BankAccountId: {BankAccountId}",
                userId,
                quickSendDto.ContactId,
                quickSendDto.BankAccountId);

            try
            {
                if (quickSendDto.ContactId <= 0 || quickSendDto.BankAccountId <= 0)
                {
                    return ApiResponse<DirectSendResultDto>.BadRequest(
                        "شناسه مخاطب و شماره حساب الزامی است",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var contact = await _contactRepository.GetByIdAsync(quickSendDto.ContactId);
                if (contact == null || contact.IsDeleted)
                    return ApiResponse<DirectSendResultDto>.NotFound("مخاطب یافت نشد");

                var notebook = await _notebookRepository.GetByIdAsync(contact.ContactNotebookId);
                if (notebook == null || notebook.UserId != userId || notebook.IsDeleted)
                    return ApiResponse<DirectSendResultDto>.Forbidden("مخاطب متعلق به شما نیست");

                var entity = await _bankAccountRepository.GetOwnedByIdAsync(
                    quickSendDto.BankAccountId,
                    userId,
                    asNoTracking: true);

                if (entity == null || !entity.IsActive)
                    return ApiResponse<DirectSendResultDto>.NotFound("شماره حساب مورد نظر یافت نشد یا فعال نیست");

                var smsContent = BuildSmsContent(entity);
                if (string.IsNullOrWhiteSpace(smsContent))
                {
                    return ApiResponse<DirectSendResultDto>.BadRequest(
                        "شماره حساب محتوایی برای ارسال ندارد",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var blocked = QuickSendContentApprovalHelper.TryBlockIfNotApproved(
                    entity.ApprovalStatus,
                    entity.RejectionReason,
                    "شماره حساب");
                if (blocked != null)
                    return blocked;

                var createMessageResult = await _messageService.CreateMessageAsync(userId, new CreateMessageDto
                {
                    Content = smsContent
                });

                if (!createMessageResult.Success || createMessageResult.Data == null)
                {
                    return ApiResponse<DirectSendResultDto>.BadRequest(
                        createMessageResult.Message ?? "خطا در ایجاد پیام",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var messageId = createMessageResult.Data.Id;

                var selectResult = await _messageService.SelectRecipientsAsync(userId, new SelectRecipientsDto
                {
                    MessageId = messageId,
                    SelectionType = "Individual",
                    MobileNumbers = new List<string> { contact.MobileNumber },
                    FullNames = new List<string> { contact.FullName ?? string.Empty }
                });

                if (!selectResult.Success || selectResult.Data == null)
                {
                    return ApiResponse<DirectSendResultDto>.BadRequest(
                        selectResult.Message ?? "خطا در انتخاب گیرندگان",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var session = await _context.MessageSessions
                    .Where(s =>
                        s.MessageId == messageId &&
                        s.UserId == userId &&
                        !s.IsDeleted &&
                        !s.IsUsed)
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefaultAsync();

                if (session == null)
                {
                    return ApiResponse<DirectSendResultDto>.BadRequest(
                        "خطا در ایجاد Session برای ارسال",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var sendResult = await _messageService.SendDirectMessageAsync(
                    userId,
                    messageId,
                    new SendDirectMessageDto
                    {
                        SendType = CampaignSendType.Quick,
                        PreventDuplicate = false,
                        DuplicatePreventionHours = 24,
                        SendToSpecificTags = false
                    },
                    session,
                    bypassAdminApproval: true);

                _logger.LogInformation(
                    "ارسال سریع شماره حساب انجام شد — MessageId: {MessageId}, ContactId: {ContactId}",
                    messageId,
                    quickSendDto.ContactId);

                return sendResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "خطا در ارسال سریع شماره حساب — ContactId: {ContactId}, BankAccountId: {BankAccountId}",
                    quickSendDto.ContactId,
                    quickSendDto.BankAccountId);
                return ApiResponse<DirectSendResultDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        /// <summary>
        /// پیش‌نمایش محتوا برای پنل ادمین — همان متن کامل SMS
        /// </summary>
        public static string BuildContentPreview(BankAccount entity) =>
            BuildSmsContent(entity);

        /// <summary>
        /// متن SMS: در صورت وجود توضیحات ارسال → قبل از عنوان؛ سپس عنوان و شماره‌ها.
        /// </summary>
        public static string BuildSmsContent(BankAccount entity)
        {
            var sb = new StringBuilder();

            var smsDescription = QuickSendLinkSmsHelper.NormalizeCaption(entity.SmsCaption);
            if (!string.IsNullOrEmpty(smsDescription))
                sb.AppendLine(smsDescription);

            if (!string.IsNullOrWhiteSpace(entity.Title))
                sb.AppendLine(entity.Title.Trim());

            if (!string.IsNullOrWhiteSpace(entity.AccountNumber))
                sb.AppendLine($"شماره حساب: {entity.AccountNumber}");

            if (!string.IsNullOrWhiteSpace(entity.CardNumber))
                sb.AppendLine($"شماره کارت: {entity.CardNumber}");

            if (!string.IsNullOrWhiteSpace(entity.ShebaNumber))
                sb.AppendLine($"شماره شبا: {entity.ShebaNumber}");

            return sb.ToString().Trim();
        }

        private async Task UnsetDefaultsAsync(int userId, int? exceptId = null)
        {
            var defaults = await _context.BankAccounts
                .Where(b =>
                    b.UserId == userId &&
                    b.IsDefault &&
                    !b.IsDeleted &&
                    (exceptId == null || b.Id != exceptId.Value))
                .ToListAsync();

            if (defaults.Count == 0)
                return;

            var now = DateTime.UtcNow;
            foreach (var item in defaults)
            {
                item.IsDefault = false;
                item.UpdatedAt = now;
            }
        }

        private void InvalidateUserCache(int userId) =>
            InvalidateListCache(_cache, userId);

        /// <summary>
        /// پاک‌سازی کش لیست شماره حساب (مثلاً بعد از تأیید/رد ادمین).
        /// </summary>
        public static void InvalidateListCache(IMemoryCache cache, int userId)
        {
            for (var page = 1; page <= 20; page++)
            {
                foreach (var size in new[] { 10, 20, 50, 100 })
                    cache.Remove(BuildListCacheKey(userId, page, size));
            }
        }

        private static string BuildListCacheKey(int userId, int pageNumber, int pageSize) =>
            $"bank_accounts:u{userId}:p{pageNumber}:s{pageSize}";

        private static string NormalizeTitle(string? title) =>
            string.IsNullOrWhiteSpace(title) ? string.Empty : title.Trim();

        private static (
            string? AccountNumber,
            string? CardNumber,
            string? ShebaNumber,
            string? Error) NormalizeBankFields(
            string? accountNumber,
            string? cardNumber,
            string? shebaNumber)
        {
            var (normalizedAccount, accountError) =
                BusinessCardSocialNetworkHelper.NormalizeAccountNumber(accountNumber);
            if (accountError != null)
                return (null, null, null, accountError);

            var (normalizedCard, cardError) =
                BusinessCardSocialNetworkHelper.NormalizeCardNumber(cardNumber);
            if (cardError != null)
                return (null, null, null, cardError);

            var (normalizedSheba, shebaError) =
                BusinessCardSocialNetworkHelper.NormalizeSheba(shebaNumber);
            if (shebaError != null)
                return (null, null, null, shebaError);

            return (normalizedAccount, normalizedCard, normalizedSheba, null);
        }

        private static BankAccountResponseDto MapToDto(BankAccount entity) => new()
        {
            Id = entity.Id,
            Title = entity.Title,
            SmsDescription = entity.SmsCaption,
            AccountNumber = entity.AccountNumber,
            CardNumber = entity.CardNumber,
            ShebaNumber = entity.ShebaNumber,
            IsActive = entity.IsActive,
            IsDefault = entity.IsDefault,
            CreatedAt = entity.CreatedAt,
            ApprovalStatus = entity.ApprovalStatus,
            RejectionReason = entity.RejectionReason,
            ApprovedAt = entity.ApprovedAt
        };
    }
}
