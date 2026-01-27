using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.QuickAction;
using Api_Vapp.DTOs.Message;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Data;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Services
{
    /// <summary>
    /// سرویس مدیریت اقدام‌های سریع (لینک‌ها)
    /// </summary>
    public class QuickActionService : IQuickActionService
    {
        private readonly IQuickActionRepository _quickActionRepository;
        private readonly IContactRepository _contactRepository;
        private readonly IContactNotebookRepository _notebookRepository;
        private readonly IMessageService _messageService;
        private readonly Api_Context _context;
        private readonly ILogger<QuickActionService> _logger;
        private readonly IFileUploadService _fileUploadService;

        public QuickActionService(
            IQuickActionRepository quickActionRepository,
            IContactRepository contactRepository,
            IContactNotebookRepository notebookRepository,
            IMessageService messageService,
            Api_Context context,
            ILogger<QuickActionService> logger,
            IFileUploadService fileUploadService)
        {
            _quickActionRepository = quickActionRepository;
            _contactRepository = contactRepository;
            _notebookRepository = notebookRepository;
            _messageService = messageService;
            _context = context;
            _logger = logger;
            _fileUploadService = fileUploadService;
        }

        public async Task<ApiResponse<QuickActionResponseDto>> CreateQuickActionAsync(int userId, CreateQuickActionDto createDto)
        {
            try
            {
                // اگر فایل آیکون ارسال شده باشد، آن را آپلود می‌کنیم
                if (createDto.IconFile != null && createDto.IconFile.Length > 0)
                {
                    // اعتبارسنجی فایل تصویر
                    var validationError = ValidateIconImage(createDto.IconFile);
                    if (!string.IsNullOrEmpty(validationError))
                    {
                        return ApiResponse<QuickActionResponseDto>.BadRequest(validationError);
                    }

                    // ایجاد یک QuickAction موقت برای گرفتن ID (قبل از آپلود)
                    var tempQuickAction = new QuickAction
                    {
                        UserId = userId,
                        Name = createDto.Name,
                        ActionType = createDto.ActionType,
                        Content = createDto.Content,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true,
                        IsDeleted = false
                    };

                    await _quickActionRepository.AddAsync(tempQuickAction);
                    // SaveChangesAsync is called inside AddAsync

                    try
                    {
                        string iconPath = await _fileUploadService.UploadFileAsync(
                            createDto.IconFile,
                            "QuickAction",
                            tempQuickAction.Id,
                            "icons");

                        // به‌روزرسانی مسیر آیکون
                        tempQuickAction.Icon = iconPath;
                        tempQuickAction.UpdatedAt = DateTime.UtcNow;
                        await _quickActionRepository.UpdateAsync(tempQuickAction);

                        _logger.LogInformation("Quick action created successfully with ID: {Id} by user: {UserId}", 
                            tempQuickAction.Id, userId);

                        return ApiResponse<QuickActionResponseDto>.CreateSuccess(
                            MapToDto(tempQuickAction), 
                            "لینک با موفقیت ایجاد شد",
                            201);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "خطا در آپلود آیکون برای اکشن جدید");
                        // حذف QuickAction ایجاد شده در صورت خطا
                        tempQuickAction.IsDeleted = true;
                        await _quickActionRepository.UpdateAsync(tempQuickAction);
                        return ApiResponse<QuickActionResponseDto>.InternalServerError("خطا در آپلود فایل آیکون");
                    }
                }
                else
                {
                    // اگر فایلی ارسال نشده، QuickAction بدون آیکون ایجاد می‌شود
                    var quickAction = new QuickAction
                    {
                        UserId = userId,
                        Name = createDto.Name,
                        ActionType = createDto.ActionType,
                        Content = createDto.Content,
                        Icon = null,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true,
                        IsDeleted = false
                    };

                    await _quickActionRepository.AddAsync(quickAction);
                    // SaveChangesAsync is called inside AddAsync

                    _logger.LogInformation("Quick action created successfully with ID: {Id} by user: {UserId}", 
                        quickAction.Id, userId);

                    return ApiResponse<QuickActionResponseDto>.CreateSuccess(
                        MapToDto(quickAction), 
                        "لینک با موفقیت ایجاد شد",
                        201);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating quick action for user: {UserId}", userId);
                return ApiResponse<QuickActionResponseDto>.InternalServerError($"خطا در ایجاد لینک: {ex.Message}");
            }
        }

        public async Task<ApiResponse<QuickActionListResponseDto>> GetQuickActionsAsync(int userId, int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                var actions = await _quickActionRepository.GetByUserIdAsync(userId);
                var actionsList = actions.ToList();
                var totalCount = actionsList.Count;
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var pagedActions = actionsList
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var actionDtos = pagedActions.Select(MapToDto).ToList();

                var response = new QuickActionListResponseDto
                {
                    QuickActions = actionDtos,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                return ApiResponse<QuickActionListResponseDto>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting quick actions for user: {UserId}", userId);
                return ApiResponse<QuickActionListResponseDto>.InternalServerError($"خطا در دریافت لیست لینک‌ها: {ex.Message}");
            }
        }

        public async Task<ApiResponse<QuickActionResponseDto>> GetQuickActionByIdAsync(int id, int userId)
        {
            try
            {
                var action = await _quickActionRepository.GetByIdAsync(id);
                if (action == null || action.UserId != userId)
                {
                    return ApiResponse<QuickActionResponseDto>.NotFound("لینک مورد نظر یافت نشد");
                }

                return ApiResponse<QuickActionResponseDto>.CreateSuccess(MapToDto(action));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting quick action: {Id}", id);
                return ApiResponse<QuickActionResponseDto>.InternalServerError($"خطا در دریافت لینک: {ex.Message}");
            }
        }

        public async Task<ApiResponse<QuickActionResponseDto>> UpdateQuickActionAsync(int id, int userId, UpdateQuickActionDto updateDto)
        {
            try
            {
                var action = await _quickActionRepository.GetByIdAsync(id);
                if (action == null || action.UserId != userId)
                {
                    return ApiResponse<QuickActionResponseDto>.NotFound("لینک مورد نظر یافت نشد");
                }

                // اگر فایل آیکون ارسال شده باشد، آن را آپلود می‌کنیم
                if (updateDto.IconFile != null && updateDto.IconFile.Length > 0)
                {
                    // اعتبارسنجی فایل تصویر
                    var validationError = ValidateIconImage(updateDto.IconFile);
                    if (!string.IsNullOrEmpty(validationError))
                    {
                        return ApiResponse<QuickActionResponseDto>.BadRequest(validationError);
                    }

                    // حذف آیکون قبلی در صورت وجود
                    string? oldIconPath = null;
                    if (!string.IsNullOrEmpty(action.Icon))
                    {
                        oldIconPath = action.Icon;
                    }

                    // آپلود آیکون جدید
                    string iconPath;
                    try
                    {
                        iconPath = await _fileUploadService.UploadFileAsync(
                            updateDto.IconFile,
                            "QuickAction",
                            action.Id,
                            "icons");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "خطا در آپلود آیکون برای اکشن {ActionId}", id);
                        return ApiResponse<QuickActionResponseDto>.InternalServerError("خطا در آپلود فایل آیکون");
                    }

                    // به‌روزرسانی مسیر آیکون
                    action.Icon = iconPath;

                    // حذف آیکون قدیمی در صورت وجود
                    if (!string.IsNullOrEmpty(oldIconPath))
                    {
                        try
                        {
                            await _fileUploadService.DeleteFileAsync(oldIconPath, "QuickAction", action.Id, "icons");
                        }
                        catch (Exception deleteEx)
                        {
                            _logger.LogWarning(deleteEx, "خطا در حذف آیکون قدیمی: {OldIconPath}", oldIconPath);
                            // ادامه می‌دهیم حتی اگر حذف آیکون قدیمی با خطا مواجه شود
                        }
                    }
                }

                if (updateDto.Name != null) action.Name = updateDto.Name;
                // ActionType: اگر string.Empty ارسال شده باشد، آن را null می‌کنیم (برای حذف)
                // اگر مقدار معتبری ارسال شده باشد، به‌روزرسانی می‌شود
                if (updateDto.ActionType != null)
                {
                    if (updateDto.ActionType == string.Empty)
                        action.ActionType = null;
                    else
                        action.ActionType = updateDto.ActionType;
                }
                if (updateDto.Content != null) action.Content = updateDto.Content;
                if (updateDto.IsActive.HasValue) action.IsActive = updateDto.IsActive.Value;

                action.UpdatedAt = DateTime.UtcNow;

                await _quickActionRepository.UpdateAsync(action);
                // SaveChangesAsync is called inside UpdateAsync

                _logger.LogInformation("Quick action updated successfully with ID: {Id}", id);

                return ApiResponse<QuickActionResponseDto>.CreateSuccess(
                    MapToDto(action), 
                    "لینک با موفقیت به‌روزرسانی شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating quick action: {Id}", id);
                return ApiResponse<QuickActionResponseDto>.InternalServerError($"خطا در به‌روزرسانی لینک: {ex.Message}");
            }
        }

        public async Task<ApiResponse<bool>> DeleteQuickActionAsync(int id, int userId)
        {
            try
            {
                var action = await _quickActionRepository.GetByIdAsync(id);
                if (action == null || action.UserId != userId)
                {
                    return ApiResponse<bool>.NotFound("لینک مورد نظر یافت نشد");
                }

                // حذف فایل آیکون از سرور در صورت وجود
                if (!string.IsNullOrEmpty(action.Icon))
                {
                    try
                    {
                        await _fileUploadService.DeleteFileAsync(action.Icon, "QuickAction", action.Id, "icons");
                        _logger.LogInformation("Icon file deleted successfully for quick action: {Id}", id);
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(deleteEx, "خطا در حذف فایل آیکون برای اکشن {ActionId}: {IconPath}", id, action.Icon);
                        // ادامه می‌دهیم حتی اگر حذف فایل با خطا مواجه شود
                    }
                }

                action.IsDeleted = true;
                action.UpdatedAt = DateTime.UtcNow;

                await _quickActionRepository.UpdateAsync(action);
                // SaveChangesAsync is called inside UpdateAsync

                _logger.LogInformation("Quick action deleted successfully with ID: {Id}", id);

                return ApiResponse<bool>.CreateSuccess(true, "لینک با موفقیت حذف شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting quick action: {Id}", id);
                return ApiResponse<bool>.InternalServerError($"خطا در حذف لینک: {ex.Message}");
            }
        }

        public async Task<ApiResponse<string>> UploadIconAsync(int id, int userId, Microsoft.AspNetCore.Http.IFormFile iconFile)
        {
            try
            {
                var action = await _quickActionRepository.GetByIdAsync(id);
                if (action == null || action.UserId != userId)
                {
                    return ApiResponse<string>.NotFound("اکشن مورد نظر یافت نشد");
                }

                // اعتبارسنجی فایل تصویر
                var validationError = ValidateIconImage(iconFile);
                if (!string.IsNullOrEmpty(validationError))
                {
                    return ApiResponse<string>.BadRequest(validationError);
                }

                // حذف آیکون قبلی در صورت وجود
                string? oldIconPath = null;
                if (!string.IsNullOrEmpty(action.Icon))
                {
                    oldIconPath = action.Icon;
                }

                // آپلود آیکون جدید
                string iconPath;
                try
                {
                    iconPath = await _fileUploadService.UploadFileAsync(
                        iconFile,
                        "QuickAction",
                        action.Id,
                        "icons");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطا در آپلود آیکون برای اکشن {ActionId}", id);
                    return ApiResponse<string>.InternalServerError("خطا در آپلود فایل آیکون");
                }

                // به‌روزرسانی مسیر آیکون در دیتابیس
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    action.Icon = iconPath;
                    action.UpdatedAt = DateTime.UtcNow;
                    await _quickActionRepository.UpdateAsync(action);

                    // حذف آیکون قدیمی در صورت وجود
                    if (!string.IsNullOrEmpty(oldIconPath))
                    {
                        try
                        {
                            await _fileUploadService.DeleteFileAsync(
                                oldIconPath,
                                "QuickAction",
                                action.Id,
                                "icons");
                        }
                        catch (Exception deleteEx)
                        {
                            _logger.LogWarning(deleteEx, "خطا در حذف آیکون قدیمی: {OldIconPath}", oldIconPath);
                            // ادامه می‌دهیم حتی اگر حذف فایل قدیمی موفق نشد
                        }
                    }

                    await transaction.CommitAsync();

                    _logger.LogInformation("آیکون با موفقیت آپلود شد برای اکشن {ActionId}", id);
                    return ApiResponse<string>.CreateSuccess(iconPath, "آیکون با موفقیت آپلود شد");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "خطا در به‌روزرسانی مسیر آیکون برای اکشن {ActionId}", id);

                    // حذف فایل آپلود شده در صورت خطا
                    try
                    {
                        await _fileUploadService.DeleteFileAsync(iconPath, "QuickAction", action.Id, "icons");
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(deleteEx, "خطا در حذف فایل آپلود شده پس از خطا: {IconPath}", iconPath);
                    }

                    return ApiResponse<string>.InternalServerError("خطا در ذخیره اطلاعات آیکون");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در آپلود آیکون برای اکشن {ActionId}", id);
                return ApiResponse<string>.InternalServerError($"خطا در آپلود آیکون: {ex.Message}");
            }
        }

        /// <summary>
        /// اعتبارسنجی فایل عکس آیکون
        /// </summary>
        private string? ValidateIconImage(Microsoft.AspNetCore.Http.IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                return "فایل تصویر انتخاب نشده است";
            }

            // بررسی نوع فایل - فقط عکس مجاز است
            var allowedImageTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp", "image/svg+xml" };
            var contentType = imageFile.ContentType.ToLower();
            
            if (!allowedImageTypes.Contains(contentType))
            {
                return $"نوع فایل '{contentType}' مجاز نیست. فقط فایل‌های تصویری (JPEG, PNG, GIF, WebP, SVG) قابل قبول هستند";
            }

            // بررسی حجم فایل (حداکثر 5 مگابایت برای آیکون)
            var maxSize = 5 * 1024 * 1024; // 5 MB
            if (imageFile.Length > maxSize)
            {
                var fileSizeMB = imageFile.Length / (1024.0 * 1024.0);
                return $"حجم فایل ({fileSizeMB:F2} MB) از حد مجاز (5 MB) بیشتر است";
            }

            return null; // معتبر است
        }

        public async Task<ApiResponse<QuickActionResponseDto>> SetUserDefaultActionAsync(int userId, int actionId)
        {
            _logger.LogInformation("📥 Setting default action for user {UserId}, action {ActionId}", userId, actionId);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // بررسی وجود کاربر
                var userExists = await _context.Users.AnyAsync(u => u.Id == userId && !u.IsDeleted);
                if (!userExists)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<QuickActionResponseDto>.NotFound("کاربر یافت نشد");
                }

                // بررسی وجود اکشن و تعلق آن به کاربر
                var action = await _context.QuickActions
                    .FirstOrDefaultAsync(qa => qa.Id == actionId && 
                                               qa.UserId == userId && 
                                               qa.IsActive && 
                                               !qa.IsDeleted);

                if (action == null)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<QuickActionResponseDto>.NotFound("لینک مورد نظر یافت نشد یا متعلق به شما نیست");
                }

                // غیرفعال کردن تمام اکشن‌های پیش‌فرض قبلی کاربر (اگر وجود داشته باشند)
                var previousDefaultActions = await _context.QuickActions
                    .Where(qa => qa.UserId == userId && 
                                 qa.IsDefault && 
                                 qa.IsActive && 
                                 !qa.IsDeleted &&
                                 qa.Id != actionId)
                    .ToListAsync();

                if (previousDefaultActions.Any())
                {
                    foreach (var previousAction in previousDefaultActions)
                    {
                        previousAction.IsDefault = false;
                        previousAction.UpdatedAt = DateTime.UtcNow;
                        _context.QuickActions.Update(previousAction);
                    }
                    _logger.LogInformation("🔧 {Count} previous default action(s) unset for user {UserId}", 
                        previousDefaultActions.Count, userId);
                }

                // تنظیم اکشن جدید به عنوان پیش‌فرض
                action.IsDefault = true;
                action.UpdatedAt = DateTime.UtcNow;
                _context.QuickActions.Update(action);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // لود کردن action برای mapping
                var updatedAction = await _context.QuickActions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(qa => qa.Id == actionId);

                _logger.LogInformation("✅ Default action set successfully: Action {ActionId} for user {UserId}", 
                    actionId, userId);

                return ApiResponse<QuickActionResponseDto>.CreateSuccess(
                    MapToDto(updatedAction ?? action),
                    "لینک پیش‌فرض با موفقیت تنظیم شد"
                );
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(ex, "⚠️ Concurrency conflict while setting default action: {ActionId} for user: {UserId}", 
                    actionId, userId);
                return ApiResponse<QuickActionResponseDto>.BadRequest("این لینک در حال استفاده توسط درخواست دیگری است. لطفاً دوباره تلاش کنید");
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "❌ Database error setting default action: {ActionId} for user: {UserId}", 
                    actionId, userId);
                return ApiResponse<QuickActionResponseDto>.InternalServerError("خطا در تنظیم لینک پیش‌فرض. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "❌ Unexpected error setting default action: {ActionId} for user: {UserId}", 
                    actionId, userId);
                return ApiResponse<QuickActionResponseDto>.InternalServerError("خطای غیرمنتظره در تنظیم لینک پیش‌فرض");
            }
        }

        public async Task<ApiResponse<DirectSendResultDto>> QuickSendActionAsync(int userId, QuickSendActionDto quickSendDto)
        {
            _logger.LogInformation("📥 Quick send action for user {UserId}, contact {ContactId}", userId, quickSendDto.ContactId);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. بررسی وجود کاربر
                var userExists = await _context.Users.AnyAsync(u => u.Id == userId && !u.IsDeleted);
                if (!userExists)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<DirectSendResultDto>.NotFound("کاربر یافت نشد");
                }

                // 2. پیدا کردن مخاطب و بررسی تعلق آن به کاربر
                var contact = await _contactRepository.GetByIdAsync(quickSendDto.ContactId);
                if (contact == null || contact.IsDeleted)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<DirectSendResultDto>.NotFound("مخاطب یافت نشد");
                }

                // بررسی تعلق دفترچه به کاربر
                var notebook = await _notebookRepository.GetByIdAsync(contact.ContactNotebookId);
                if (notebook == null || notebook.UserId != userId || notebook.IsDeleted)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<DirectSendResultDto>.Forbidden("مخاطب متعلق به شما نیست");
                }

                // 3. پیدا کردن اکشن پیش‌فرض کاربر
                var defaultAction = await _context.QuickActions
                    .FirstOrDefaultAsync(qa => qa.UserId == userId && 
                                               qa.IsDefault && 
                                               qa.IsActive && 
                                               !qa.IsDeleted);

                string messageContent;

                if (defaultAction == null)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<DirectSendResultDto>.BadRequest("لینک پیش‌فرضی تنظیم نشده است. لطفاً ابتدا یک لینک را به عنوان پیش‌فرض انتخاب کنید");
                }

                if (string.IsNullOrWhiteSpace(defaultAction.Content))
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<DirectSendResultDto>.BadRequest("لینک پیش‌فرض محتوایی ندارد");
                }

                // استفاده از محتوای اکشن پیش‌فرض به عنوان محتوای پیام
                messageContent = defaultAction.Content;

                // 4. Commit Transaction قبل از صدا زدن MessageService (که خودش Transaction دارد)
                await transaction.CommitAsync();

                // 5. استفاده از MessageService برای ارسال پیام
                // ایجاد یک پیام موقت با محتوای لینک
                var createMessageDto = new CreateMessageDto
                {
                    Content = messageContent
                };

                var createMessageResult = await _messageService.CreateMessageAsync(userId, createMessageDto);
                if (!createMessageResult.Success || createMessageResult.Data == null)
                {
                    return ApiResponse<DirectSendResultDto>.BadRequest(createMessageResult.Message ?? "خطا در ایجاد پیام");
                }

                var message = await _context.Messages
                    .FirstOrDefaultAsync(m => m.Id == createMessageResult.Data.Id);

                if (message == null)
                {
                    return ApiResponse<DirectSendResultDto>.BadRequest("خطا در ایجاد پیام");
                }

                // 6. شخصی‌سازی پیام با اطلاعات مخاطب (اگر MessageService این قابلیت را دارد)
                // در اینجا فقط محتوای لینک را ارسال می‌کنیم، بدون شخصی‌سازی
                // اگر نیاز به شخصی‌سازی باشد، باید از PersonalizeMessageWithContactAsync استفاده کنیم

                // 7. ایجاد SelectRecipientsDto برای انتخاب گیرنده
                var selectRecipientsDto = new SelectRecipientsDto
                {
                    MessageId = message.Id,
                    SelectionType = "Individual",
                    MobileNumbers = new List<string> { contact.MobileNumber },
                    FullNames = new List<string> { contact.FullName ?? "" }
                };

                // 8. انتخاب گیرندگان (این یک Session ایجاد می‌کند و خودش Transaction دارد)
                var selectResult = await _messageService.SelectRecipientsAsync(userId, selectRecipientsDto);
                if (!selectResult.Success || selectResult.Data == null)
                {
                    return ApiResponse<DirectSendResultDto>.BadRequest(selectResult.Message ?? "خطا در انتخاب گیرندگان");
                }

                // 9. پیدا کردن Session ایجاد شده
                var session = await _context.MessageSessions
                    .Where(s => s.MessageId == message.Id && 
                               s.UserId == userId && 
                               !s.IsDeleted && 
                               !s.IsUsed)
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefaultAsync();

                if (session == null)
                {
                    return ApiResponse<DirectSendResultDto>.BadRequest("خطا در ایجاد Session برای ارسال");
                }

                // 10. ارسال مستقیم پیام
                var sendDto = new SendDirectMessageDto
                {
                    SendType = CampaignSendType.Quick,
                    PreventDuplicate = false,
                    DuplicatePreventionHours = 24,
                    SendToSpecificTags = false
                };

                var sendResult = await _messageService.SendDirectMessageAsync(userId, message.Id, sendDto, session);

                _logger.LogInformation("✅ Quick send action completed - MessageId: {MessageId}, ContactId: {ContactId}, UserId: {UserId}", 
                    message.Id, quickSendDto.ContactId, userId);

                return sendResult;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch { }
                _logger.LogWarning(ex, "⚠️ Concurrency conflict while quick sending action: ContactId {ContactId} for user: {UserId}", 
                    quickSendDto.ContactId, userId);
                return ApiResponse<DirectSendResultDto>.BadRequest("این درخواست در حال استفاده توسط درخواست دیگری است. لطفاً دوباره تلاش کنید");
            }
            catch (DbUpdateException ex)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch { }
                _logger.LogError(ex, "❌ Database error quick sending action: ContactId {ContactId} for user: {UserId}", 
                    quickSendDto.ContactId, userId);
                return ApiResponse<DirectSendResultDto>.InternalServerError("خطا در ارسال لینک سریع. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch { }
                _logger.LogError(ex, "❌ Unexpected error quick sending action: ContactId {ContactId} for user: {UserId}", 
                    quickSendDto.ContactId, userId);
                return ApiResponse<DirectSendResultDto>.InternalServerError("خطای غیرمنتظره در ارسال لینک سریع");
            }
        }

        /// <summary>
        /// تشخیص نوع لینک بر اساس ساختار URL
        /// </summary>
        private string? DetectLinkType(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            // نرمال‌سازی URL (حذف فاصله و تبدیل به حروف کوچک)
            var normalizedUrl = url.Trim().ToLowerInvariant();

            // حذف پروتکل‌ها برای مقایسه بهتر
            if (normalizedUrl.StartsWith("http://"))
                normalizedUrl = normalizedUrl.Substring(7);
            else if (normalizedUrl.StartsWith("https://"))
                normalizedUrl = normalizedUrl.Substring(8);

            // حذف www. در صورت وجود
            if (normalizedUrl.StartsWith("www."))
                normalizedUrl = normalizedUrl.Substring(4);

            // تشخیص بر اساس دامنه
            if (normalizedUrl.Contains("instagram.com") || normalizedUrl.StartsWith("instagram.com/") || 
                normalizedUrl.StartsWith("instagr.am/"))
                return "Instagram";

            if (normalizedUrl.Contains("t.me/") || normalizedUrl.StartsWith("t.me/") || 
                normalizedUrl.Contains("telegram.me/") || normalizedUrl.StartsWith("telegram.me/"))
                return "Telegram";

            if (normalizedUrl.Contains("wa.me/") || normalizedUrl.StartsWith("wa.me/") || 
                normalizedUrl.Contains("whatsapp.com/") || normalizedUrl.StartsWith("whatsapp.com/") ||
                normalizedUrl.Contains("api.whatsapp.com/") || normalizedUrl.StartsWith("api.whatsapp.com/"))
                return "WhatsApp";

            if (normalizedUrl.Contains("linkedin.com/") || normalizedUrl.StartsWith("linkedin.com/") ||
                normalizedUrl.Contains("linked.in/") || normalizedUrl.StartsWith("linked.in/"))
                return "LinkedIn";

            if (normalizedUrl.Contains("twitter.com/") || normalizedUrl.StartsWith("twitter.com/") ||
                normalizedUrl.Contains("x.com/") || normalizedUrl.StartsWith("x.com/") ||
                normalizedUrl.Contains("t.co/") || normalizedUrl.StartsWith("t.co/"))
                return "Twitter";

            if (normalizedUrl.Contains("youtube.com/") || normalizedUrl.StartsWith("youtube.com/") ||
                normalizedUrl.Contains("youtu.be/") || normalizedUrl.StartsWith("youtu.be/"))
                return "YouTube";

            if (normalizedUrl.Contains("facebook.com/") || normalizedUrl.StartsWith("facebook.com/") ||
                normalizedUrl.Contains("fb.com/") || normalizedUrl.StartsWith("fb.com/") ||
                normalizedUrl.Contains("fb.me/") || normalizedUrl.StartsWith("fb.me/"))
                return "Facebook";

            if (normalizedUrl.Contains("tiktok.com/") || normalizedUrl.StartsWith("tiktok.com/") ||
                normalizedUrl.Contains("vm.tiktok.com/") || normalizedUrl.StartsWith("vm.tiktok.com/"))
                return "TikTok";

            if (normalizedUrl.Contains("snapchat.com/") || normalizedUrl.StartsWith("snapchat.com/") ||
                normalizedUrl.Contains("snapchat.com/add/") || normalizedUrl.StartsWith("snapchat.com/add/"))
                return "Snapchat";

            if (normalizedUrl.Contains("pinterest.com/") || normalizedUrl.StartsWith("pinterest.com/") ||
                normalizedUrl.Contains("pin.it/") || normalizedUrl.StartsWith("pin.it/"))
                return "Pinterest";

            if (normalizedUrl.Contains("reddit.com/") || normalizedUrl.StartsWith("reddit.com/"))
                return "Reddit";

            if (normalizedUrl.Contains("discord.com/") || normalizedUrl.StartsWith("discord.com/") ||
                normalizedUrl.Contains("discord.gg/") || normalizedUrl.StartsWith("discord.gg/"))
                return "Discord";

            if (normalizedUrl.Contains("github.com/") || normalizedUrl.StartsWith("github.com/"))
                return "GitHub";

            if (normalizedUrl.Contains("spotify.com/") || normalizedUrl.StartsWith("spotify.com/"))
                return "Spotify";

            if (normalizedUrl.Contains("apple.com/") || normalizedUrl.StartsWith("apple.com/") ||
                normalizedUrl.Contains("apps.apple.com/") || normalizedUrl.StartsWith("apps.apple.com/"))
                return "Apple";

            if (normalizedUrl.Contains("play.google.com/") || normalizedUrl.StartsWith("play.google.com/"))
                return "GooglePlay";

            // اگر هیچ کدام از پلتفرم‌های بالا نبود، به عنوان Website عمومی در نظر گرفته می‌شود
            if (normalizedUrl.Contains(".") || normalizedUrl.StartsWith("http") || normalizedUrl.StartsWith("www."))
                return "Website";

            return "Unknown";
        }

        private QuickActionResponseDto MapToDto(QuickAction action)
        {
            return new QuickActionResponseDto
            {
                Id = action.Id,
                Name = action.Name,
                ActionType = action.ActionType,
                Content = action.Content,
                Icon = action.Icon,
                DisplayOrder = action.DisplayOrder,
                IsActive = action.IsActive,
                IsDefault = action.IsDefault,
                CreatedAt = action.CreatedAt,
                LinkType = DetectLinkType(action.Content)
            };
        }
    }
}

