using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Message;
using Api_Vapp.DTOs.Sms;
using Api_Vapp.Constants;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Api_Vapp.Services
{
    /// <summary>
    /// پیاده‌سازی سرویس مدیریت پیام‌ها و کمپین‌ها
    /// </summary>
    public class MessageService : IMessageService
    {
        private readonly IMessageRepository _messageRepository;
        private readonly IMessageCampaignRepository _campaignRepository;
        private readonly IMessageTemplateRepository _templateRepository;
        private readonly IMessageSessionRepository _sessionRepository;
        private readonly IContactRepository _contactRepository;
        private readonly IContactNotebookRepository _notebookRepository;
        private readonly IUserRepository _userRepository;
        private readonly ISmsService _smsService;
        private readonly ISmsDeliveryTrackingService _deliveryTracking;
        private readonly Api_Vapp.Data.Api_Context _context;
        private readonly ILogger<MessageService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly IFileUploadService? _fileUploadService;

        // هزینه هر پارت پیام (قابل تنظیم از appsettings)
        private readonly decimal _costPerPart = 160; // تومان

        /// <summary>
        /// بررسی اینکه آیا چک کردن کیف پول غیرفعال است یا نه
        /// </summary>
        private bool IsWalletCheckDisabled()
        {
            var environmentName = _hostEnvironment.EnvironmentName;
            return _configuration.GetValue<bool>($"{environmentName}:DisableWalletCheck", false);
        }

        public MessageService(
            IMessageRepository messageRepository,
            IMessageCampaignRepository campaignRepository,
            IMessageTemplateRepository templateRepository,
            IMessageSessionRepository sessionRepository,
            IContactRepository contactRepository,
            IContactNotebookRepository notebookRepository,
            IUserRepository userRepository,
            ISmsService smsService,
            ISmsDeliveryTrackingService deliveryTracking,
            Api_Vapp.Data.Api_Context context,
            ILogger<MessageService> logger,
            IConfiguration configuration,
            IHostEnvironment hostEnvironment,
            IFileUploadService? fileUploadService = null)
        {
            _messageRepository = messageRepository;
            _campaignRepository = campaignRepository;
            _templateRepository = templateRepository;
            _sessionRepository = sessionRepository;
            _contactRepository = contactRepository;
            _notebookRepository = notebookRepository;
            _userRepository = userRepository;
            _smsService = smsService;
            _deliveryTracking = deliveryTracking;
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _hostEnvironment = hostEnvironment;
            _fileUploadService = fileUploadService;
        }

        #region Message Operations

        public async Task<ApiResponse<MessageResponseDto>> CreateMessageAsync(int userId, CreateMessageDto createDto)
        {
            try
            {
                // محتوای پیام (می‌تواند خالی باشد و بعداً به‌روزرسانی شود)
                var content = string.IsNullOrWhiteSpace(createDto.Content) ? "" : createDto.Content.Trim();

                var message = new Message
                {
                    UserId = userId,
                    Content = content,
                    CharacterCount = SmsPartsCalculator.CountMessageCharacters(content),
                    PartsCount = SmsPartsCalculator.CalculateParts(content),
                    IsPersonalized = ContainsPlaceholders(content),
                    Placeholders = ExtractPlaceholders(content),
                    Status = "Draft",
                    CreatedAt = DateTime.UtcNow
                };

                var createdMessage = await _messageRepository.AddAsync(message);

                _logger.LogInformation("Message created successfully with ID: {MessageId} by user: {UserId}", 
                    createdMessage.Id, userId);

                return ApiResponse<MessageResponseDto>.CreateSuccess(
                    await MapToMessageResponseDtoAsync(createdMessage),
                    "پیام با موفقیت ایجاد شد",
                    201
                );
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "محتوای پیام نامعتبر برای کاربر: {UserId}", userId);
                // استفاده از Middleware برای ترجمه - اگر پیام فارسی است همان را برمی‌گردانیم
                var errorMessage = ControlledErrorHelper.SanitizeArgumentMessage(ex.Message, "محتویات پیام نامعتبر است");
                return ApiResponse<MessageResponseDto>.BadRequest(errorMessage);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating message for user: {UserId}", userId);
                return ApiResponse<MessageResponseDto>.InternalServerError("خطا در ذخیره‌سازی پیام. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating message for user: {UserId}", userId);
                return ApiResponse<MessageResponseDto>.InternalServerError("خطای غیرمنتظره در ایجاد پیام");
            }
        }

        public async Task<ApiResponse<MessageResponseDto>> GetMessageByIdAsync(int messageId, int userId)
        {
            try
            {
                var message = await _messageRepository.GetByIdWithTemplateAsync(messageId);

                if (message == null)
                {
                    return ApiResponse<MessageResponseDto>.NotFound("پیام یافت نشد");
                }

                if (message.UserId != userId)
                {
                    return ApiResponse<MessageResponseDto>.Forbidden("شما مجاز به دسترسی به این پیام نیستید");
                }

                return ApiResponse<MessageResponseDto>.CreateSuccess(
                    await MapToMessageResponseDtoAsync(message)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting message: {MessageId}", messageId);
                return ApiResponse<MessageResponseDto>.InternalServerError("خطا در دریافت پیام");
            }
        }

        public async Task<ApiResponse<MessageListResponseDto>> GetMessagesAsync(int userId, int pageNumber = 1, int pageSize = 10, string? searchTerm = null)
        {
            try
            {
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                IEnumerable<Message> messages;
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var allMessages = await _messageRepository.GetByUserIdAsync(userId);
                    messages = allMessages.Where(m => 
                        m.Content.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    messages = await _messageRepository.GetByUserIdAsync(userId);
                }

                var messagesList = messages.ToList();
                var totalCount = messagesList.Count;
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var pagedMessages = messagesList
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var messageDtos = new List<MessageResponseDto>();
                foreach (var message in pagedMessages)
                {
                    messageDtos.Add(await MapToMessageResponseDtoAsync(message));
                }

                var response = new MessageListResponseDto
                {
                    Messages = messageDtos,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                return ApiResponse<MessageListResponseDto>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting messages for user: {UserId}", userId);
                return ApiResponse<MessageListResponseDto>.InternalServerError("خطا در دریافت لیست پیام‌ها");
            }
        }

        public async Task<ApiResponse<MessageResponseDto>> UpdateMessageAsync(int messageId, int userId, UpdateMessageDto updateDto)
        {
            try
            {
                var message = await _messageRepository.GetByIdAsync(messageId);

                if (message == null)
                {
                    return ApiResponse<MessageResponseDto>.NotFound("پیام یافت نشد");
                }

                if (message.UserId != userId)
                {
                    return ApiResponse<MessageResponseDto>.Forbidden("شما مجاز به ویرایش این پیام نیستید");
                }

                bool hasChanges = false;

                // به‌روزرسانی Content فقط در صورت ارسال مقدار غیرخالی
                if (!string.IsNullOrWhiteSpace(updateDto.Content))
                {
                    message.Content = updateDto.Content.Trim();
                    message.CharacterCount = SmsPartsCalculator.CountMessageCharacters(message.Content);
                    message.PartsCount = SmsPartsCalculator.CalculateParts(message.Content);
                    message.IsPersonalized = ContainsPlaceholders(message.Content);
                    message.Placeholders = ExtractPlaceholders(message.Content);
                    hasChanges = true;
                }

                // اگر هیچ تغییری ایجاد نشده
                if (!hasChanges)
                {
                    return ApiResponse<MessageResponseDto>.CreateSuccess(
                        await MapToMessageResponseDtoAsync(message),
                        "هیچ تغییری اعمال نشد"
                    );
                }

                message.UpdatedAt = DateTime.UtcNow;

                var updatedMessage = await _messageRepository.UpdateAsync(message);

                _logger.LogInformation("Message updated successfully with ID: {MessageId}", messageId);

                return ApiResponse<MessageResponseDto>.CreateSuccess(
                    await MapToMessageResponseDtoAsync(updatedMessage),
                    "پیام با موفقیت به‌روزرسانی شد"
                );
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid message content for message: {MessageId}", messageId);
                // استفاده از Middleware برای ترجمه - اگر پیام فارسی است همان را برمی‌گردانیم
                var errorMessage = ControlledErrorHelper.SanitizeArgumentMessage(ex.Message, "محتویات پیام نامعتبر است");
                return ApiResponse<MessageResponseDto>.BadRequest(errorMessage);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error updating message: {MessageId}", messageId);
                return ApiResponse<MessageResponseDto>.InternalServerError("خطا در به‌روزرسانی پیام. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error updating message: {MessageId}", messageId);
                return ApiResponse<MessageResponseDto>.InternalServerError("خطای غیرمنتظره در به‌روزرسانی پیام");
            }
        }

        public async Task<ApiResponse<bool>> DeleteMessageAsync(int messageId, int userId)
        {
            try
            {
                var message = await _messageRepository.GetByIdAsync(messageId);

                if (message == null)
                {
                    return ApiResponse<bool>.NotFound("پیام یافت نشد");
                }

                if (message.UserId != userId)
                {
                    return ApiResponse<bool>.Forbidden("شما مجاز به حذف این پیام نیستید");
                }

                // بررسی استفاده در کمپین‌ها
                var campaigns = await _campaignRepository.GetByUserIdAsync(userId);
                if (campaigns.Any(c => c.MessageId == messageId && c.Status != "Draft" && c.Status != "Cancelled"))
                {
                    return ApiResponse<bool>.BadRequest("این پیام در کمپین‌های فعال استفاده شده و قابل حذف نیست");
                }

                message.IsDeleted = true;
                message.UpdatedAt = DateTime.UtcNow;
                await _messageRepository.UpdateAsync(message);

                _logger.LogInformation("Message deleted successfully with ID: {MessageId}", messageId);

                return ApiResponse<bool>.CreateSuccess(true, "پیام با موفقیت حذف شد");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error deleting message: {MessageId}", messageId);
                return ApiResponse<bool>.InternalServerError("خطا در حذف پیام. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error deleting message: {MessageId}", messageId);
                return ApiResponse<bool>.InternalServerError("خطای غیرمنتظره در حذف پیام");
            }
        }

        #endregion

        #region Campaign Operations

        public async Task<ApiResponse<CampaignSummaryDto>> GetCampaignSummaryAsync(int userId, int messageId)
        {
            try
            {
                var message = await _messageRepository.GetByIdAsync(messageId);
                if (message == null || message.UserId != userId)
                {
                    return ApiResponse<CampaignSummaryDto>.NotFound("پیام یافت نشد");
                }

                // خواندن Session و تنظیمات
                var session = await _sessionRepository.GetActiveSessionByMessageIdAsync(messageId, userId);
                if (session == null || string.IsNullOrEmpty(session.RecipientsJson))
                {
                    return ApiResponse<CampaignSummaryDto>.BadRequest(
                        "هیچ گیرنده‌ای برای این پیام انتخاب نشده است. لطفاً ابتدا گیرندگان را انتخاب کنید.");
                }

                // خواندن گیرندگان از Session
                List<RecipientItemDto> recipients;
                try
                {
                    recipients = JsonSerializer.Deserialize<List<RecipientItemDto>>(session.RecipientsJson)
                        ?? new List<RecipientItemDto>();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deserializing recipients from Session - SessionId: {SessionId}", session.Id);
                    return ApiResponse<CampaignSummaryDto>.InternalServerError("خطا در خواندن لیست گیرندگان");
                }

                // خواندن تنظیمات از Session
                CampaignSendType sendType = CampaignSendType.Quick;
                DateTime? scheduledAt = null;
                bool preventDuplicate = false;
                int duplicatePreventionHours = 24;
                bool sendToSpecificTags = false;
                List<int>? selectedTagIds = null;

                try
                {
                    var selectionCriteria = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(session.SelectionCriteria ?? "{}") ?? new Dictionary<string, JsonElement>();

                    if (selectionCriteria.TryGetValue("SendType", out var sendTypeElement))
                    {
                        if (Enum.TryParse<CampaignSendType>(sendTypeElement.GetString(), out var parsedSendType))
                        {
                            sendType = parsedSendType;
                        }
                    }

                    if (selectionCriteria.TryGetValue("ScheduledAt", out var scheduledAtElement))
                    {
                        if (DateTime.TryParse(scheduledAtElement.GetString(), out var parsedDate))
                        {
                            scheduledAt = parsedDate;
                        }
                    }

                    if (selectionCriteria.TryGetValue("PreventDuplicate", out var preventDuplicateElement))
                    {
                        preventDuplicate = preventDuplicateElement.GetBoolean();
                    }

                    if (selectionCriteria.TryGetValue("DuplicatePreventionHours", out var hoursElement))
                    {
                        duplicatePreventionHours = hoursElement.GetInt32();
                    }

                    if (selectionCriteria.TryGetValue("SendToSpecificTags", out var sendToTagsElement))
                    {
                        sendToSpecificTags = sendToTagsElement.GetBoolean();
                    }

                    if (selectionCriteria.TryGetValue("SelectedTagIds", out var tagIdsElement))
                    {
                        var tagIdsStr = tagIdsElement.GetString();
                        if (!string.IsNullOrEmpty(tagIdsStr))
                        {
                            selectedTagIds = JsonSerializer.Deserialize<List<int>>(tagIdsStr);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error reading campaign settings from Session - using defaults - MessageId: {MessageId}", messageId);
                }

                var recipientsCount = recipients.Count;
                var partsCount = message.PartsCount;
                var totalCost = recipientsCount * partsCount * _costPerPart;

                // بررسی موجودی کیف پول
                var user = await _userRepository.GetByIdAsync(userId);
                var disableWalletCheck = IsWalletCheckDisabled();
                var walletStatus = disableWalletCheck || (user?.WalletBalance >= totalCost) ? "Sufficient" : "Insufficient";

                var summary = new CampaignSummaryDto
                {
                    SendType = sendType,
                    ScheduledAt = scheduledAt,
                    PreventDuplicate = preventDuplicate,
                    DuplicatePreventionHours = duplicatePreventionHours,
                    SendToSpecificTags = sendToSpecificTags,
                    SelectedTagIds = selectedTagIds,
                    RecipientsCount = recipientsCount,
                    PartsCount = partsCount,
                    CostPerPart = _costPerPart,
                    EstimatedTotalCost = totalCost,
                    WalletStatus = walletStatus,
                    WalletBalance = user?.WalletBalance ?? 0
                };

                return ApiResponse<CampaignSummaryDto>.CreateSuccess(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting campaign summary for message: {MessageId}", messageId);
                return ApiResponse<CampaignSummaryDto>.InternalServerError("خطا در دریافت خلاصه کمپین");
            }
        }

        public async Task<ApiResponse<CampaignSummaryDto>> CalculateCampaignSummaryAsync(int userId, int messageId, CreateCampaignDto campaignDto, string? idempotencyKey = null)
        {
            try
            {
                var message = await _messageRepository.GetByIdAsync(messageId);
                if (message == null || message.UserId != userId)
                {
                    return ApiResponse<CampaignSummaryDto>.NotFound("پیام یافت نشد");
                }

                List<RecipientItemDto> recipients;

                // تلاش برای خواندن گیرندگان از Session (برای پیام عادی)
                var session = await _sessionRepository.GetActiveSessionByMessageIdAsync(messageId, userId);
                if (session != null && !string.IsNullOrEmpty(session.RecipientsJson))
                {
                    // خواندن گیرندگان از Session
                    try
                    {
                        recipients = JsonSerializer.Deserialize<List<RecipientItemDto>>(session.RecipientsJson) 
                            ?? new List<RecipientItemDto>();
                        _logger.LogInformation("Recipients loaded from Session - SessionId: {SessionId}, MessageId: {MessageId}, Count: {Count}", 
                            session.Id, messageId, recipients.Count);
                }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error deserializing recipients from Session - SessionId: {SessionId}", session.Id);
                        recipients = new List<RecipientItemDto>();
                    }
                }
                else
                {
                    // برای پیام عادی، Session باید وجود داشته باشد
                    return ApiResponse<CampaignSummaryDto>.BadRequest(
                        "هیچ گیرنده‌ای برای این پیام انتخاب نشده است. لطفاً ابتدا گیرندگان را انتخاب کنید.");
                }

                // فیلترها در calculate-summary اعمال نمی‌شوند (مشکل 5.1)
                // فیلترها فقط در confirm-and-send اعمال می‌شوند تا هزینه دقیق محاسبه شود
                // این باعث می‌شود که هزینه در calculate-summary تخمینی باشد و در confirm-and-send دقیق شود

                var recipientsCount = recipients.Count;
                var partsCount = message.PartsCount;
                var totalCost = recipientsCount * partsCount * _costPerPart;

                // بررسی موجودی کیف پول
                var user = await _userRepository.GetByIdAsync(userId);
                var disableWalletCheck = IsWalletCheckDisabled();
                var walletStatus = disableWalletCheck || (user?.WalletBalance >= totalCost) ? "Sufficient" : "Insufficient";

                var summary = new CampaignSummaryDto
                {
                    SendType = campaignDto.SendType,
                    ScheduledAt = campaignDto.ScheduledAt,
                    PreventDuplicate = campaignDto.PreventDuplicate,
                    DuplicatePreventionHours = campaignDto.DuplicatePreventionHours,
                    SendToSpecificTags = campaignDto.SendToSpecificTags,
                    SelectedTagIds = campaignDto.SelectedTagIds,
                    RecipientsCount = recipientsCount,
                    PartsCount = partsCount,
                    CostPerPart = _costPerPart,
                    EstimatedTotalCost = totalCost,
                    WalletStatus = walletStatus,
                    WalletBalance = user?.WalletBalance ?? 0
                };

                // ذخیره تنظیمات در Session برای استفاده در confirm-and-send
                if (session != null)
                {
                    try
                    {
                        // استفاده از Transaction برای جلوگیری از Race Condition
                        using var transaction = await _context.Database.BeginTransactionAsync();
                        try
                        {
                            // خواندن مجدد Session با Lock
                            var sessionWithLock = await _context.MessageSessions
                                .FromSqlRaw(
                                    "SELECT * FROM MessageSessions WITH (UPDLOCK, ROWLOCK) WHERE Id = {0}",
                                    session.Id)
                                .FirstOrDefaultAsync();

                            if (sessionWithLock != null)
                            {
                                // Merge کردن تنظیمات (مشکل 5.2) - حفظ تنظیمات قبلی و فقط به‌روزرسانی فیلدهای جدید
                                var selectionCriteria = JsonSerializer.Deserialize<Dictionary<string, object>>(sessionWithLock.SelectionCriteria ?? "{}") ?? new Dictionary<string, object>();
                                
                                // فقط فیلدهای ارسال شده را به‌روزرسانی می‌کنیم (Merge)
                                // این باعث می‌شود تنظیمات قبلی حفظ شوند
                                selectionCriteria["SendType"] = campaignDto.SendType.ToString();
                                if (campaignDto.ScheduledAt.HasValue)
                                {
                                    selectionCriteria["ScheduledAt"] = campaignDto.ScheduledAt.Value.ToString("O");
                                }
                                else if (campaignDto.SendType == CampaignSendType.Quick)
                                {
                                    // اگر Quick است، ScheduledAt را حذف می‌کنیم
                                    selectionCriteria.Remove("ScheduledAt");
                                }
                                
                                selectionCriteria["PreventDuplicate"] = campaignDto.PreventDuplicate;
                                selectionCriteria["DuplicatePreventionHours"] = campaignDto.DuplicatePreventionHours;
                                selectionCriteria["SendToSpecificTags"] = campaignDto.SendToSpecificTags;
                                
                                if (campaignDto.SelectedTagIds != null && campaignDto.SelectedTagIds.Any())
                                {
                                    selectionCriteria["SelectedTagIds"] = JsonSerializer.Serialize(campaignDto.SelectedTagIds);
                                }
                                else if (campaignDto.SendToSpecificTags == false)
                                {
                                    // اگر SendToSpecificTags false است، SelectedTagIds را حذف می‌کنیم
                                    selectionCriteria.Remove("SelectedTagIds");
                                }
                                
                                selectionCriteria["ForceSend"] = campaignDto.ForceSend;
                                
                                sessionWithLock.SelectionCriteria = JsonSerializer.Serialize(selectionCriteria);
                                sessionWithLock.UpdatedAt = DateTime.UtcNow;
                                await _sessionRepository.UpdateAsync(sessionWithLock);
                                await transaction.CommitAsync();
                                
                                _logger.LogInformation("Campaign settings merged to Session - SessionId: {SessionId}, MessageId: {MessageId}, SendType: {SendType}", 
                                    sessionWithLock.Id, messageId, campaignDto.SendType);
                            }
                        }
                        catch (DbUpdateConcurrencyException ex)
                        {
                            await transaction.RollbackAsync();
                            _logger.LogWarning(ex, "Concurrency conflict while saving campaign settings - MessageId: {MessageId}", messageId);
                            // خطا در ذخیره تنظیمات مانع از برگرداندن خلاصه نمی‌شود
                        }
                        catch (Exception)
                        {
                            await transaction.RollbackAsync();
                            throw;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error saving campaign settings to Session - MessageId: {MessageId}", messageId);
                        // خطا در ذخیره تنظیمات مانع از برگرداندن خلاصه نمی‌شود
                    }
                }

                // ارسال SMS (پیش‌فرض همیشه ارسال می‌شود)
                _logger.LogInformation("Starting send process for MessageId: {MessageId}", messageId);
                
                try
                {
                    // استفاده از منطق ConfirmAndSendMessageAsync برای ارسال
                    var sendResult = await ConfirmAndSendMessageAsync(userId, messageId, idempotencyKey);
                    
                    if (sendResult.Success && sendResult.Data != null)
                    {
                        // به‌روزرسانی خلاصه با نتایج ارسال
                        summary.AutoSent = true;
                        summary.SentCount = sendResult.Data.SentCount;
                        summary.FailedCount = sendResult.Data.FailedCount;
                        summary.ActualCost = sendResult.Data.TotalCost;
                        
                        _logger.LogInformation("Send completed - MessageId: {MessageId}, Sent: {SentCount}, Failed: {FailedCount}", 
                            messageId, sendResult.Data.SentCount, sendResult.Data.FailedCount);
                    }
                    else
                    {
                        // اگر ارسال ناموفق بود، خلاصه را بدون نتایج برمی‌گردانیم
                        summary.AutoSent = false;
                        _logger.LogWarning("Send failed - MessageId: {MessageId}, Error: {Error}", 
                            messageId, sendResult.Message);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during send - MessageId: {MessageId}", messageId);
                    summary.AutoSent = false;
                    // خلاصه را بدون نتایج برمی‌گردانیم
                }

                            return ApiResponse<CampaignSummaryDto>.CreateSuccess(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating campaign summary for message: {MessageId}", messageId);
                return ApiResponse<CampaignSummaryDto>.InternalServerError("خطا در محاسبه خلاصه کمپین");
            }
        }

        public async Task<ApiResponse<DirectSendResultDto>> ConfirmAndSendMessageAsync(int userId, int messageId, string? idempotencyKey = null)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _logger.LogInformation("=== شروع تأیید و ارسال پیام ===");
                _logger.LogInformation("MessageId: {MessageId}, UserId: {UserId}, IdempotencyKey: {IdempotencyKey}", 
                    messageId, userId, idempotencyKey ?? "None");

                // بررسی Idempotency (مشکل 7.2) - اگر IdempotencyKey وجود دارد و قبلاً استفاده شده، نتیجه قبلی را برمی‌گردانیم
                if (!string.IsNullOrWhiteSpace(idempotencyKey))
                {
                    // بررسی اینکه آیا این IdempotencyKey قبلاً استفاده شده است
                    // این بررسی باید در یک جدول جداگانه یا در Session ذخیره شود
                    // برای سادگی، از Session استفاده می‌کنیم - اگر Session استفاده شده و IdempotencyKey یکسان است، نتیجه قبلی را برمی‌گردانیم
                    var existingSession = await _sessionRepository.GetActiveSessionByMessageIdAsync(messageId, userId);
                    if (existingSession != null && existingSession.IsUsed)
                    {
                        // بررسی اینکه آیا IdempotencyKey در SelectionCriteria ذخیره شده است
                        try
                        {
                            var selectionCriteria = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(existingSession.SelectionCriteria ?? "{}") ?? new Dictionary<string, JsonElement>();
                            if (selectionCriteria.TryGetValue("IdempotencyKey", out var storedKeyElement))
                            {
                                var storedKey = storedKeyElement.GetString();
                                if (storedKey == idempotencyKey)
                                {
                                    _logger.LogInformation("Idempotency Key detected - returning previous result - MessageId: {MessageId}, Key: {Key}", 
                                        messageId, idempotencyKey);
                                    await transaction.RollbackAsync();
                                    // در اینجا باید نتیجه قبلی را برگردانیم، اما چون نتیجه را ذخیره نکرده‌ایم، خطا برمی‌گردانیم
                                    // برای پیاده‌سازی کامل، باید نتیجه را در Session یا جدول جداگانه ذخیره کنیم
                                    return ApiResponse<DirectSendResultDto>.BadRequest(
                                        "این درخواست قبلاً پردازش شده است. لطفاً از Idempotency Key متفاوتی استفاده کنید.");
                                }
                            }
                        }
                        catch
                        {
                            // در صورت خطا در خواندن، ادامه می‌دهیم
                        }
                    }
                }

                // بررسی وجود پیام
                var message = await _messageRepository.GetByIdAsync(messageId);
                if (message == null || message.UserId != userId)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<DirectSendResultDto>.NotFound("پیام یافت نشد");
                }

                // خواندن Session با Lock برای جلوگیری از Race Condition
                // استفاده از Raw SQL برای SELECT FOR UPDATE (Pessimistic Lock)
                // ابتدا بدون شرط IsUsed بررسی می‌کنیم تا ببینیم Session وجود دارد یا نه
                var now = DateTime.UtcNow;
                _logger.LogInformation("Looking for Session - MessageId: {MessageId}, UserId: {UserId}, Now (UTC): {Now}", 
                    messageId, userId, now);
                
                var session = await _context.MessageSessions
                    .FromSqlRaw(
                        "SELECT * FROM MessageSessions WITH (UPDLOCK, ROWLOCK) WHERE MessageId = {0} AND UserId = {1} AND IsDeleted = 0 AND (ExpiresAt IS NULL OR ExpiresAt > {2})",
                        messageId, userId, now)
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefaultAsync();

                if (session == null)
                {
                    // بررسی دقیق‌تر: آیا Session وجود دارد اما منقضی شده یا حذف شده است؟
                    var anySession = await _sessionRepository.GetByMessageIdAsync(messageId, userId);
                    if (anySession != null)
                    {
                        _logger.LogWarning("Session found but not active - SessionId: {SessionId}, IsDeleted: {IsDeleted}, IsUsed: {IsUsed}, ExpiresAt: {ExpiresAt}, Now: {Now}, HasRecipientsJson: {HasRecipientsJson}", 
                            anySession.Id, anySession.IsDeleted, anySession.IsUsed, anySession.ExpiresAt, now, !string.IsNullOrEmpty(anySession.RecipientsJson));
                        
                        if (anySession.IsDeleted)
                        {
                            await transaction.RollbackAsync();
                            return ApiResponse<DirectSendResultDto>.BadRequest(
                                "Session حذف شده است. لطفاً گیرندگان را دوباره انتخاب کنید.");
                        }
                        
                        if (anySession.ExpiresAt.HasValue && anySession.ExpiresAt.Value <= now)
                        {
                            await transaction.RollbackAsync();
                            return ApiResponse<DirectSendResultDto>.BadRequest(
                                $"Session منقضی شده است. تاریخ انقضا: {anySession.ExpiresAt.Value:yyyy-MM-dd HH:mm:ss} UTC");
                        }
                            }
                            else
                            {
                        _logger.LogWarning("No Session found at all - MessageId: {MessageId}, UserId: {UserId}", 
                            messageId, userId);
                    }
                    
                    await transaction.RollbackAsync();
                    return ApiResponse<DirectSendResultDto>.BadRequest(
                        "هیچ Session فعالی برای این پیام یافت نشد. لطفاً ابتدا گیرندگان را انتخاب کنید.");
                }

                _logger.LogInformation("Session found - SessionId: {SessionId}, IsUsed: {IsUsed}, HasRecipientsJson: {HasRecipientsJson}, RecipientsJsonLength: {Length}, ExpiresAt: {ExpiresAt}", 
                    session.Id, session.IsUsed, !string.IsNullOrEmpty(session.RecipientsJson), session.RecipientsJson?.Length ?? 0, session.ExpiresAt);

                // بررسی IsUsed بعد از Lock
                if (session.IsUsed)
                {
                    await transaction.RollbackAsync();
                    _logger.LogWarning("Session already used - SessionId: {SessionId}, MessageId: {MessageId}", 
                        session.Id, messageId);
                    return ApiResponse<DirectSendResultDto>.BadRequest(
                        "این Session قبلاً استفاده شده است. لطفاً گیرندگان را دوباره انتخاب کنید.");
                }

                if (string.IsNullOrEmpty(session.RecipientsJson))
                {
                    await transaction.RollbackAsync();
                    _logger.LogWarning("Session has empty RecipientsJson - SessionId: {SessionId}, MessageId: {MessageId}, SelectionCriteria: {SelectionCriteria}", 
                        session.Id, messageId, session.SelectionCriteria);
                    return ApiResponse<DirectSendResultDto>.BadRequest(
                        "لیست گیرندگان در Session خالی است. لطفاً ابتدا گیرندگان را انتخاب کنید.");
                }

                // خواندن تنظیمات از Session
                CampaignSendType sendType = CampaignSendType.Quick;
                DateTime? scheduledAt = null;
                bool preventDuplicate = false;
                int duplicatePreventionHours = 24;
                bool sendToSpecificTags = false;
                List<int>? selectedTagIds = null;
                bool forceSend = false;

                try
                {
                    var selectionCriteria = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(session.SelectionCriteria ?? "{}") ?? new Dictionary<string, JsonElement>();
                    
                    // خواندن SendType
                    if (selectionCriteria.TryGetValue("SendType", out var sendTypeElement))
                    {
                        if (Enum.TryParse<CampaignSendType>(sendTypeElement.GetString(), out var parsedSendType))
                        {
                            sendType = parsedSendType;
                        }
                    }

                    // خواندن ScheduledAt
                    if (selectionCriteria.TryGetValue("ScheduledAt", out var scheduledAtElement))
                    {
                        var scheduledAtStr = scheduledAtElement.GetString();
                        if (!string.IsNullOrEmpty(scheduledAtStr) && DateTime.TryParse(scheduledAtStr, out var parsedScheduledAt))
                        {
                            scheduledAt = parsedScheduledAt;
                        }
                    }

                    // خواندن PreventDuplicate
                    if (selectionCriteria.TryGetValue("PreventDuplicate", out var preventDuplicateElement))
                    {
                        preventDuplicate = preventDuplicateElement.GetBoolean();
                    }

                    // خواندن DuplicatePreventionHours
                    if (selectionCriteria.TryGetValue("DuplicatePreventionHours", out var hoursElement))
                    {
                        duplicatePreventionHours = hoursElement.GetInt32();
                    }

                    // خواندن SendToSpecificTags
                    if (selectionCriteria.TryGetValue("SendToSpecificTags", out var sendToTagsElement))
                    {
                        sendToSpecificTags = sendToTagsElement.GetBoolean();
                    }

                    // خواندن SelectedTagIds
                    if (selectionCriteria.TryGetValue("SelectedTagIds", out var tagIdsElement))
                    {
                        var tagIdsStr = tagIdsElement.GetString();
                        if (!string.IsNullOrEmpty(tagIdsStr))
                        {
                            selectedTagIds = JsonSerializer.Deserialize<List<int>>(tagIdsStr);
                        }
                    }

                    // خواندن ForceSend
                    if (selectionCriteria.TryGetValue("ForceSend", out var forceSendElement))
                    {
                        forceSend = forceSendElement.GetBoolean();
                    }

                    _logger.LogInformation("Settings loaded from Session - SendType: {SendType}, PreventDuplicate: {PreventDuplicate}, SendToSpecificTags: {SendToSpecificTags}", 
                        sendType, preventDuplicate, sendToSpecificTags);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error reading campaign settings from Session - using defaults - MessageId: {MessageId}", messageId);
                    // در صورت خطا، از مقادیر پیش‌فرض استفاده می‌کنیم
                }

                // ذخیره IdempotencyKey در Session (اگر وجود دارد) - قبل از ارسال
                if (!string.IsNullOrWhiteSpace(idempotencyKey))
                    {
                        try
                        {
                        var selectionCriteria = JsonSerializer.Deserialize<Dictionary<string, object>>(session.SelectionCriteria ?? "{}") ?? new Dictionary<string, object>();
                        selectionCriteria["IdempotencyKey"] = idempotencyKey;
                        session.SelectionCriteria = JsonSerializer.Serialize(selectionCriteria);
                        await _sessionRepository.UpdateAsync(session);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("IdempotencyKey saved to Session - MessageId: {MessageId}, Key: {Key}", messageId, idempotencyKey);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error saving IdempotencyKey to Session - MessageId: {MessageId}", messageId);
                    }
                }

                // تبدیل تنظیمات به SendDirectMessageDto
                            var sendDto = new SendDirectMessageDto
                            {
                    SendType = sendType,
                    ScheduledAt = scheduledAt,
                    PreventDuplicate = preventDuplicate,
                    DuplicatePreventionHours = duplicatePreventionHours,
                    SendToSpecificTags = sendToSpecificTags,
                    SelectedTagIds = selectedTagIds
                };

                // برای Scheduled، باید زمان در آینده باشد
                if (sendType == CampaignSendType.Scheduled)
                {
                    if (!scheduledAt.HasValue)
                    {
                        return ApiResponse<DirectSendResultDto>.BadRequest(
                            "برای ارسال زمان‌دار، ابتدا باید از calculate-summary استفاده کنید و تنظیمات را ذخیره کنید.");
                    }

                    // تبدیل به UTC برای مقایسه
                    var scheduledAtValue = scheduledAt.Value;
                    DateTime scheduledAtUtc;
                    
                    if (scheduledAtValue.Kind == DateTimeKind.Unspecified)
                    {
                        scheduledAtUtc = DateTime.SpecifyKind(scheduledAtValue, DateTimeKind.Utc);
                    }
                    else if (scheduledAtValue.Kind == DateTimeKind.Local)
                    {
                        scheduledAtUtc = scheduledAtValue.ToUniversalTime();
                            }
                            else
                            {
                        scheduledAtUtc = scheduledAtValue;
                    }
                    
                    var nowUtc = DateTime.UtcNow;
                    
                    // بررسی اینکه زمان در آینده باشد
                    if (scheduledAtUtc <= nowUtc)
                    {
                        // اگر ForceSend فعال باشد، زمان را به 5 ثانیه بعد تنظیم می‌کنیم (فقط برای تست)
                        if (forceSend)
                        {
                            scheduledAtUtc = nowUtc.AddSeconds(5);
                            _logger.LogInformation("ForceSend enabled - Setting scheduled time to 5 seconds from now for testing - MessageId: {MessageId}", messageId);
                            sendDto.ScheduledAt = scheduledAtUtc;
                        }
                        else
                        {
                            return ApiResponse<DirectSendResultDto>.BadRequest("زمان ارسال باید در آینده باشد");
                        }
                    }
                    else
                    {
                        sendDto.ScheduledAt = scheduledAtUtc;
                    }

                    // برای Scheduled، فعلاً فقط Session را به‌روزرسانی می‌کنیم و ارسال را به Background Service می‌سپاریم
                    // در حال حاضر SendDirectMessageAsync برای Scheduled پیام خطا برمی‌گرداند
                    return ApiResponse<DirectSendResultDto>.BadRequest(
                        "ارسال زمان‌بندی شده در حال حاضر پشتیبانی نمی‌شود. لطفاً از ارسال فوری استفاده کنید.");
                }

                // برای Quick: ارسال فوری
                if (sendType == CampaignSendType.Quick)
                {
                    // علامت‌گذاری Session به عنوان استفاده شده (قبل از ارسال برای جلوگیری از استفاده مجدد)
                    session.IsUsed = true;
                    session.UpdatedAt = DateTime.UtcNow;
                    await _sessionRepository.UpdateAsync(session);
                    await _context.SaveChangesAsync();

                    // Commit Transaction برای Session
                    await transaction.CommitAsync();

                    // ارسال پیام‌ها (خارج از Transaction برای جلوگیری از Timeout)
                    // پاس دادن Session به SendDirectMessageAsync تا نیازی به خواندن مجدد نباشد
                    var sendResult = await SendDirectMessageAsync(userId, messageId, sendDto, session);
                    return sendResult;
                }

                await transaction.RollbackAsync();
                return ApiResponse<DirectSendResultDto>.BadRequest("نوع ارسال نامعتبر است");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(ex, "Concurrency conflict detected - MessageId: {MessageId}, UserId: {UserId}", messageId, userId);
                return ApiResponse<DirectSendResultDto>.BadRequest(
                    "این Session در حال استفاده توسط درخواست دیگری است. لطفاً دوباره تلاش کنید.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error confirming and sending message - MessageId: {MessageId}", messageId);
                return ApiResponse<DirectSendResultDto>.InternalServerError("خطا در تأیید و ارسال پیام");
            }
        }

        public async Task<ApiResponse<CampaignResponseDto>> CreateCampaignAsync(int userId, CreateCampaignDto createDto)
        {
            try
            {
                var message = await _messageRepository.GetByIdAsync(createDto.MessageId);
                if (message == null || message.UserId != userId)
                {
                    return ApiResponse<CampaignResponseDto>.NotFound("پیام یافت نشد");
                }

                // برای پیام عادی، گیرندگان از Session خوانده می‌شوند
                // این متد فقط برای پیام خودکار استفاده می‌شود که متد مجزایی دارد
                return ApiResponse<CampaignResponseDto>.BadRequest(
                    "این متد برای پیام عادی استفاده نمی‌شود. برای پیام عادی از متد calculate-summary استفاده کنید که به صورت خودکار پیام‌ها را ارسال می‌کند.");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating campaign for user: {UserId}", userId);
                return ApiResponse<CampaignResponseDto>.InternalServerError("خطا در ذخیره‌سازی کمپین. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating campaign for user: {UserId}", userId);
                return ApiResponse<CampaignResponseDto>.InternalServerError("خطای غیرمنتظره در ایجاد کمپین");
            }
        }

        public async Task<ApiResponse<CampaignResponseDto>> GetCampaignByIdAsync(int campaignId, int userId)
        {
            try
            {
                var campaign = await _campaignRepository.GetByIdWithMessageAsync(campaignId);

                if (campaign == null)
                {
                    return ApiResponse<CampaignResponseDto>.NotFound("کمپین یافت نشد");
                }

                if (campaign.UserId != userId)
                {
                    return ApiResponse<CampaignResponseDto>.Forbidden("شما مجاز به دسترسی به این کمپین نیستید");
                }

                return ApiResponse<CampaignResponseDto>.CreateSuccess(
                    await MapToCampaignResponseDtoAsync(campaign)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting campaign: {CampaignId}", campaignId);
                return ApiResponse<CampaignResponseDto>.InternalServerError("خطا در دریافت کمپین");
            }
        }

        public async Task<ApiResponse<CampaignListResponseDto>> GetCampaignsAsync(int userId, int pageNumber = 1, int pageSize = 10, string? status = null)
        {
            try
            {
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                IEnumerable<MessageCampaign> campaigns;
                if (!string.IsNullOrWhiteSpace(status))
                {
                    campaigns = await _campaignRepository.GetByUserIdAndStatusAsync(userId, status);
                }
                else
                {
                    campaigns = await _campaignRepository.GetByUserIdAsync(userId);
                }

                var campaignsList = campaigns.ToList();
                var totalCount = campaignsList.Count;
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var pagedCampaigns = campaignsList
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var campaignDtos = new List<CampaignResponseDto>();
                foreach (var campaign in pagedCampaigns)
                {
                    campaignDtos.Add(await MapToCampaignResponseDtoAsync(campaign));
                }

                var response = new CampaignListResponseDto
                {
                    Campaigns = campaignDtos,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                return ApiResponse<CampaignListResponseDto>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting campaigns for user: {UserId}", userId);
                return ApiResponse<CampaignListResponseDto>.InternalServerError("خطا در دریافت لیست کمپین‌ها");
            }
        }

        public async Task<ApiResponse<bool>> ConfirmAndSendCampaignAsync(int campaignId, int userId, bool bypassAdminApproval = false)
        {
            try
            {
                var campaign = await _campaignRepository.GetByIdWithRecipientsAsync(campaignId);

                if (campaign == null)
                {
                    return ApiResponse<bool>.NotFound("کمپین یافت نشد");
                }

                if (campaign.UserId != userId)
                {
                    return ApiResponse<bool>.Forbidden("شما مجاز به ارسال این کمپین نیستید");
                }

                if (campaign.Status != "Draft" && campaign.Status != "Pending" && campaign.Status != "PendingApproval")
                {
                    return ApiResponse<bool>.BadRequest("این کمپین قابل ارسال نیست");
                }

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    return ApiResponse<bool>.NotFound("کاربر یافت نشد");
                }

                var message = await _messageRepository.GetByIdAsync(campaign.MessageId);
                if (message == null)
                {
                    return ApiResponse<bool>.NotFound("پیام مربوط به کمپین یافت نشد");
                }

                if (!bypassAdminApproval)
                {
                    var templateError = await ValidateTemplateApprovalForMessageAsync(message);
                    if (templateError != null)
                        return ApiResponse<bool>.BadRequest(templateError);

                    if (campaign.AdminApprovalStatus != AdminApprovalStatuses.Approved)
                    {
                        campaign.AdminApprovalStatus = AdminApprovalStatuses.Pending;
                        campaign.Status = "PendingApproval";
                        campaign.UpdatedAt = DateTime.UtcNow;
                        await _campaignRepository.UpdateAsync(campaign);
                        await UpsertCampaignApprovalRequestAsync(campaign, message);

                        return ApiResponse<bool>.CreateSuccess(
                            true,
                            "درخواست ارسال در صف تأیید ادمین قرار گرفت",
                            202);
                    }
                }

                // بررسی موجودی کیف پول
                // غیرفعال شده - دیگر کیف پول چک نمی‌شود
                /*
                if (user.WalletBalance < campaign.EstimatedTotalCost)
                {
                    return ApiResponse<bool>.BadRequest("موجودی کیف پول کافی نیست");
                }
                */

                campaign.Status = "Sending";
                campaign.UpdatedAt = DateTime.UtcNow;
                await _campaignRepository.UpdateAsync(campaign);

                // ارسال پیام‌ها به گیرندگان
                int sentCount = 0;
                int failedCount = 0;
                decimal actualCost = 0;

                foreach (var recipient in campaign.Recipients.Where(r => r.Status == "Pending"))
                {
                    try
                    {
                        // آماده‌سازی متن پیام (شخصی‌سازی در صورت نیاز)
                        string messageContent = message.Content;

                            // اگر پیام placeholder دارد و recipient ContactId دارد، شخصی‌سازی می‌کنیم
                        if (message.IsPersonalized && recipient.ContactId.HasValue)
                        {
                            var contact = await _contactRepository.GetByIdAsync(recipient.ContactId.Value);
                            if (contact != null)
                            {
                                // شخصی‌سازی پیام با اطلاعات مخاطب
                                messageContent = await PersonalizeMessageWithContactAsync(message.Content, contact);
                                recipient.PersonalizedContent = messageContent;
                            }
                        }
                        else if (message.IsPersonalized && !string.IsNullOrEmpty(recipient.PersonalizedContent))
                        {
                            // اگر قبلاً شخصی‌سازی شده، از آن استفاده می‌کنیم
                            messageContent = recipient.PersonalizedContent;
                        }

                        // اضافه کردن 'لغو11' در انتهای پیامک (الزام API)
                        if (!messageContent.TrimEnd().EndsWith("لغو11"))
                        {
                            messageContent = $"{messageContent.TrimEnd()}\nلغو11";
                        }

                        // محاسبه دقیق تعداد پارت‌ها برای پیام نهایی (با در نظر گیری شخصی‌سازی و 'لغو11')
                        int actualPartsCount;
                        try
                        {
                            actualPartsCount = SmsPartsCalculator.CalculateParts(messageContent);
                        }
                        catch (ArgumentException ex)
                        {
                            // پیام بیش از 10 صفحه است
                            recipient.Status = "Failed";
                            recipient.ErrorMessage = ControlledErrorHelper.SendFailed;
                            recipient.RetryCount++;
                            failedCount++;
                            _logger.LogWarning("Message exceeds max pages for {Mobile} - Campaign: {CampaignId}, Error: {Error}", 
                                recipient.MobileNumber, campaignId, ex.Message);
                            continue;
                        }

                        // ارسال پیامک
                        var smsRequest = new DTOs.Sms.SendSmsRequestDto
                        {
                            Mobile = recipient.MobileNumber,
                            Message = messageContent
                        };

                        var smsResult = await _smsService.SendSmsAsync(smsRequest);

                        // Sid > 0 یعنی پیام ارسال شده (حتی اگر Status = 0 باشد)
                        bool isSuccess = smsResult.Success && smsResult.Data != null && 
                            (smsResult.Data.Sid > 0 || smsResult.Data.Status > 0);
                        
                        if (isSuccess)
                        {
                            // ارسال موفق
                            recipient.Status = "Sent";
                            recipient.SentAt = DateTime.UtcNow;
                            recipient.SmsServiceId = smsResult.Data!.Sid.ToString();
                            recipient.ErrorMessage = null;
                            sentCount++;
                            actualCost += campaign.CostPerPart * actualPartsCount;

                            await _deliveryTracking.TrackSuccessfulSendAsync(new SmsDeliveryTrackRequestDto
                            {
                                UserId = userId,
                                SourceModule = SmsSourceModules.MessageCampaign,
                                SourceEntityId = campaignId,
                                SourceEntityLabel = campaign.Title ?? $"کمپین #{campaignId}",
                                Mobile = recipient.MobileNumber,
                                Sid = smsResult.Data.Sid,
                                SentAt = DateTime.UtcNow
                            });
                            
                            _logger.LogInformation("SMS sent successfully to {Mobile} - Campaign: {CampaignId}, Sid: {Sid}", 
                                recipient.MobileNumber, campaignId, smsResult.Data.Sid);
                        }
                        else
                        {
                            // ارسال ناموفق
                            recipient.Status = "Failed";
                            recipient.ErrorMessage = ControlledErrorHelper.SendFailed;
                            recipient.RetryCount++;
                            failedCount++;
                            
                            _logger.LogWarning("SMS send failed to {Mobile} - Campaign: {CampaignId}, Error: {Error}", 
                                recipient.MobileNumber, campaignId, recipient.ErrorMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        // خطا در ارسال
                        recipient.Status = "Failed";
                        recipient.ErrorMessage = ControlledErrorHelper.SendFailed;
                        recipient.RetryCount++;
                        failedCount++;
                        
                        _logger.LogError(ex, "Error sending SMS to {Mobile} in campaign {CampaignId}", 
                            recipient.MobileNumber, campaignId);
                    }

                    // به‌روزرسانی recipient در دیتابیس
                    _context.MessageRecipients.Update(recipient);
                }

                await _context.SaveChangesAsync();

                // به‌روزرسانی وضعیت کمپین
                campaign.Status = sentCount > 0 ? "Sent" : "Failed";
                campaign.SentAt = DateTime.UtcNow;
                campaign.SentCount = sentCount;
                campaign.FailedCount = failedCount;
                campaign.ActualTotalCost = actualCost;
                campaign.UpdatedAt = DateTime.UtcNow;
                
                if (failedCount > 0 && sentCount == 0)
                {
                    campaign.ErrorMessage = "همه پیام‌ها با خطا مواجه شدند";
                }
                else if (failedCount > 0)
                {
                    campaign.ErrorMessage = $"{failedCount} پیام با خطا مواجه شد";
                }

                await _campaignRepository.UpdateAsync(campaign);

                // کسر از کیف پول (فقط برای پیام‌های موفق)
                // غیرفعال شده - دیگر از کیف پول کسر نمی‌شود
                /*
                user.WalletBalance -= actualCost;
                await _userRepository.UpdateAsync(user);
                */

                _logger.LogInformation("Campaign {CampaignId} completed - Sent: {SentCount}, Failed: {FailedCount}, Actual Cost: {ActualCost}", 
                    campaignId, sentCount, failedCount, actualCost);

                var successMessage = sentCount > 0 
                    ? $"پیام‌ها با موفقیت ارسال شد ({sentCount} ارسال موفق، {failedCount} ناموفق)"
                    : "هیچ پیامی ارسال نشد";

                return ApiResponse<bool>.CreateSuccess(true, successMessage);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error sending campaign: {CampaignId}", campaignId);
                return ApiResponse<bool>.InternalServerError("خطا در ذخیره‌سازی وضعیت ارسال کمپین. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error sending campaign: {CampaignId}", campaignId);
                return ApiResponse<bool>.InternalServerError("خطای غیرمنتظره در ارسال کمپین");
            }
        }

        public async Task<ApiResponse<bool>> CancelCampaignAsync(int campaignId, int userId)
        {
            try
            {
                var campaign = await _campaignRepository.GetByIdAsync(campaignId);

                if (campaign == null)
                {
                    return ApiResponse<bool>.NotFound("کمپین یافت نشد");
                }

                if (campaign.UserId != userId)
                {
                    return ApiResponse<bool>.Forbidden("شما مجاز به لغو این کمپین نیستید");
                }

                if (campaign.Status == "Sent" || campaign.Status == "Cancelled")
                {
                    return ApiResponse<bool>.BadRequest("این کمپین قابل لغو نیست");
                }

                campaign.Status = "Cancelled";
                campaign.UpdatedAt = DateTime.UtcNow;
                await _campaignRepository.UpdateAsync(campaign);

                _logger.LogInformation("Campaign cancelled successfully with ID: {CampaignId}", campaignId);

                return ApiResponse<bool>.CreateSuccess(true, "کمپین با موفقیت لغو شد");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error cancelling campaign: {CampaignId}", campaignId);
                return ApiResponse<bool>.InternalServerError("خطا در لغو کمپین. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error cancelling campaign: {CampaignId}", campaignId);
                return ApiResponse<bool>.InternalServerError("خطای غیرمنتظره در لغو کمپین");
            }
        }

        /// <summary>
        /// تغییر وضعیت فعال/غیرفعال کمپین
        /// </summary>
        public async Task<ApiResponse<bool>> ToggleCampaignStatusAsync(int campaignId, int userId, bool isActive)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var campaign = await _campaignRepository.GetByIdAsync(campaignId);

                if (campaign == null)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<bool>.NotFound("کمپین یافت نشد");
                }

                if (campaign.UserId != userId)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<bool>.Forbidden("شما مجاز به تغییر وضعیت این کمپین نیستید");
                }

                // نمی‌توان وضعیت کمپین‌های ارسال شده یا لغو شده را تغییر داد
                if (campaign.Status == "Sent" || campaign.Status == "Cancelled")
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<bool>.BadRequest("وضعیت کمپین‌های ارسال شده یا لغو شده قابل تغییر نیست");
                }

                campaign.IsActive = isActive;
                campaign.UpdatedAt = DateTime.UtcNow;
                await _campaignRepository.UpdateAsync(campaign);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Campaign status toggled to {Status} for ID: {CampaignId}", isActive ? "Active" : "Inactive", campaignId);

                return ApiResponse<bool>.CreateSuccess(true, $"کمپین با موفقیت {(isActive ? "فعال" : "غیرفعال")} شد");
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Database error toggling campaign status: {CampaignId}", campaignId);
                return ApiResponse<bool>.InternalServerError("خطا در تغییر وضعیت کمپین. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Unexpected error toggling campaign status: {CampaignId}", campaignId);
                return ApiResponse<bool>.InternalServerError("خطای غیرمنتظره در تغییر وضعیت کمپین");
            }
        }

        #endregion

        #region Template Operations

        public async Task<ApiResponse<TemplateResponseDto>> CreateTemplateAsync(int userId, CreateTemplateDto createDto)
        {
            string? uploadedIconPath = null;

            // آپلود فایل آیکون قبل از شروع Transaction (طبق RULES.txt: اول فایل آپلود شود)
            if (createDto.IconFile != null && _fileUploadService != null)
            {
                try
                {
                    // استفاده از userId به عنوان entityId موقت
                    // فایل در پوشه template/{userId}/icons ذخیره می‌شود
                    uploadedIconPath = await _fileUploadService.UploadFileAsync(
                        createDto.IconFile, 
                        "template", 
                        userId, 
                        "icons"
                    );
                    _logger.LogInformation("📤 Icon file uploaded successfully: {IconPath}", uploadedIconPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error uploading icon file for template");
                    return ApiResponse<TemplateResponseDto>.BadRequest(ControlledErrorHelper.FileUploadFailed);
                }
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // بررسی فیلدهای الزامی (لایه امنیتی اضافی)
                if (string.IsNullOrWhiteSpace(createDto.Name))
                {
                    await transaction.RollbackAsync();
                    // حذف فایل در صورت خطا
                    if (uploadedIconPath != null && _fileUploadService != null)
                    {
                        try
                        {
                            await _fileUploadService.DeleteFileAsync(uploadedIconPath, "template", userId, "icons");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "⚠️ Error deleting uploaded file after validation error");
                        }
                    }
                    return ApiResponse<TemplateResponseDto>.BadRequest("نام قالب نمی‌تواند خالی باشد");
                }

                if (string.IsNullOrWhiteSpace(createDto.Content))
                {
                    await transaction.RollbackAsync();
                    // حذف فایل در صورت خطا
                    if (uploadedIconPath != null && _fileUploadService != null)
                    {
                        try
                        {
                            await _fileUploadService.DeleteFileAsync(uploadedIconPath, "template", userId, "icons");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "⚠️ Error deleting uploaded file after validation error");
                        }
                    }
                    return ApiResponse<TemplateResponseDto>.BadRequest("محتویات قالب نمی‌تواند خالی باشد");
                }

                // بررسی وجود کاربر
                var userExists = await _context.Users.AnyAsync(u => u.Id == userId && !u.IsDeleted);
                if (!userExists)
                {
                    await transaction.RollbackAsync();
                    // حذف فایل در صورت خطا
                    if (uploadedIconPath != null && _fileUploadService != null)
                    {
                        try
                        {
                            await _fileUploadService.DeleteFileAsync(uploadedIconPath, "template", userId, "icons");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "⚠️ Error deleting uploaded file after validation error");
                        }
                    }
                    return ApiResponse<TemplateResponseDto>.NotFound("کاربر یافت نشد");
                }

                // بررسی وجود گروه در صورت ارسال GroupId
                if (createDto.GroupId.HasValue)
                {
                    var groupExists = await _context.TemplateGroups
                        .AnyAsync(tg => tg.Id == createDto.GroupId.Value && 
                                       tg.UserId == userId && 
                                       !tg.IsDeleted);
                    if (!groupExists)
                    {
                        await transaction.RollbackAsync();
                        // حذف فایل در صورت خطا
                        if (uploadedIconPath != null && _fileUploadService != null)
                        {
                            try
                            {
                                await _fileUploadService.DeleteFileAsync(uploadedIconPath, "template", userId, "icons");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "⚠️ Error deleting uploaded file after validation error");
                            }
                        }
                        return ApiResponse<TemplateResponseDto>.BadRequest("گروه قالب مورد نظر یافت نشد");
                    }
                }

                // تعیین مقدار Icon: اولویت با فایل آپلود شده، سپس Icon متنی
                string? iconValue = null;
                if (!string.IsNullOrWhiteSpace(uploadedIconPath))
                {
                    iconValue = uploadedIconPath;
                }
                else if (!string.IsNullOrWhiteSpace(createDto.Icon))
                {
                    iconValue = createDto.Icon.Trim();
                }

                var template = new MessageTemplate
                {
                    UserId = userId,
                    Name = createDto.Name.Trim(),
                    Content = createDto.Content.Trim(),
                    Category = createDto.Category?.Trim(),
                    Description = createDto.Description?.Trim(),
                    Icon = iconValue,
                    GroupId = createDto.GroupId,
                    IsDefault = false, // قالب‌های شخصی کاربر پیش‌فرض نیستند
                    IsActive = true,
                    ApprovalStatus = AdminApprovalStatuses.Pending,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.MessageTemplates.AddAsync(template);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // لود کردن template با Group برای mapping
                var createdTemplate = await _context.MessageTemplates
                    .AsNoTracking()
                    .Include(mt => mt.Group)
                    .FirstOrDefaultAsync(mt => mt.Id == template.Id);

                _logger.LogInformation("✅ Template created successfully with ID: {TemplateId} by user: {UserId}", 
                    template.Id, userId);

                return ApiResponse<TemplateResponseDto>.CreateSuccess(
                    MapToTemplateResponseDto(createdTemplate ?? template),
                    "قالب با موفقیت ایجاد شد",
                    201
                );
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();
                // حذف فایل در صورت خطا
                if (uploadedIconPath != null && _fileUploadService != null)
                {
                    try
                    {
                        await _fileUploadService.DeleteFileAsync(uploadedIconPath, "template", userId, "icons");
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(deleteEx, "⚠️ Error deleting uploaded file after concurrency error");
                    }
                }
                _logger.LogWarning(ex, "⚠️ Concurrency conflict while creating template for user: {UserId}", userId);
                return ApiResponse<TemplateResponseDto>.BadRequest("این قالب در حال استفاده توسط درخواست دیگری است. لطفاً دوباره تلاش کنید");
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                // حذف فایل در صورت خطا
                if (uploadedIconPath != null && _fileUploadService != null)
                {
                    try
                    {
                        await _fileUploadService.DeleteFileAsync(uploadedIconPath, "template", userId, "icons");
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(deleteEx, "⚠️ Error deleting uploaded file after database error");
                    }
                }
                _logger.LogError(ex, "❌ Database error creating template for user: {UserId}", userId);
                return ApiResponse<TemplateResponseDto>.InternalServerError("خطا در ذخیره‌سازی قالب. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                // حذف فایل در صورت خطا
                if (uploadedIconPath != null && _fileUploadService != null)
                {
                    try
                    {
                        await _fileUploadService.DeleteFileAsync(uploadedIconPath, "template", userId, "icons");
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(deleteEx, "⚠️ Error deleting uploaded file after unexpected error");
                    }
                }
                _logger.LogError(ex, "❌ Unexpected error creating template for user: {UserId}", userId);
                return ApiResponse<TemplateResponseDto>.InternalServerError("خطای غیرمنتظره در ایجاد قالب");
            }
        }

        public async Task<ApiResponse<List<TemplateResponseDto>>> GetTemplatesAsync(int userId)
        {
            try
            {
                // ایجاد قالب‌های پیش‌فرض برای کاربر در صورت عدم وجود
                await CreateDefaultTemplatesForUserAsync(userId);

                // دریافت فقط قالب‌های فعال کاربر (هم پیش‌فرض و هم شخصی) با eager loading Group
                var templates = await _context.MessageTemplates
                    .AsNoTracking()
                    .Include(mt => mt.Group)
                    .Where(mt => mt.UserId == userId && mt.IsActive && !mt.IsDeleted)
                    .ToListAsync();
                
                // ترتیب‌دهی: قالب‌های پیش‌فرض اول، سپس بقیه بر اساس تاریخ ایجاد
                var templateDtos = templates
                    .Select(MapToTemplateResponseDto)
                    .OrderByDescending(t => t.IsDefault) // قالب‌های پیش‌فرض اول
                    .ThenByDescending(t => t.CreatedAt) // سپس بر اساس تاریخ ایجاد
                    .ToList();

                _logger.LogInformation("Retrieved {Count} templates for user {UserId}", templateDtos.Count, userId);

                return ApiResponse<List<TemplateResponseDto>>.CreateSuccess(templateDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting templates for user {UserId}", userId);
                return ApiResponse<List<TemplateResponseDto>>.InternalServerError("خطا در دریافت لیست قالب‌ها");
            }
        }

        public async Task<ApiResponse<List<CategoryGroupDto>>> GetTemplatesGroupedByCategoryAsync(int userId)
        {
            try
            {
                // ایجاد قالب‌های پیش‌فرض برای کاربر در صورت عدم وجود
                await CreateDefaultTemplatesForUserAsync(userId);

                // دریافت فقط قالب‌های فعال کاربر (هم پیش‌فرض و هم شخصی) با eager loading Group
                var templates = await _context.MessageTemplates
                    .AsNoTracking()
                    .Include(mt => mt.Group)
                    .Where(mt => mt.UserId == userId && mt.IsActive && !mt.IsDeleted)
                    .ToListAsync();
                
                var templateDtos = templates.Select(MapToTemplateResponseDto).ToList();

                // گروه‌بندی قالب‌ها بر اساس Category
                var groupedTemplates = templateDtos
                    .GroupBy(t => t.Category ?? "بدون دسته‌بندی")
                    .Select(g => new CategoryGroupDto
                    {
                        CategoryName = g.Key == "بدون دسته‌بندی" ? null : g.Key,
                        Templates = g
                            .OrderByDescending(t => t.IsDefault) // قالب‌های پیش‌فرض اول
                            .ThenByDescending(t => t.CreatedAt) // سپس بر اساس تاریخ ایجاد
                            .ToList()
                    })
                    .OrderBy(g => g.CategoryName ?? "zzz") // قالب‌های بدون دسته‌بندی در آخر
                    .ToList();

                _logger.LogInformation("Retrieved {Count} grouped templates for user {UserId}", groupedTemplates.Count, userId);

                return ApiResponse<List<CategoryGroupDto>>.CreateSuccess(groupedTemplates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting grouped templates for user {UserId}", userId);
                return ApiResponse<List<CategoryGroupDto>>.InternalServerError("خطا در دریافت لیست دسته‌بندی شده قالب‌ها");
            }
        }

        public async Task<ApiResponse<TemplateResponseDto>> UpdateTemplateAsync(int id, int userId, UpdateTemplateDto updateDto)
        {
            string? uploadedIconPath = null;
            string? oldIconPath = null;

            // آپلود فایل آیکون قبل از شروع Transaction (طبق RULES.txt: اول فایل آپلود شود)
            if (updateDto.IconFile != null && _fileUploadService != null)
            {
                try
                {
                    // استفاده از id به عنوان entityId
                    // فایل در پوشه template/{id}/icons ذخیره می‌شود
                    uploadedIconPath = await _fileUploadService.UploadFileAsync(
                        updateDto.IconFile, 
                        "template", 
                        id, 
                        "icons"
                    );
                    _logger.LogInformation("📤 Icon file uploaded successfully: {IconPath}", uploadedIconPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error uploading icon file for template");
                    return ApiResponse<TemplateResponseDto>.BadRequest(ControlledErrorHelper.FileUploadFailed);
                }
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var template = await _templateRepository.GetByIdAsync(id);
                if (template == null || template.UserId != userId)
                {
                    await transaction.RollbackAsync();
                    // حذف فایل در صورت خطا
                    if (uploadedIconPath != null && _fileUploadService != null)
                    {
                        try
                        {
                            await _fileUploadService.DeleteFileAsync(uploadedIconPath, "template", id, "icons");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "⚠️ Error deleting uploaded file after validation error");
                        }
                    }
                    return ApiResponse<TemplateResponseDto>.NotFound("قالب مورد نظر یافت نشد");
                }

                // ذخیره مسیر فایل قدیم برای حذف بعدی
                oldIconPath = template.Icon;

                // بررسی اینکه آیا قالب پیش‌فرض است (قالب‌های پیش‌فرض قابل ویرایش هستند اما باید IsDefault حفظ شود)
                // این بررسی اختیاری است - می‌توانیم اجازه ویرایش قالب‌های پیش‌فرض را بدهیم

                // بررسی وجود گروه در صورت ارسال GroupId
                // توجه: در C# نمی‌توانیم بین null و عدم ارسال تفاوت قائل شویم
                // بنابراین فقط در صورتی که GroupId مقدار داشته باشد (HasValue = true) آن را به‌روزرسانی می‌کنیم
                if (updateDto.GroupId.HasValue)
                {
                    var groupExists = await _context.TemplateGroups
                        .AnyAsync(tg => tg.Id == updateDto.GroupId.Value && 
                                       tg.UserId == userId && 
                                       !tg.IsDeleted);
                    if (!groupExists)
                    {
                        await transaction.RollbackAsync();
                        // حذف فایل در صورت خطا
                        if (uploadedIconPath != null && _fileUploadService != null)
                        {
                            try
                            {
                                await _fileUploadService.DeleteFileAsync(uploadedIconPath, "template", id, "icons");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "⚠️ Error deleting uploaded file after validation error");
                            }
                        }
                        return ApiResponse<TemplateResponseDto>.BadRequest("گروه قالب مورد نظر یافت نشد");
                    }
                    template.GroupId = updateDto.GroupId.Value;
                }
                // برای حذف گروه از قالب، می‌توان از endpoint جداگانه استفاده کرد یا GroupId را به 0 تنظیم کرد

                if (updateDto.Name != null) template.Name = updateDto.Name.Trim();
                var contentChanged = false;
                if (updateDto.Content != null)
                {
                    template.Content = updateDto.Content.Trim();
                    contentChanged = true;
                }
                if (updateDto.Category != null) template.Category = updateDto.Category.Trim();
                if (updateDto.Description != null) template.Description = updateDto.Description.Trim();

                if (contentChanged || updateDto.Name != null)
                {
                    template.ApprovalStatus = AdminApprovalStatuses.Pending;
                    template.ApprovedAt = null;
                    template.ApprovedByUserId = null;
                    template.RejectionReason = null;
                }
                
                // تعیین مقدار Icon: اولویت با فایل آپلود شده، سپس Icon متنی
                if (!string.IsNullOrWhiteSpace(uploadedIconPath))
                {
                    template.Icon = uploadedIconPath;
                }
                else if (updateDto.Icon != null)
                {
                    template.Icon = updateDto.Icon.Trim();
                }
                
                if (updateDto.IsActive.HasValue) template.IsActive = updateDto.IsActive.Value;

                template.UpdatedAt = DateTime.UtcNow;

                _context.MessageTemplates.Update(template);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // حذف فایل قدیم در صورت موفقیت (بعد از commit)
                if (!string.IsNullOrWhiteSpace(uploadedIconPath) && 
                    !string.IsNullOrWhiteSpace(oldIconPath) && 
                    oldIconPath != uploadedIconPath &&
                    _fileUploadService != null)
                {
                    try
                    {
                        // استخراج entityId از مسیر قدیم یا استفاده از id
                        await _fileUploadService.DeleteFileAsync(oldIconPath, "template", id, "icons");
                        _logger.LogInformation("🗑️ Old icon file deleted: {OldIconPath}", oldIconPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ Error deleting old icon file: {OldIconPath}", oldIconPath);
                        // این خطا نباید باعث شکست عملیات شود
                    }
                }

                // لود کردن template با Group برای mapping
                var updatedTemplate = await _context.MessageTemplates
                    .AsNoTracking()
                    .Include(mt => mt.Group)
                    .FirstOrDefaultAsync(mt => mt.Id == id);

                _logger.LogInformation("✅ Template updated successfully with ID: {TemplateId} by user: {UserId}", 
                    id, userId);

                return ApiResponse<TemplateResponseDto>.CreateSuccess(
                    MapToTemplateResponseDto(updatedTemplate ?? template),
                    "قالب با موفقیت به‌روزرسانی شد"
                );
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();
                // حذف فایل در صورت خطا
                if (uploadedIconPath != null && _fileUploadService != null)
                {
                    try
                    {
                        await _fileUploadService.DeleteFileAsync(uploadedIconPath, "template", id, "icons");
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(deleteEx, "⚠️ Error deleting uploaded file after concurrency error");
                    }
                }
                _logger.LogWarning(ex, "⚠️ Concurrency conflict while updating template: {TemplateId}", id);
                return ApiResponse<TemplateResponseDto>.BadRequest("این قالب توسط کاربر دیگری به‌روزرسانی شده است. لطفاً صفحه را رفرش کنید و دوباره تلاش کنید");
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                // حذف فایل در صورت خطا
                if (uploadedIconPath != null && _fileUploadService != null)
                {
                    try
                    {
                        await _fileUploadService.DeleteFileAsync(uploadedIconPath, "template", id, "icons");
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(deleteEx, "⚠️ Error deleting uploaded file after database error");
                    }
                }
                _logger.LogError(ex, "❌ Database error updating template: {TemplateId}", id);
                return ApiResponse<TemplateResponseDto>.InternalServerError("خطا در به‌روزرسانی قالب. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                // حذف فایل در صورت خطا
                if (uploadedIconPath != null && _fileUploadService != null)
                {
                    try
                    {
                        await _fileUploadService.DeleteFileAsync(uploadedIconPath, "template", id, "icons");
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(deleteEx, "⚠️ Error deleting uploaded file after unexpected error");
                    }
                }
                _logger.LogError(ex, "❌ Unexpected error updating template: {TemplateId}", id);
                return ApiResponse<TemplateResponseDto>.InternalServerError("خطای غیرمنتظره در به‌روزرسانی قالب");
            }
        }

        public async Task<ApiResponse<bool>> DeleteTemplateAsync(int id, int userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var template = await _templateRepository.GetByIdAsync(id);
                if (template == null || template.UserId != userId)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<bool>.NotFound("قالب مورد نظر یافت نشد");
                }

                // بررسی اینکه آیا قالب پیش‌فرض است (قالب‌های پیش‌فرض قابل حذف نیستند)
                if (template.IsDefault)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<bool>.BadRequest("قالب‌های پیش‌فرض قابل حذف نیستند");
                }

                // بررسی استفاده در پیام‌ها (اختیاری - می‌توانیم اجازه حذف را بدهیم)
                var isUsedInMessages = await _context.Messages
                    .AnyAsync(m => m.TemplateId == id && !m.IsDeleted);
                
                if (isUsedInMessages)
                {
                    _logger.LogWarning("Template {TemplateId} is used in messages but will be soft deleted", id);
                    // می‌توانیم اجازه حذف را بدهیم یا خطا برگردانیم - در اینجا اجازه می‌دهیم
                }

                template.IsDeleted = true;
                template.UpdatedAt = DateTime.UtcNow;

                _context.MessageTemplates.Update(template);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("🗑️ Template deleted successfully with ID: {TemplateId} by user: {UserId}", 
                    id, userId);

                return ApiResponse<bool>.CreateSuccess(true, "قالب با موفقیت حذف شد");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(ex, "⚠️ Concurrency conflict while deleting template: {TemplateId}", id);
                return ApiResponse<bool>.BadRequest("این قالب در حال استفاده توسط درخواست دیگری است. لطفاً دوباره تلاش کنید");
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "❌ Database error deleting template: {TemplateId}", id);
                return ApiResponse<bool>.InternalServerError("خطا در حذف قالب. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "❌ Unexpected error deleting template: {TemplateId}", id);
                return ApiResponse<bool>.InternalServerError("خطای غیرمنتظره در حذف قالب");
            }
        }

        public async Task<ApiResponse<TemplateResponseDto>> SetUserDefaultTemplateAsync(int userId, int templateId)
        {
            _logger.LogInformation("📥 Setting default template for user {UserId}, template {TemplateId}", userId, templateId);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // بررسی وجود کاربر
                var userExists = await _context.Users.AnyAsync(u => u.Id == userId && !u.IsDeleted);
                if (!userExists)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<TemplateResponseDto>.NotFound("کاربر یافت نشد");
                }

                // بررسی وجود قالب و تعلق آن به کاربر
                var template = await _context.MessageTemplates
                    .FirstOrDefaultAsync(mt => mt.Id == templateId && 
                                               mt.UserId == userId && 
                                               mt.IsActive && 
                                               !mt.IsDeleted);

                if (template == null)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<TemplateResponseDto>.NotFound("قالب مورد نظر یافت نشد یا متعلق به شما نیست");
                }

                // غیرفعال کردن تمام قالب‌های پیش‌فرض قبلی کاربر (اگر وجود داشته باشند)
                var previousDefaultTemplates = await _context.MessageTemplates
                    .Where(mt => mt.UserId == userId && 
                                 mt.IsDefault && 
                                 mt.IsActive && 
                                 !mt.IsDeleted &&
                                 mt.Id != templateId)
                    .ToListAsync();

                if (previousDefaultTemplates.Any())
                {
                    foreach (var previousTemplate in previousDefaultTemplates)
                    {
                        previousTemplate.IsDefault = false;
                        previousTemplate.UpdatedAt = DateTime.UtcNow;
                        _context.MessageTemplates.Update(previousTemplate);
                    }
                    _logger.LogInformation("🔧 {Count} previous default template(s) unset for user {UserId}", 
                        previousDefaultTemplates.Count, userId);
                }

                // تنظیم قالب جدید به عنوان پیش‌فرض
                template.IsDefault = true;
                template.UpdatedAt = DateTime.UtcNow;
                _context.MessageTemplates.Update(template);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // لود کردن template با Group برای mapping
                var updatedTemplate = await _context.MessageTemplates
                    .AsNoTracking()
                    .Include(mt => mt.Group)
                    .FirstOrDefaultAsync(mt => mt.Id == templateId);

                _logger.LogInformation("✅ Default template set successfully: Template {TemplateId} for user {UserId}", 
                    templateId, userId);

                return ApiResponse<TemplateResponseDto>.CreateSuccess(
                    MapToTemplateResponseDto(updatedTemplate ?? template),
                    "قالب پیش‌فرض با موفقیت تنظیم شد"
                );
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(ex, "⚠️ Concurrency conflict while setting default template: {TemplateId} for user: {UserId}", 
                    templateId, userId);
                return ApiResponse<TemplateResponseDto>.BadRequest("این قالب در حال استفاده توسط درخواست دیگری است. لطفاً دوباره تلاش کنید");
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "❌ Database error setting default template: {TemplateId} for user: {UserId}", 
                    templateId, userId);
                return ApiResponse<TemplateResponseDto>.InternalServerError("خطا در تنظیم قالب پیش‌فرض. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "❌ Unexpected error setting default template: {TemplateId} for user: {UserId}", 
                    templateId, userId);
                return ApiResponse<TemplateResponseDto>.InternalServerError("خطای غیرمنتظره در تنظیم قالب پیش‌فرض");
            }
        }

        public async Task<ApiResponse<DirectSendResultDto>> QuickSendMessageAsync(int userId, QuickSendMessageDto quickSendDto)
        {
            _logger.LogInformation("📥 Quick send message for user {UserId}, contact {ContactId}", userId, quickSendDto.ContactId);

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

                // 3. پیدا کردن قالب پیش‌فرض کاربر
                var defaultTemplate = await _context.MessageTemplates
                    .FirstOrDefaultAsync(mt => mt.UserId == userId && 
                                               mt.IsDefault && 
                                               mt.IsActive && 
                                               !mt.IsDeleted);

                string messageContent;
                int? templateId = null;

                if (defaultTemplate == null)
                {
                    // اگر قالب پیش‌فرض وجود نداشت، یک پیام مناسب با placeholder ارسال می‌کنیم
                    // این پیام بعداً با PersonalizeMessageWithContactAsync شخصی‌سازی می‌شود
                    messageContent = "سلام {{نام}} عزیز!\n\nاز تماس شما متشکریم.\n\nبا احترام";
                    _logger.LogInformation("⚠️ No default template found for user {UserId}, using default message", userId);
                }
                else
                {
                    messageContent = defaultTemplate.Content;
                    templateId = defaultTemplate.Id;
                }

                // 4. ایجاد پیام
                var message = new Message
                {
                    UserId = userId,
                    Content = messageContent,
                    TemplateId = templateId,
                    IsPersonalized = true, // قالب‌ها معمولاً placeholder دارند
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Messages.AddAsync(message);
                await _context.SaveChangesAsync();

                // 5. شخصی‌سازی پیام با اطلاعات مخاطب (حتی اگر قالب پیش‌فرض وجود نداشت)
                var personalizedContent = await PersonalizeMessageWithContactAsync(message.Content, contact);
                message.Content = personalizedContent;
                message.UpdatedAt = DateTime.UtcNow;

                // استخراج Placeholder ها
                message.Placeholders = ExtractPlaceholders(personalizedContent);

                _context.Messages.Update(message);
                await _context.SaveChangesAsync();

                // 6. Commit Transaction قبل از صدا زدن SelectRecipientsAsync (که خودش Transaction دارد)
                await transaction.CommitAsync();

                // 7. ایجاد SelectRecipientsDto برای انتخاب گیرنده
                var selectRecipientsDto = new SelectRecipientsDto
                {
                    MessageId = message.Id, // اضافه کردن MessageId که الزامی است
                    SelectionType = "Individual",
                    MobileNumbers = new List<string> { contact.MobileNumber },
                    FullNames = new List<string> { contact.FullName ?? "" }
                };

                // 8. انتخاب گیرندگان (این یک Session ایجاد می‌کند و خودش Transaction دارد)
                var selectResult = await SelectRecipientsAsync(userId, selectRecipientsDto);
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

                var sendResult = await SendDirectMessageAsync(userId, message.Id, sendDto, session);

                _logger.LogInformation("✅ Quick send message completed - MessageId: {MessageId}, ContactId: {ContactId}, UserId: {UserId}", 
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
                _logger.LogWarning(ex, "⚠️ Concurrency conflict while quick sending message: ContactId {ContactId} for user: {UserId}", 
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
                _logger.LogError(ex, "❌ Database error quick sending message: ContactId {ContactId} for user: {UserId}", 
                    quickSendDto.ContactId, userId);
                return ApiResponse<DirectSendResultDto>.InternalServerError("خطا در ارسال پیام سریع. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch { }
                _logger.LogError(ex, "❌ Unexpected error quick sending message: ContactId {ContactId} for user: {UserId}", 
                    quickSendDto.ContactId, userId);
                return ApiResponse<DirectSendResultDto>.InternalServerError("خطای غیرمنتظره در ارسال پیام سریع");
            }
        }

        public async Task<ApiResponse<MessageTagListResponseDto>> GetTagsAsync(int userId, int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                _logger.LogInformation("GetTagsAsync called for user {UserId}, pageNumber: {PageNumber}, pageSize: {PageSize}", 
                    userId, pageNumber, pageSize);
                
                // بررسی اینکه userId معتبر است
                var userExists = await _context.Users.AnyAsync(u => u.Id == userId && !u.IsDeleted);
                if (!userExists)
                {
                    _logger.LogWarning("User {UserId} does not exist or is deleted! Returning empty list.", userId);
                    return ApiResponse<MessageTagListResponseDto>.CreateSuccess(new MessageTagListResponseDto
                    {
                        Tags = new List<MessageTagResponseDto>(),
                        TotalCount = 0,
                        PageNumber = pageNumber,
                        PageSize = pageSize,
                        TotalPages = 0
                    });
                }

                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                var query = _context.MessageTags
                    .Where(t => t.UserId == userId && !t.IsDeleted && t.IsActive);

                // بررسی اینکه آیا داده‌ای وجود دارد یا نه
                var allTagsCount = await _context.MessageTags
                    .Where(t => t.UserId == userId)
                    .CountAsync();
                
                var activeTagsCount = await _context.MessageTags
                    .Where(t => t.UserId == userId && !t.IsDeleted && t.IsActive)
                    .CountAsync();

                // بررسی تمام تگ‌های موجود در دیتابیس (برای دیباگ)
                var allTagsInDb = await _context.MessageTags
                    .Select(t => new { t.Id, t.UserId, t.Name, t.IsDeleted, t.IsActive })
                    .ToListAsync();
                
                _logger.LogInformation("User {UserId} has {TotalCount} total tags, {ActiveCount} active tags", 
                    userId, allTagsCount, activeTagsCount);
                _logger.LogInformation("All tags in database: {Tags}", 
                    string.Join(", ", allTagsInDb.Select(t => $"ID:{t.Id}, UserId:{t.UserId}, Name:{t.Name}, Deleted:{t.IsDeleted}, Active:{t.IsActive}")));

                var totalCount = await query.CountAsync();
                var totalPages = totalCount > 0 ? (int)Math.Ceiling(totalCount / (double)pageSize) : 0;

                // بررسی اینکه pageNumber از totalPages بیشتر نباشد
                if (totalPages > 0 && pageNumber > totalPages)
                {
                    pageNumber = totalPages;
                }

                var tags = await query
                    .OrderBy(t => t.Name)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(t => new MessageTagResponseDto
                    {
                        Id = t.Id,
                        Name = t.Name,
                        Color = t.Color,
                        Description = t.Description,
                        IsActive = t.IsActive,
                        CreatedAt = t.CreatedAt
                    })
                    .ToListAsync();

                _logger.LogInformation("Returning {Count} tags for user {UserId}", tags.Count, userId);

                var response = new MessageTagListResponseDto
                {
                    Tags = tags,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                return ApiResponse<MessageTagListResponseDto>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tags for user: {UserId}", userId);
                return ApiResponse<MessageTagListResponseDto>.InternalServerError("خطا در دریافت تگ‌ها");
            }
        }

        /// <summary>
        /// دریافت لیست تگ‌های کاربر همراه با تعداد مخاطبین هر تگ از تمام دفترچه‌ها
        /// </summary>
        public async Task<ApiResponse<MessageTagResponseDto>> CreateTagAsync(int userId, CreateMessageTagDto createDto)
        {
            try
            {
                // بررسی وجود تگ با همین نام برای کاربر
                var existingTag = await _context.MessageTags
                    .Where(t => t.UserId == userId 
                        && t.Name == createDto.Name 
                        && !t.IsDeleted)
                    .FirstOrDefaultAsync();

                if (existingTag != null)
                {
                    return ApiResponse<MessageTagResponseDto>.BadRequest("تگ با این نام قبلاً ایجاد شده است");
                }

                var tag = new MessageTag
                {
                    UserId = userId,
                    Name = createDto.Name.Trim(),
                    Color = createDto.Color,
                    Description = createDto.Description,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.MessageTags.AddAsync(tag);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Tag created successfully with ID: {TagId} by user: {UserId}", 
                    tag.Id, userId);

                var tagDto = new MessageTagResponseDto
                {
                    Id = tag.Id,
                    Name = tag.Name,
                    Color = tag.Color,
                    Description = tag.Description,
                    IsActive = tag.IsActive,
                    CreatedAt = tag.CreatedAt
                };

                return ApiResponse<MessageTagResponseDto>.CreateSuccess(
                    tagDto,
                    "تگ با موفقیت ایجاد شد",
                    201
                );
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating tag for user: {UserId}", userId);
                return ApiResponse<MessageTagResponseDto>.InternalServerError("خطا در ذخیره‌سازی تگ. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating tag for user: {UserId}", userId);
                return ApiResponse<MessageTagResponseDto>.InternalServerError("خطای غیرمنتظره در ایجاد تگ");
            }
        }

        public async Task<ApiResponse<MessageTagWithContactCountListResponseDto>> GetTagsWithContactCountAsync(int userId, int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                // دریافت تمام دفترچه‌های کاربر
                var notebookIds = await _context.ContactNotebooks
                    .Where(n => n.UserId == userId && !n.IsDeleted)
                    .Select(n => n.Id)
                    .ToListAsync();

                // دریافت تمام مخاطبین کاربر (از تمام دفترچه‌ها)
                var allContactIds = await _context.Contacts
                    .Where(c => notebookIds.Contains(c.ContactNotebookId) && !c.IsDeleted)
                    .Select(c => c.Id)
                    .ToListAsync();

                // دریافت تگ‌های کاربر با pagination
                var query = _context.MessageTags
                    .Where(t => t.UserId == userId && !t.IsDeleted && t.IsActive);

                var totalCount = await query.CountAsync();
                var totalPages = totalCount > 0 ? (int)Math.Ceiling(totalCount / (double)pageSize) : 0;

                // بررسی اینکه pageNumber از totalPages بیشتر نباشد
                if (totalPages > 0 && pageNumber > totalPages)
                {
                    pageNumber = totalPages;
                }

                var tags = await query
                    .OrderBy(t => t.Name)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // محاسبه تعداد مخاطبین برای هر تگ
                var tagDtos = new List<MessageTagWithContactCountDto>();
                foreach (var tag in tags)
                {
                    var contactCount = await _context.ContactTags
                        .Where(ct => ct.TagId == tag.Id && allContactIds.Contains(ct.ContactId))
                        .CountAsync();

                    tagDtos.Add(new MessageTagWithContactCountDto
                    {
                        Id = tag.Id,
                        Name = tag.Name,
                        Color = tag.Color,
                        Description = tag.Description,
                        IsActive = tag.IsActive,
                        CreatedAt = tag.CreatedAt,
                        ContactCount = contactCount
                    });
                }

                var response = new MessageTagWithContactCountListResponseDto
                {
                    Tags = tagDtos,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                return ApiResponse<MessageTagWithContactCountListResponseDto>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tags with contact count for user: {UserId}", userId);
                return ApiResponse<MessageTagWithContactCountListResponseDto>.InternalServerError("خطا در دریافت تگ‌ها");
            }
        }

        #endregion

        #region Template Group Operations

        public async Task<ApiResponse<TemplateGroupResponseDto>> CreateTemplateGroupAsync(int userId, CreateTemplateGroupDto createDto)
        {
            string? uploadedIconPath = null;

            // آپلود فایل آیکون قبل از شروع Transaction (طبق RULES.txt: اول فایل آپلود شود)
            if (createDto.IconFile != null && _fileUploadService != null)
            {
                try
                {
                    // استفاده از userId به عنوان entityId موقت
                    // فایل در پوشه template-group/{userId}/icons ذخیره می‌شود
                    uploadedIconPath = await _fileUploadService.UploadFileAsync(
                        createDto.IconFile, 
                        "template-group", 
                        userId, 
                        "icons"
                    );
                    _logger.LogInformation("📤 Icon file uploaded successfully: {IconPath}", uploadedIconPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error uploading icon file for template group");
                    return ApiResponse<TemplateGroupResponseDto>.BadRequest(ControlledErrorHelper.FileUploadFailed);
                }
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // بررسی فیلدهای الزامی
                if (string.IsNullOrWhiteSpace(createDto.Name))
                {
                    await transaction.RollbackAsync();
                    // حذف فایل در صورت خطا
                    if (uploadedIconPath != null && _fileUploadService != null)
                    {
                        try
                        {
                            await _fileUploadService.DeleteFileAsync(uploadedIconPath, "template-group", userId, "icons");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "⚠️ Error deleting uploaded file after validation error");
                        }
                    }
                    return ApiResponse<TemplateGroupResponseDto>.BadRequest("نام گروه نمی‌تواند خالی باشد");
                }

                // بررسی وجود کاربر
                var userExists = await _context.Users.AnyAsync(u => u.Id == userId && !u.IsDeleted);
                if (!userExists)
                {
                    await transaction.RollbackAsync();
                    // حذف فایل در صورت خطا
                    if (uploadedIconPath != null && _fileUploadService != null)
                    {
                        try
                        {
                            await _fileUploadService.DeleteFileAsync(uploadedIconPath, "template-group", userId, "icons");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "⚠️ Error deleting uploaded file after validation error");
                        }
                    }
                    return ApiResponse<TemplateGroupResponseDto>.NotFound("کاربر یافت نشد");
                }

                // بررسی تکراری نبودن نام گروه برای کاربر
                var duplicateName = await _context.TemplateGroups
                    .AnyAsync(tg => tg.UserId == userId && 
                                   tg.Name.Trim().ToLower() == createDto.Name.Trim().ToLower() && 
                                   !tg.IsDeleted);
                if (duplicateName)
                {
                    await transaction.RollbackAsync();
                    // حذف فایل در صورت خطا
                    if (uploadedIconPath != null && _fileUploadService != null)
                    {
                        try
                        {
                            await _fileUploadService.DeleteFileAsync(uploadedIconPath, "template-group", userId, "icons");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "⚠️ Error deleting uploaded file after validation error");
                        }
                    }
                    return ApiResponse<TemplateGroupResponseDto>.BadRequest("گروهی با این نام قبلاً ایجاد شده است");
                }

                // تعیین مقدار Icon: اولویت با فایل آپلود شده، سپس Icon متنی
                string? iconValue = null;
                if (!string.IsNullOrWhiteSpace(uploadedIconPath))
                {
                    iconValue = uploadedIconPath;
                }
                else if (!string.IsNullOrWhiteSpace(createDto.Icon))
                {
                    iconValue = createDto.Icon.Trim();
                }

                var templateGroup = new TemplateGroup
                {
                    UserId = userId,
                    Name = createDto.Name.Trim(),
                    Description = createDto.Description?.Trim(),
                    Icon = iconValue,
                    DisplayOrder = createDto.DisplayOrder,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.TemplateGroups.AddAsync(templateGroup);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("✅ Template group created successfully with ID: {GroupId} by user: {UserId}", 
                    templateGroup.Id, userId);

                var templatesCount = await _context.MessageTemplates
                    .AsNoTracking()
                    .CountAsync(mt => mt.GroupId == templateGroup.Id && !mt.IsDeleted);

                return ApiResponse<TemplateGroupResponseDto>.CreateSuccess(
                    MapToTemplateGroupResponseDto(templateGroup, templatesCount),
                    "گروه قالب با موفقیت ایجاد شد",
                    201
                );
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();
                // حذف فایل در صورت خطا
                if (uploadedIconPath != null && _fileUploadService != null)
                {
                    try
                    {
                        await _fileUploadService.DeleteFileAsync(uploadedIconPath, "template-group", userId, "icons");
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(deleteEx, "⚠️ Error deleting uploaded file after concurrency error");
                    }
                }
                _logger.LogWarning(ex, "⚠️ Concurrency conflict while creating template group for user: {UserId}", userId);
                return ApiResponse<TemplateGroupResponseDto>.BadRequest("این گروه در حال استفاده توسط درخواست دیگری است. لطفاً دوباره تلاش کنید");
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                // حذف فایل در صورت خطا
                if (uploadedIconPath != null && _fileUploadService != null)
                {
                    try
                    {
                        await _fileUploadService.DeleteFileAsync(uploadedIconPath, "template-group", userId, "icons");
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(deleteEx, "⚠️ Error deleting uploaded file after database error");
                    }
                }
                _logger.LogError(ex, "❌ Database error creating template group for user: {UserId}", userId);
                return ApiResponse<TemplateGroupResponseDto>.InternalServerError("خطا در ذخیره‌سازی گروه. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                // حذف فایل در صورت خطا
                if (uploadedIconPath != null && _fileUploadService != null)
                {
                    try
                    {
                        await _fileUploadService.DeleteFileAsync(uploadedIconPath, "template-group", userId, "icons");
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(deleteEx, "⚠️ Error deleting uploaded file after unexpected error");
                    }
                }
                _logger.LogError(ex, "❌ Unexpected error creating template group for user: {UserId}", userId);
                return ApiResponse<TemplateGroupResponseDto>.InternalServerError("خطای غیرمنتظره در ایجاد گروه");
            }
        }

        public async Task<ApiResponse<List<TemplateGroupSummaryDto>>> GetTemplateGroupsAsync(int userId)
        {
            try
            {
                // ایجاد قالب‌های پیش‌فرض برای کاربر در صورت عدم وجود
                await CreateDefaultTemplatesForUserAsync(userId);

                // دریافت قالب‌های فعال کاربر برای شمارش
                var templates = await _context.MessageTemplates
                    .AsNoTracking()
                    .Where(mt => mt.UserId == userId && mt.IsActive && !mt.IsDeleted)
                    .ToListAsync();

                // دریافت لیست گروه‌های کاربر برای ترتیب‌دهی
                var groups = await _context.TemplateGroups
                    .AsNoTracking()
                    .Where(tg => tg.UserId == userId && !tg.IsDeleted)
                    .OrderBy(tg => tg.DisplayOrder)
                    .ThenBy(tg => tg.CreatedAt)
                    .ToListAsync();

                // شمارش قالب‌ها بر اساس GroupId
                var templateCounts = templates
                    .GroupBy(t => t.GroupId)
                    .ToDictionary(g => g.Key ?? -1, g => g.Count());

                // ایجاد لیست گروه‌ها با تعداد قالب‌ها
                var groupSummaries = groups
                    .Select(g => new TemplateGroupSummaryDto
                    {
                        Id = g.Id.ToString(),
                        Name = g.Name,
                        Count = templateCounts.GetValueOrDefault(g.Id, 0)
                    })
                    .ToList();

                // افزودن گروه "بدون گروه" (همیشه نمایش داده می‌شود)
                var uncategorizedCount = templateCounts.GetValueOrDefault(-1, 0);
                groupSummaries.Add(new TemplateGroupSummaryDto
                {
                    Id = "0",
                    Name = "بدون گروه",
                    Count = uncategorizedCount
                });

                // مرتب‌سازی: اول گروه‌های با ترتیب مشخص، سپس بدون گروه
                groupSummaries = groupSummaries
                    .OrderBy(g =>
                    {
                        if (g.Id == "0") return int.MaxValue;
                        var group = groups.FirstOrDefault(gr => gr.Id.ToString() == g.Id);
                        return group?.DisplayOrder ?? int.MaxValue;
                    })
                    .ThenBy(g => g.Name)
                    .ToList();

                _logger.LogInformation("Retrieved {Count} template groups for user {UserId}", groupSummaries.Count, userId);

                return ApiResponse<List<TemplateGroupSummaryDto>>.CreateSuccess(groupSummaries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting template groups for user {UserId}", userId);
                return ApiResponse<List<TemplateGroupSummaryDto>>.InternalServerError("خطا در دریافت لیست گروه‌های قالب");
            }
        }

        public async Task<ApiResponse<TemplateGroupResponseDto>> GetTemplateGroupByIdAsync(int id, int userId)
        {
            try
            {
                var group = await _context.TemplateGroups
                    .AsNoTracking()
                    .FirstOrDefaultAsync(tg => tg.Id == id && tg.UserId == userId && !tg.IsDeleted);

                if (group == null)
                {
                    return ApiResponse<TemplateGroupResponseDto>.NotFound("گروه مورد نظر یافت نشد");
                }

                var templatesCount = await _context.MessageTemplates
                    .AsNoTracking()
                    .CountAsync(mt => mt.GroupId == id && !mt.IsDeleted);

                return ApiResponse<TemplateGroupResponseDto>.CreateSuccess(MapToTemplateGroupResponseDto(group, templatesCount));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting template group {GroupId} for user {UserId}", id, userId);
                return ApiResponse<TemplateGroupResponseDto>.InternalServerError("خطا در دریافت گروه");
            }
        }

        public async Task<ApiResponse<List<TemplateResponseDto>>> GetTemplatesByGroupIdAsync(int groupId, int userId)
        {
            try
            {
                List<TemplateResponseDto> templateDtos;

                // اگر groupId = 0 باشد، قالب‌های بدون گروه را برمی‌گردانیم
                if (groupId == 0)
                {
                    var uncategorizedTemplates = await _context.MessageTemplates
                        .AsNoTracking()
                        .Include(mt => mt.Group)
                        .Where(mt => mt.GroupId == null && mt.UserId == userId && mt.IsActive && !mt.IsDeleted)
                        .ToListAsync();

                    templateDtos = uncategorizedTemplates
                        .Select(MapToTemplateResponseDto)
                        .OrderByDescending(t => t.IsDefault) // قالب‌های پیش‌فرض اول
                        .ThenByDescending(t => t.CreatedAt) // سپس بر اساس تاریخ ایجاد
                        .ToList();

                    _logger.LogInformation("Retrieved {Count} uncategorized templates for user {UserId}", templateDtos.Count, userId);

                    return ApiResponse<List<TemplateResponseDto>>.CreateSuccess(templateDtos);
                }

                // بررسی وجود گروه و تعلق آن به کاربر
                var group = await _context.TemplateGroups
                    .AsNoTracking()
                    .FirstOrDefaultAsync(tg => tg.Id == groupId && tg.UserId == userId && !tg.IsDeleted);

                if (group == null)
                {
                    return ApiResponse<List<TemplateResponseDto>>.NotFound("گروه مورد نظر یافت نشد");
                }

                // دریافت تمام قالب‌های این گروه
                var templates = await _context.MessageTemplates
                    .AsNoTracking()
                    .Include(mt => mt.Group)
                    .Where(mt => mt.GroupId == groupId && mt.UserId == userId && mt.IsActive && !mt.IsDeleted)
                    .ToListAsync();

                templateDtos = templates
                    .Select(MapToTemplateResponseDto)
                    .OrderByDescending(t => t.IsDefault) // قالب‌های پیش‌فرض اول
                    .ThenByDescending(t => t.CreatedAt) // سپس بر اساس تاریخ ایجاد
                    .ToList();

                _logger.LogInformation("Retrieved {Count} templates for group {GroupId} and user {UserId}", templateDtos.Count, groupId, userId);

                return ApiResponse<List<TemplateResponseDto>>.CreateSuccess(templateDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting templates for group {GroupId} and user {UserId}", groupId, userId);
                return ApiResponse<List<TemplateResponseDto>>.InternalServerError("خطا در دریافت لیست قالی‌های گروه");
            }
        }

        public async Task<ApiResponse<TemplateGroupResponseDto>> UpdateTemplateGroupAsync(int id, int userId, UpdateTemplateGroupDto updateDto)
        {
            string? uploadedIconPath = null;
            string? oldIconPath = null;

            // آپلود فایل آیکون قبل از شروع Transaction (طبق RULES.txt: اول فایل آپلود شود)
            if (updateDto.IconFile != null && _fileUploadService != null)
            {
                try
                {
                    // استفاده از id به عنوان entityId
                    // فایل در پوشه template-group/{id}/icons ذخیره می‌شود
                    uploadedIconPath = await _fileUploadService.UploadFileAsync(
                        updateDto.IconFile, 
                        "template-group", 
                        id, 
                        "icons"
                    );
                    _logger.LogInformation("📤 Icon file uploaded successfully: {IconPath}", uploadedIconPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error uploading icon file for template group");
                    return ApiResponse<TemplateGroupResponseDto>.BadRequest(ControlledErrorHelper.FileUploadFailed);
                }
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var group = await _context.TemplateGroups
                    .FirstOrDefaultAsync(tg => tg.Id == id && tg.UserId == userId && !tg.IsDeleted);

                if (group == null)
                {
                    await transaction.RollbackAsync();
                    // حذف فایل در صورت خطا
                    if (uploadedIconPath != null && _fileUploadService != null)
                    {
                        try
                        {
                            await _fileUploadService.DeleteFileAsync(uploadedIconPath, "template-group", id, "icons");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "⚠️ Error deleting uploaded file after validation error");
                        }
                    }
                    return ApiResponse<TemplateGroupResponseDto>.NotFound("گروه مورد نظر یافت نشد");
                }

                // ذخیره مسیر فایل قدیم برای حذف بعدی
                oldIconPath = group.Icon;

                // بررسی تکراری نبودن نام گروه (در صورت تغییر نام)
                if (!string.IsNullOrWhiteSpace(updateDto.Name) && 
                    updateDto.Name.Trim().ToLower() != group.Name.Trim().ToLower())
                {
                    var duplicateName = await _context.TemplateGroups
                        .AnyAsync(tg => tg.UserId == userId && 
                                       tg.Id != id &&
                                       tg.Name.Trim().ToLower() == updateDto.Name.Trim().ToLower() && 
                                       !tg.IsDeleted);
                    if (duplicateName)
                    {
                        await transaction.RollbackAsync();
                        // حذف فایل در صورت خطا
                        if (uploadedIconPath != null && _fileUploadService != null)
                        {
                            try
                            {
                                await _fileUploadService.DeleteFileAsync(uploadedIconPath, "template-group", id, "icons");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "⚠️ Error deleting uploaded file after validation error");
                            }
                        }
                        return ApiResponse<TemplateGroupResponseDto>.BadRequest("گروهی با این نام قبلاً ایجاد شده است");
                    }
                }

                if (updateDto.Name != null) group.Name = updateDto.Name.Trim();
                if (updateDto.Description != null) group.Description = updateDto.Description.Trim();
                
                // تعیین مقدار Icon: اولویت با فایل آپلود شده، سپس Icon متنی
                if (!string.IsNullOrWhiteSpace(uploadedIconPath))
                {
                    group.Icon = uploadedIconPath;
                }
                else if (updateDto.Icon != null)
                {
                    group.Icon = updateDto.Icon.Trim();
                }
                
                if (updateDto.DisplayOrder.HasValue) group.DisplayOrder = updateDto.DisplayOrder.Value;
                if (updateDto.IsActive.HasValue) group.IsActive = updateDto.IsActive.Value;

                group.UpdatedAt = DateTime.UtcNow;

                _context.TemplateGroups.Update(group);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // حذف فایل قدیم در صورت موفقیت (بعد از commit)
                if (!string.IsNullOrWhiteSpace(uploadedIconPath) && 
                    !string.IsNullOrWhiteSpace(oldIconPath) && 
                    oldIconPath != uploadedIconPath &&
                    _fileUploadService != null)
                {
                    try
                    {
                        // استخراج entityId از مسیر قدیم یا استفاده از id
                        await _fileUploadService.DeleteFileAsync(oldIconPath, "template-group", id, "icons");
                        _logger.LogInformation("🗑️ Old icon file deleted: {OldIconPath}", oldIconPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ Error deleting old icon file: {OldIconPath}", oldIconPath);
                        // این خطا نباید باعث شکست عملیات شود
                    }
                }

                _logger.LogInformation("✅ Template group updated successfully with ID: {GroupId} by user: {UserId}", 
                    id, userId);

                var templatesCount = await _context.MessageTemplates
                    .AsNoTracking()
                    .CountAsync(mt => mt.GroupId == id && !mt.IsDeleted);

                return ApiResponse<TemplateGroupResponseDto>.CreateSuccess(
                    MapToTemplateGroupResponseDto(group, templatesCount),
                    "گروه با موفقیت به‌روزرسانی شد"
                );
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();
                // حذف فایل در صورت خطا
                if (uploadedIconPath != null && _fileUploadService != null)
                {
                    try
                    {
                        await _fileUploadService.DeleteFileAsync(uploadedIconPath, "template-group", id, "icons");
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(deleteEx, "⚠️ Error deleting uploaded file after concurrency error");
                    }
                }
                _logger.LogWarning(ex, "⚠️ Concurrency conflict while updating template group: {GroupId}", id);
                return ApiResponse<TemplateGroupResponseDto>.BadRequest("این گروه توسط کاربر دیگری به‌روزرسانی شده است. لطفاً صفحه را رفرش کنید و دوباره تلاش کنید");
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                // حذف فایل در صورت خطا
                if (uploadedIconPath != null && _fileUploadService != null)
                {
                    try
                    {
                        await _fileUploadService.DeleteFileAsync(uploadedIconPath, "template-group", id, "icons");
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(deleteEx, "⚠️ Error deleting uploaded file after database error");
                    }
                }
                _logger.LogError(ex, "❌ Database error updating template group: {GroupId}", id);
                return ApiResponse<TemplateGroupResponseDto>.InternalServerError("خطا در به‌روزرسانی گروه. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                // حذف فایل در صورت خطا
                if (uploadedIconPath != null && _fileUploadService != null)
                {
                    try
                    {
                        await _fileUploadService.DeleteFileAsync(uploadedIconPath, "template-group", id, "icons");
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(deleteEx, "⚠️ Error deleting uploaded file after unexpected error");
                    }
                }
                _logger.LogError(ex, "❌ Unexpected error updating template group: {GroupId}", id);
                return ApiResponse<TemplateGroupResponseDto>.InternalServerError("خطای غیرمنتظره در به‌روزرسانی گروه");
            }
        }

        public async Task<ApiResponse<bool>> DeleteTemplateGroupAsync(int id, int userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var group = await _context.TemplateGroups
                    .FirstOrDefaultAsync(tg => tg.Id == id && tg.UserId == userId && !tg.IsDeleted);

                if (group == null)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<bool>.NotFound("گروه مورد نظر یافت نشد");
                }

                // بررسی استفاده در قالب‌ها
                var templatesCount = await _context.MessageTemplates
                    .CountAsync(mt => mt.GroupId == id && !mt.IsDeleted);
                
                if (templatesCount > 0)
                {
                    await transaction.RollbackAsync();
                    _logger.LogWarning("⚠️ Cannot delete template group {GroupId} because it contains {Count} templates", id, templatesCount);
                    return ApiResponse<bool>.Error(
                        $"امکان حذف این گروه وجود ندارد زیرا {templatesCount} قالب در این گروه قرار دارد. لطفاً ابتدا قالب‌ها را به گروه دیگری منتقل کنید یا حذف کنید.",
                        409 // Conflict
                    );
                }

                group.IsDeleted = true;
                group.UpdatedAt = DateTime.UtcNow;

                _context.TemplateGroups.Update(group);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("🗑️ Template group deleted successfully with ID: {GroupId} by user: {UserId}", 
                    id, userId);

                return ApiResponse<bool>.CreateSuccess(true, "گروه با موفقیت حذف شد");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(ex, "⚠️ Concurrency conflict while deleting template group: {GroupId}", id);
                return ApiResponse<bool>.BadRequest("این گروه در حال استفاده توسط درخواست دیگری است. لطفاً دوباره تلاش کنید");
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "❌ Database error deleting template group: {GroupId}", id);
                return ApiResponse<bool>.InternalServerError("خطا در حذف گروه. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "❌ Unexpected error deleting template group: {GroupId}", id);
                return ApiResponse<bool>.InternalServerError("خطای غیرمنتظره در حذف گروه");
            }
        }

        private TemplateGroupResponseDto MapToTemplateGroupResponseDto(TemplateGroup group, int templatesCount = 0)
        {
            return new TemplateGroupResponseDto
            {
                Id = group.Id,
                Name = group.Name,
                Description = group.Description,
                Icon = group.Icon,
                DisplayOrder = group.DisplayOrder,
                IsActive = group.IsActive,
                TemplatesCount = templatesCount,
                CreatedAt = group.CreatedAt,
                UpdatedAt = group.UpdatedAt
            };
        }

        #endregion

        #region Recipient Operations

        public async Task<ApiResponse<RecipientListResponseDto>> SelectRecipientsAsync(int userId, SelectRecipientsDto selectDto)
        {
            // استفاده از Transaction برای تمام عملیات (مشکل 2.1)
            using var transaction = await _context.Database.BeginTransactionAsync();
            List<RecipientItemDto> recipients = new List<RecipientItemDto>(); // تعریف در scope بالاتر برای استفاده در catch block
            try
            {
                recipients = new List<RecipientItemDto>();

                if (selectDto.SelectionType == "Notebook" && selectDto.ContactNotebookIds != null && selectDto.ContactNotebookIds.Any())
                {
                    foreach (var notebookId in selectDto.ContactNotebookIds)
                    {
                        var notebook = await _notebookRepository.GetByIdAsync(notebookId);
                        if (notebook == null || notebook.UserId != userId) continue;

                        // دریافت همه مخاطبین دفترچه
                        var allContacts = await _contactRepository.GetByNotebookIdAsync(notebookId);
                        var validContacts = allContacts.Where(c => !c.IsDeleted).ToList();

                        // اگر ContactIds ارسال شده باشد، آن مخاطبین را از لیست حذف می‌کنیم (یعنی به آن‌ها پیام نمی‌رود)
                        // اگر ContactIds null باشد، همه مخاطبین انتخاب می‌شوند
                        if (selectDto.ContactIds != null && selectDto.ContactIds.Any())
                        {
                            // حذف مخاطبینی که در ContactIds هستند (یعنی به آن‌ها پیام نمی‌رود)
                            var excludedContactIds = selectDto.ContactIds.ToHashSet();
                            validContacts = validContacts
                                .Where(c => !excludedContactIds.Contains(c.Id))
                                .ToList();
                        }

                        recipients.AddRange(validContacts.Select(c => new RecipientItemDto
                        {
                            ContactId = c.Id,
                            MobileNumber = c.MobileNumber,
                            FullName = c.FullName
                        }));
                    }
                }
                else if (selectDto.SelectionType == "Tag" && selectDto.TagIds != null && selectDto.TagIds.Any())
                {
                    // دریافت تمام مخاطبین که تگ‌های انتخاب شده را دارند
                    var contactIds = await _context.ContactTags
                        .Where(ct => selectDto.TagIds.Contains(ct.TagId))
                        .Select(ct => ct.ContactId)
                        .Distinct()
                        .ToListAsync();

                    foreach (var contactId in contactIds)
                    {
                        var contact = await _contactRepository.GetByIdAsync(contactId);
                        if (contact != null && !contact.IsDeleted)
                        {
                            var notebook = await _notebookRepository.GetByIdAsync(contact.ContactNotebookId);
                            if (notebook != null && notebook.UserId == userId && !notebook.IsDeleted)
                            {
                                recipients.Add(new RecipientItemDto
                                {
                                    ContactId = contact.Id,
                                    MobileNumber = contact.MobileNumber,
                                    FullName = contact.FullName
                                });
                            }
                        }
                    }
                }
                else if (selectDto.SelectionType == MessageSelectionTypes.ContactIds)
                {
                    if (selectDto.ContactIds == null || !selectDto.ContactIds.Any(id => id > 0))
                    {
                        return ApiResponse<RecipientListResponseDto>.BadRequest(
                            "در حالت انتخاب دستی مخاطبین، باید حداقل یک شناسه مخاطب ارسال شود",
                            errorCode: ErrorCodes.ValidationFailed);
                    }

                    var selectedContactIds = selectDto.ContactIds
                        .Where(id => id > 0)
                        .Distinct()
                        .ToList();

                    var validContacts = await _contactRepository.GetByIdsForUserAsync(userId, selectedContactIds);

                    if (validContacts.Count != selectedContactIds.Count)
                    {
                        var foundIds = validContacts.Select(c => c.Id).ToHashSet();
                        var invalidIds = selectedContactIds.Where(id => !foundIds.Contains(id)).ToList();
                        return ApiResponse<RecipientListResponseDto>.BadRequest(
                            $"برخی مخاطبین انتخاب‌شده نامعتبر هستند یا متعلق به شما نیستند: [{string.Join(", ", invalidIds)}]",
                            errorCode: ErrorCodes.InvalidInput);
                    }

                    recipients.AddRange(validContacts.Select(c => new RecipientItemDto
                    {
                        ContactId = c.Id,
                        MobileNumber = c.MobileNumber,
                        FullName = c.FullName
                    }));
                }
                else if (selectDto.SelectionType == MessageSelectionTypes.Individual)
                {
                    // در حالت Individual، باید شماره موبایل وارد شود
                    if (selectDto.MobileNumbers == null || !selectDto.MobileNumbers.Any())
                    {
                        return ApiResponse<RecipientListResponseDto>.BadRequest("در حالت ارسال تکی، باید حداقل یک شماره موبایل وارد شود");
                    }

                    // در حالت Individual، fullNames الزامی است و باید با mobileNumbers هم‌اندازه باشد
                    if (selectDto.FullNames == null || selectDto.FullNames.Count != selectDto.MobileNumbers.Count)
                    {
                        return ApiResponse<RecipientListResponseDto>.BadRequest("در حالت ارسال تکی، باید برای هر شماره موبایل یک نام کامل وارد شود");
                    }

                    // پیدا کردن یا ایجاد دفترچه پیش‌فرض برای مخاطبین تکی
                    const string defaultNotebookName = "مخاطبین تکی";
                    var defaultNotebook = await _context.ContactNotebooks
                        .FirstOrDefaultAsync(n => n.UserId == userId && n.Name == defaultNotebookName && !n.IsDeleted);

                    if (defaultNotebook == null)
                    {
                        // ایجاد دفترچه جدید
                        defaultNotebook = new ContactNotebook
                        {
                            UserId = userId,
                            Name = defaultNotebookName,
                            Description = "دفترچه خودکار برای مخاطبین تکی",
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        };

                        await _context.ContactNotebooks.AddAsync(defaultNotebook);
                        await _context.SaveChangesAsync();

                        _logger.LogInformation("Created default notebook for individual contacts - NotebookId: {NotebookId}, UserId: {UserId}",
                            defaultNotebook.Id, userId);
                    }

                    // پردازش هر شماره موبایل و ایجاد دیکشنری برای نگاشت شماره موبایل به نام کامل
                    var mobileNumberList = selectDto.MobileNumbers
                        .Where(mn => !string.IsNullOrWhiteSpace(mn))
                        .Select(mn => mn.Trim())
                        .ToList();

                    // ایجاد دیکشنری برای نگاشت شماره موبایل به نام کامل
                    // در حالت Individual، fullNames تضمینی هم‌اندازه با mobileNumbers است
                    var mobileToFullNameMap = new Dictionary<string, string?>();
                    for (int i = 0; i < mobileNumberList.Count; i++)
                    {
                        var fullName = !string.IsNullOrWhiteSpace(selectDto.FullNames[i])
                            ? selectDto.FullNames[i].Trim()
                            : null;
                        mobileToFullNameMap[mobileNumberList[i]] = fullName;
                    }

                    // حذف تکراری‌ها از لیست شماره موبایل (اما نگه داشتن اولین نام کامل برای هر شماره)
                    var uniqueMobileNumbers = mobileNumberList
                        .GroupBy(mn => mn)
                        .Select(g => g.First())
                        .ToList();

                    // دریافت تمام مخاطبین موجود در دفترچه
                    var existingContacts = await _context.Contacts
                        .Where(c => c.ContactNotebookId == defaultNotebook.Id 
                            && uniqueMobileNumbers.Contains(c.MobileNumber)
                            && !c.IsDeleted)
                        .ToListAsync();

                    var existingMobileNumbers = existingContacts.Select(c => c.MobileNumber).ToHashSet();
                    var contactsToCreate = uniqueMobileNumbers
                        .Where(mn => !existingMobileNumbers.Contains(mn))
                        .ToList();

                    // به‌روزرسانی نام کامل مخاطبین موجود در صورت وجود
                    bool hasUpdates = false;
                    foreach (var contact in existingContacts)
                    {
                        if (mobileToFullNameMap.TryGetValue(contact.MobileNumber, out var fullName) 
                            && !string.IsNullOrWhiteSpace(fullName) 
                            && contact.FullName != fullName)
                        {
                            contact.FullName = fullName;
                            contact.UpdatedAt = DateTime.UtcNow;
                            hasUpdates = true;
                        }
                    }

                    if (hasUpdates)
                    {
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Updated FullNames for existing contacts in default notebook - NotebookId: {NotebookId}, UserId: {UserId}",
                            defaultNotebook.Id, userId);
                    }

                    // ایجاد مخاطبین جدید (batch)
                    if (contactsToCreate.Any())
                    {
                        try
                        {
                            var newContacts = contactsToCreate.Select(mn => new Contact
                            {
                                ContactNotebookId = defaultNotebook.Id,
                                MobileNumber = mn,
                                FullName = mobileToFullNameMap.TryGetValue(mn, out var fullName) ? fullName : null,
                                CreatedAt = DateTime.UtcNow
                            }).ToList();

                            await _context.Contacts.AddRangeAsync(newContacts);
                            await _context.SaveChangesAsync();

                            _logger.LogInformation("Created {Count} contacts in default notebook - NotebookId: {NotebookId}, UserId: {UserId}",
                                newContacts.Count, defaultNotebook.Id, userId);

                            // اضافه کردن مخاطبین جدید به لیست موجود
                            existingContacts.AddRange(newContacts);
                        }
                        catch (DbUpdateException dbEx)
                        {
                            // اگر خطای unique constraint رخ داد (مثلاً در حالت race condition)،
                            // مخاطبین موجود را پیدا می‌کنیم و به لیست اضافه می‌کنیم
                            var errorMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                            if (errorMessage.ToLower().Contains("unique") || errorMessage.ToLower().Contains("duplicate"))
                            {
                                _logger.LogWarning("خطای unique constraint در ایجاد مخاطبین. در حال جستجوی مخاطبین موجود...");
                                
                                // جستجوی مجدد مخاطبین موجود (شامل آنهایی که ممکن است توسط request دیگر ایجاد شده باشند)
                                var allExistingContacts = await _context.Contacts
                                    .Where(c => c.ContactNotebookId == defaultNotebook.Id 
                                        && contactsToCreate.Contains(c.MobileNumber)
                                        && !c.IsDeleted)
                                    .ToListAsync();
                                
                                var allExistingMobileNumbers = allExistingContacts.Select(c => c.MobileNumber).ToHashSet();
                                var stillMissing = contactsToCreate
                                    .Where(mn => !allExistingMobileNumbers.Contains(mn))
                                    .ToList();
                                
                                // اضافه کردن مخاطبین موجود به لیست
                                foreach (var contact in allExistingContacts)
                                {
                                    if (!existingContacts.Any(c => c.Id == contact.Id))
                                    {
                                        existingContacts.Add(contact);
                                    }
                                }
                                
                                // اگر هنوز مخاطبانی باقی مانده که ایجاد نشده‌اند، سعی می‌کنیم دوباره ایجاد کنیم
                                if (stillMissing.Any())
                                {
                                    _logger.LogInformation("Attempting to create {Count} remaining contacts after unique constraint error", stillMissing.Count);
                                    
                                    // ایجاد تک‌تک برای جلوگیری از خطای batch
                                    foreach (var mobileNumber in stillMissing)
                                    {
                                        try
                                        {
                                            var contact = new Contact
                                            {
                                                ContactNotebookId = defaultNotebook.Id,
                                                MobileNumber = mobileNumber,
                                                FullName = mobileToFullNameMap.TryGetValue(mobileNumber, out var fullName) ? fullName : null,
                                                CreatedAt = DateTime.UtcNow
                                            };
                                            
                                            await _context.Contacts.AddAsync(contact);
                                            await _context.SaveChangesAsync();
                                            existingContacts.Add(contact);
                                            
                                            _logger.LogInformation("Successfully created contact with mobile {MobileNumber} after retry", mobileNumber);
                                        }
                                        catch (DbUpdateException)
                                        {
                                            // اگر باز هم خطا داد، مخاطب موجود را پیدا می‌کنیم
                                            var existingContact = await _context.Contacts
                                                .Where(c => c.ContactNotebookId == defaultNotebook.Id 
                                                    && c.MobileNumber == mobileNumber 
                                                    && !c.IsDeleted)
                                                .FirstOrDefaultAsync();
                                            
                                            if (existingContact != null && !existingContacts.Any(c => c.Id == existingContact.Id))
                                            {
                                                existingContacts.Add(existingContact);
                                                _logger.LogInformation("Found existing contact with mobile {MobileNumber} after retry failure", mobileNumber);
                                            }
                                            else
                                            {
                                                _logger.LogWarning("Could not create or find contact with mobile {MobileNumber}", mobileNumber);
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // برای سایر خطاهای دیتابیس، خطا را دوباره throw می‌کنیم
                                throw;
                            }
                        }
                    }

                    // اضافه کردن همه مخاطبین به لیست گیرندگان
                    recipients.AddRange(existingContacts.Select(c => new RecipientItemDto
                    {
                        ContactId = c.Id,
                        MobileNumber = c.MobileNumber,
                        FullName = c.FullName
                    }));
                }

                if (!recipients.Any())
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<RecipientListResponseDto>.BadRequest(
                        "هیچ گیرنده‌ای انتخاب نشد. نوع انتخاب یا پارامترهای ورودی را بررسی کنید",
                        errorCode: ErrorCodes.ValidationFailed);
                }

                // حذف تکراری‌ها بر اساس شماره موبایل
                recipients = recipients
                    .GroupBy(r => r.MobileNumber)
                    .Select(g => g.First())
                    .ToList();

                var response = new RecipientListResponseDto
                {
                    Recipients = recipients,
                    TotalCount = recipients.Count
                };

                // ذخیره Session (MessageId همیشه وجود دارد) - در همان Transaction اصلی
                    // بررسی وجود پیام
                    var message = await _messageRepository.GetByIdAsync(selectDto.MessageId);
                    if (message == null || message.UserId != userId)
                    {
                    await transaction.RollbackAsync();
                        return ApiResponse<RecipientListResponseDto>.BadRequest(
                            "پیام یافت نشد یا شما مجاز به دسترسی به این پیام نیستید");
                    }

                    // بررسی وجود Session فعال قبلی برای این MessageId
                    var existingSession = await _sessionRepository.GetActiveSessionByMessageIdAsync(
                        selectDto.MessageId, userId);

                    // تبدیل SelectRecipientsDto به JSON
                    var selectionCriteriaJson = JsonSerializer.Serialize(selectDto);
                    
                    // تبدیل لیست گیرندگان به JSON
                    var recipientsJson = JsonSerializer.Serialize(recipients);

                    MessageSession session;
                    if (existingSession != null)
                    {
                    // به‌روزرسانی Session موجود (با Optimistic Concurrency)
                        existingSession.SelectionCriteria = selectionCriteriaJson;
                        existingSession.RecipientsJson = recipientsJson;
                        existingSession.IsUsed = false;
                        existingSession.ExpiresAt = DateTime.UtcNow.AddHours(24); // 24 ساعت
                        existingSession.UpdatedAt = DateTime.UtcNow;
                        
                        session = await _sessionRepository.UpdateAsync(existingSession);
                        _logger.LogInformation("Updated MessageSession - SessionId: {SessionId}, MessageId: {MessageId}, UserId: {UserId}", 
                            session.Id, selectDto.MessageId, userId);
                    }
                    else
                    {
                        // ایجاد Session جدید
                        session = new MessageSession
                        {
                            MessageId = selectDto.MessageId,
                            UserId = userId,
                            SelectionCriteria = selectionCriteriaJson,
                            RecipientsJson = recipientsJson,
                            IsUsed = false,
                            ExpiresAt = DateTime.UtcNow.AddHours(24), // 24 ساعت
                            CreatedAt = DateTime.UtcNow
                        };

                        session = await _sessionRepository.AddAsync(session);
                        _logger.LogInformation("Created MessageSession - SessionId: {SessionId}, MessageId: {MessageId}, UserId: {UserId}", 
                            session.Id, selectDto.MessageId, userId);
                    }

                await transaction.CommitAsync();

                    response.SessionId = session.Id;
                    _logger.LogInformation("Session saved successfully - SessionId: {SessionId}, MessageId: {MessageId}, UserId: {UserId}", 
                        session.Id, selectDto.MessageId, userId);

                return ApiResponse<RecipientListResponseDto>.CreateSuccess(response);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(ex, "Concurrency conflict while saving Session - MessageId: {MessageId}, UserId: {UserId}", 
                    selectDto.MessageId, userId);
                // Retry: خواندن مجدد Session و تلاش دوباره
                // اگر recipients خالی است، باید دوباره محاسبه شود
                if (!recipients.Any())
                {
                    // در صورت نیاز می‌توانیم recipients را از Session قبلی بخوانیم
                    var existingSession = await _sessionRepository.GetActiveSessionByMessageIdAsync(selectDto.MessageId, userId);
                    if (existingSession != null && !string.IsNullOrEmpty(existingSession.RecipientsJson))
                    {
                        try
                        {
                            recipients = JsonSerializer.Deserialize<List<RecipientItemDto>>(existingSession.RecipientsJson) 
                                ?? new List<RecipientItemDto>();
                        }
                        catch
                        {
                            // اگر نتوانستیم deserialize کنیم، لیست خالی می‌ماند
                        }
                    }
                }
                
                try
                {
                    var retrySession = await _sessionRepository.GetActiveSessionByMessageIdAsync(selectDto.MessageId, userId);
                    if (retrySession != null)
                    {
                        retrySession.SelectionCriteria = JsonSerializer.Serialize(selectDto);
                        retrySession.RecipientsJson = JsonSerializer.Serialize(recipients);
                        retrySession.IsUsed = false;
                        retrySession.ExpiresAt = DateTime.UtcNow.AddHours(24);
                        retrySession.UpdatedAt = DateTime.UtcNow;
                        
                        using var retryTransaction = await _context.Database.BeginTransactionAsync();
                        try
                        {
                            await _sessionRepository.UpdateAsync(retrySession);
                            await retryTransaction.CommitAsync();
                            
                            var retryResponse = new RecipientListResponseDto
                            {
                                Recipients = recipients,
                                TotalCount = recipients.Count,
                                SessionId = retrySession.Id
                            };
                            return ApiResponse<RecipientListResponseDto>.CreateSuccess(retryResponse);
                        }
                        catch
                        {
                            await retryTransaction.RollbackAsync();
                            throw;
                        }
                    }
                }
                catch (Exception retryEx)
                {
                    _logger.LogError(retryEx, "Error in retry after concurrency conflict - MessageId: {MessageId}", selectDto.MessageId);
                }
                return ApiResponse<RecipientListResponseDto>.InternalServerError(
                    "خطا در ذخیره‌سازی Session به دلیل تداخل. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error selecting recipients for user: {UserId}, Error: {Error}", userId, ex.Message);
                _logger.LogError(ex, "Stack trace: {StackTrace}", ex.StackTrace);
                return ApiResponse<RecipientListResponseDto>.InternalServerError("خطا در انتخاب گیرندگان");
            }
        }

        #endregion

        #region Direct Send Operations (Without Campaign)

        /// <summary>
        /// ارسال مستقیم پیام (بدون ایجاد کمپین) - برای استفاده در CalculateCampaignSummaryAsync و Background Services
        /// </summary>
        public async Task<ApiResponse<DirectSendResultDto>> SendDirectMessageAsync(int userId, int messageId, SendDirectMessageDto sendDto, MessageSession? session = null, bool bypassAdminApproval = false)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                _logger.LogInformation("=== شروع ارسال پیام مستقیم ===");
                _logger.LogInformation("MessageId: {MessageId}, UserId: {UserId}, SendType: {SendType}, StartTime (UTC): {StartTime}", 
                    messageId, userId, sendDto.SendType, startTime);
                
                // بررسی وجود پیام
                var message = await _messageRepository.GetByIdAsync(messageId);
                if (message == null || message.UserId != userId)
                {
                    return ApiResponse<DirectSendResultDto>.NotFound("پیام یافت نشد");
                }

                if (!bypassAdminApproval)
                {
                    var templateError = await ValidateTemplateApprovalForMessageAsync(message);
                    if (templateError != null)
                        return ApiResponse<DirectSendResultDto>.BadRequest(templateError);
                }
                
                _logger.LogInformation("پیام یافت شد - Title: {Title}, Content Length: {ContentLength}, Parts: {Parts}", 
                    message.Title, message.Content?.Length ?? 0, message.PartsCount);

                // اگر Session پاس داده نشده باشد، از دیتابیس بخوانیم
                if (session == null)
                {
                    _logger.LogInformation("Session not passed to SendDirectMessageAsync, reading from database - MessageId: {MessageId}, UserId: {UserId}", 
                        messageId, userId);
                    
                    // بررسی Session - بدون شرط IsUsed چون ممکن است قبلاً IsUsed = true شده باشد
                    // (مثلاً در ConfirmAndSendMessageAsync که Session را علامت‌گذاری می‌کند)
                    session = await _sessionRepository.GetByMessageIdAsync(messageId, userId);
                    if (session == null)
                    {
                        _logger.LogWarning("No Session found in SendDirectMessageAsync - MessageId: {MessageId}, UserId: {UserId}", 
                            messageId, userId);
                    return ApiResponse<DirectSendResultDto>.BadRequest(
                        "هیچ گیرنده‌ای برای این پیام انتخاب نشده است. لطفاً ابتدا گیرندگان را انتخاب کنید.");
                    }
                    
                    _logger.LogInformation("Session found in SendDirectMessageAsync - SessionId: {SessionId}, IsUsed: {IsUsed}, HasRecipientsJson: {HasRecipientsJson}, RecipientsJsonLength: {Length}", 
                        session.Id, session.IsUsed, !string.IsNullOrEmpty(session.RecipientsJson), session.RecipientsJson?.Length ?? 0);
                    
                    if (string.IsNullOrEmpty(session.RecipientsJson))
                    {
                        _logger.LogWarning("Session has empty RecipientsJson in SendDirectMessageAsync - SessionId: {SessionId}, MessageId: {MessageId}", 
                            session.Id, messageId);
                        return ApiResponse<DirectSendResultDto>.BadRequest(
                            "لیست گیرندگان در Session خالی است. لطفاً ابتدا گیرندگان را انتخاب کنید.");
                    }
                    
                    // بررسی انقضا
                    if (session.ExpiresAt.HasValue && session.ExpiresAt.Value <= DateTime.UtcNow)
                    {
                        _logger.LogWarning("Session expired in SendDirectMessageAsync - SessionId: {SessionId}, ExpiresAt: {ExpiresAt}, Now: {Now}", 
                            session.Id, session.ExpiresAt, DateTime.UtcNow);
                        return ApiResponse<DirectSendResultDto>.BadRequest(
                            "Session منقضی شده است. لطفاً گیرندگان را دوباره انتخاب کنید.");
                    }
                }
                else
                {
                    // اگر Session پاس داده شده، فقط بررسی کنیم که RecipientsJson خالی نباشد
                    _logger.LogInformation("Using passed Session in SendDirectMessageAsync - SessionId: {SessionId}, IsUsed: {IsUsed}, HasRecipientsJson: {HasRecipientsJson}, RecipientsJsonLength: {Length}", 
                        session.Id, session.IsUsed, !string.IsNullOrEmpty(session.RecipientsJson), session.RecipientsJson?.Length ?? 0);
                    
                    if (string.IsNullOrEmpty(session.RecipientsJson))
                    {
                        _logger.LogError("Passed Session has empty RecipientsJson - SessionId: {SessionId}, MessageId: {MessageId}, SelectionCriteria: {SelectionCriteria}", 
                            session.Id, messageId, session.SelectionCriteria);
                        return ApiResponse<DirectSendResultDto>.BadRequest(
                            "لیست گیرندگان در Session خالی است.");
                    }
                }

                // بررسی نوع ارسال
                if (sendDto.SendType == CampaignSendType.Scheduled && !sendDto.ScheduledAt.HasValue)
                {
                    return ApiResponse<DirectSendResultDto>.BadRequest("برای ارسال زمان‌بندی شده، تاریخ و زمان ارسال باید مشخص شود");
                }

                if (sendDto.SendType == CampaignSendType.Scheduled && sendDto.ScheduledAt.HasValue && sendDto.ScheduledAt.Value <= DateTime.UtcNow)
                {
                    return ApiResponse<DirectSendResultDto>.BadRequest("تاریخ و زمان ارسال باید در آینده باشد");
                }

                // خواندن گیرندگان از Session با اعتبارسنجی JSON (مشکل 4.2)
                List<RecipientItemDto> recipients;
                try
                {
                    if (string.IsNullOrWhiteSpace(session.RecipientsJson))
                    {
                        return ApiResponse<DirectSendResultDto>.BadRequest("لیست گیرندگان خالی است");
                    }

                    // اعتبارسنجی JSON قبل از Deserialize
                    try
                    {
                        using var doc = JsonDocument.Parse(session.RecipientsJson);
                        if (doc.RootElement.ValueKind != JsonValueKind.Array)
                        {
                            return ApiResponse<DirectSendResultDto>.BadRequest("فرمت JSON لیست گیرندگان نامعتبر است");
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, "Invalid JSON format in RecipientsJson - SessionId: {SessionId}", session.Id);
                        return ApiResponse<DirectSendResultDto>.BadRequest("فرمت JSON لیست گیرندگان نامعتبر است");
                    }

                    recipients = JsonSerializer.Deserialize<List<RecipientItemDto>>(session.RecipientsJson) 
                        ?? new List<RecipientItemDto>();
                    
                    // اعتبارسنجی ساختار داده
                    if (recipients.Any(r => string.IsNullOrWhiteSpace(r.MobileNumber)))
                    {
                        _logger.LogWarning("Some recipients have invalid mobile numbers - SessionId: {SessionId}", session.Id);
                        recipients = recipients.Where(r => !string.IsNullOrWhiteSpace(r.MobileNumber)).ToList();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deserializing recipients from Session - SessionId: {SessionId}", session.Id);
                    return ApiResponse<DirectSendResultDto>.InternalServerError("خطا در خواندن لیست گیرندگان");
                }

                if (!recipients.Any())
                {
                    return ApiResponse<DirectSendResultDto>.BadRequest("هیچ گیرنده‌ای برای ارسال وجود ندارد");
                }

                if (!bypassAdminApproval)
                {
                    var existingPending = await _context.SmsApprovalRequests
                        .AnyAsync(r => r.MessageId == messageId
                            && r.UserId == userId
                            && r.MessageSessionId == session.Id
                            && r.Status == AdminApprovalStatuses.Pending
                            && !r.IsDeleted);

                    if (existingPending)
                    {
                        return ApiResponse<DirectSendResultDto>.CreateSuccess(
                            new DirectSendResultDto(),
                            "درخواست ارسال در صف تأیید ادمین قرار دارد",
                            202);
                    }

                    await UpsertDirectMessageApprovalRequestAsync(message, session, sendDto, recipients.Count);
                    return ApiResponse<DirectSendResultDto>.CreateSuccess(
                        new DirectSendResultDto(),
                        "درخواست ارسال در صف تأیید ادمین قرار گرفت",
                        202);
                }

                // اعتبارسنجی گیرندگان قبل از ارسال (مشکل 4.1)
                // بررسی اینکه ContactId ها هنوز معتبر هستند و حذف نشده‌اند
                var contactIds = recipients
                    .Where(r => r.ContactId.HasValue)
                    .Select(r => r.ContactId!.Value)
                    .Distinct()
                    .ToList();

                if (contactIds.Any())
                {
                    // یک Query برای بررسی اعتبار همه Contact ها
                    var validContactIds = await _context.Contacts
                        .Where(c => contactIds.Contains(c.Id) 
                            && !c.IsDeleted
                            && c.ContactNotebook.UserId == userId
                            && !c.ContactNotebook.IsDeleted)
                        .Select(c => c.Id)
                        .ToHashSetAsync();

                    // حذف گیرندگانی که ContactId آن‌ها معتبر نیست
                    var invalidCount = recipients.RemoveAll(r => 
                        r.ContactId.HasValue && !validContactIds.Contains(r.ContactId.Value));
                    
                    if (invalidCount > 0)
                    {
                        _logger.LogWarning("Removed {Count} invalid recipients - MessageId: {MessageId}, UserId: {UserId}", 
                            invalidCount, messageId, userId);
                    }
                }

                if (!recipients.Any())
                {
                    return ApiResponse<DirectSendResultDto>.BadRequest(
                        "بعد از اعتبارسنجی، هیچ گیرنده معتبری باقی نمانده است. لطفاً گیرندگان را دوباره انتخاب کنید.");
                }

                // اعمال فیلترها
                // فیلتر بر اساس جلوگیری از ارسال تکراری
                if (sendDto.PreventDuplicate)
                {
                    recipients = await FilterDuplicateRecipientsAsync(recipients, sendDto.DuplicatePreventionHours, userId);
                }

                // فیلتر بر اساس تگ‌ها
                if (sendDto.SendToSpecificTags && sendDto.SelectedTagIds != null)
                {
                    var validTagIds = sendDto.SelectedTagIds.Where(id => id > 0).ToList();
                    if (validTagIds.Any())
                    {
                        recipients = await FilterByTagsAsync(recipients, validTagIds, userId);
                    }
                }

                if (!recipients.Any())
                {
                    return ApiResponse<DirectSendResultDto>.BadRequest(
                        "بعد از اعمال فیلترها، هیچ گیرنده‌ای باقی نمانده است");
                }

                // محاسبه مجدد هزینه (مشکل 4.3) - با تعداد گیرندگان به‌روزرسانی شده
                var recipientsCount = recipients.Count;
                var partsCount = message.PartsCount;
                var totalCost = recipientsCount * partsCount * _costPerPart;

                // بررسی موجودی کیف پول
                // غیرفعال شده - دیگر کیف پول چک نمی‌شود
                /*
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    return ApiResponse<DirectSendResultDto>.NotFound("کاربر یافت نشد");
                }

                var disableWalletCheck = IsWalletCheckDisabled();
                if (!disableWalletCheck && user.WalletBalance < totalCost)
                {
                    return ApiResponse<DirectSendResultDto>.BadRequest("موجودی کیف پول کافی نیست");
                }
                */

                // اگر ارسال زمان‌بندی شده است، باید یک Background Job ایجاد شود
                // فعلاً فقط ارسال فوری را پشتیبانی می‌کنیم
                if (sendDto.SendType == CampaignSendType.Scheduled)
                {
                    return ApiResponse<DirectSendResultDto>.BadRequest(
                        "ارسال زمان‌بندی شده برای پیام عادی در حال حاضر پشتیبانی نمی‌شود. لطفاً از ارسال فوری استفاده کنید.");
                }

                // ارسال پیام‌ها با Compensation Pattern (مشکل 6.1)
                int sentCount = 0;
                int failedCount = 0;
                decimal actualCost = 0;
                var failedNumbers = new List<string>();
                // غیرفعال شده - دیگر استفاده نمی‌شوند
                // decimal deductedAmount = 0; // برای Compensation در صورت خطا
                // bool walletDeducted = false; // برای ردیابی اینکه آیا موجودی کسر شده است
                
                _logger.LogInformation("=== شروع ارسال به {RecipientsCount} گیرنده ===", recipients.Count);
                _logger.LogInformation("گیرندگان: {Recipients}", 
                    string.Join(", ", recipients.Select(r => $"{r.FullName ?? "بدون نام"} ({r.MobileNumber})").Take(10)));

                try
                {
                var recipientIndex = 0;
                foreach (var recipient in recipients)
                {
                    recipientIndex++;
                    try
                    {
                        _logger.LogInformation("📤 [{Index}/{Total}] در حال ارسال به گیرنده: {FullName} ({Mobile})", 
                            recipientIndex, recipients.Count, recipient.FullName ?? "بدون نام", recipient.MobileNumber);
                        
                        // آماده‌سازی متن پیام (شخصی‌سازی در صورت نیاز)
                        string messageContent = message.Content ?? string.Empty;

                        if (message.IsPersonalized && recipient.ContactId.HasValue)
                        {
                            var contact = await _contactRepository.GetByIdAsync(recipient.ContactId.Value);
                            if (contact != null)
                            {
                                messageContent = await PersonalizeMessageWithContactAsync(message.Content ?? string.Empty, contact);
                                _logger.LogInformation("پیام شخصی‌سازی شد برای {FullName} ({Mobile})", 
                                    recipient.FullName ?? "بدون نام", recipient.MobileNumber);
                            }
                        }

                        // اضافه کردن 'لغو11' در انتهای پیامک (الزام API)
                        if (!messageContent.TrimEnd().EndsWith("لغو11"))
                        {
                            messageContent = $"{messageContent.TrimEnd()}\nلغو11";
                            _logger.LogDebug("متن 'لغو11' به پیام اضافه شد برای {Mobile}", recipient.MobileNumber);
                        }

                        // محاسبه دقیق تعداد پارت‌ها برای پیام نهایی (با در نظر گیری شخصی‌سازی و 'لغو11')
                        int actualPartsCount;
                        try
                        {
                            actualPartsCount = SmsPartsCalculator.CalculateParts(messageContent);
                        }
                        catch (ArgumentException ex)
                        {
                            // پیام بیش از 10 صفحه است
                            failedCount++;
                            failedNumbers.Add(recipient.MobileNumber);
                            _logger.LogWarning("Message exceeds max pages for {Mobile} - MessageId: {MessageId}, Error: {Error}", 
                                recipient.MobileNumber, messageId, ex.Message);
                            continue;
                        }

                        // ارسال پیامک با Retry Mechanism (مشکل 6.2)
                        var smsRequest = new DTOs.Sms.SendSmsRequestDto
                        {
                            Mobile = recipient.MobileNumber,
                            Message = messageContent
                        };

                        var smsSendStartTime = DateTime.UtcNow;
                        var smsResult = await SendSmsWithRetryAsync(smsRequest, maxRetries: 3);
                        var smsSendEndTime = DateTime.UtcNow;
                        var smsSendDuration = (smsSendEndTime - smsSendStartTime).TotalMilliseconds;

                        // Sid > 0 یعنی پیام ارسال شده (حتی اگر Status = 0 باشد)
                        bool isSuccess = smsResult.Success && smsResult.Data != null && 
                            (smsResult.Data.Sid > 0 || smsResult.Data.Status > 0);

                        if (isSuccess)
                        {
                            // ارسال موفق
                            sentCount++;
                            actualCost += _costPerPart * actualPartsCount;

                            await _deliveryTracking.TrackSuccessfulSendAsync(new SmsDeliveryTrackRequestDto
                            {
                                UserId = userId,
                                SourceModule = SmsSourceModules.MessageDirect,
                                SourceEntityId = messageId,
                                SourceEntityLabel = message.Title ?? $"پیام #{messageId}",
                                Mobile = recipient.MobileNumber,
                                Sid = smsResult.Data!.Sid,
                                SentAt = smsSendEndTime
                            });
                            
                            _logger.LogInformation("✅ SMS ارسال شد - گیرنده: {FullName} ({Mobile}), MessageId: {MessageId}, Sid: {Sid}, زمان ارسال (UTC): {SendTime}, مدت زمان: {Duration}ms", 
                                recipient.FullName ?? "بدون نام", recipient.MobileNumber, messageId, smsResult.Data!.Sid, smsSendEndTime, smsSendDuration);
                        }
                        else
                        {
                            // ارسال ناموفق (بعد از Retry)
                            failedCount++;
                            failedNumbers.Add(recipient.MobileNumber);
                            
                            _logger.LogWarning("❌ SMS ارسال نشد - گیرنده: {FullName} ({Mobile}), MessageId: {MessageId}, زمان تلاش (UTC): {SendTime}, مدت زمان: {Duration}ms, خطا: {Error}", 
                                recipient.FullName ?? "بدون نام", recipient.MobileNumber, messageId, smsSendEndTime, smsSendDuration, 
                                smsResult.Data?.Message ?? smsResult.Message ?? "خطا در ارسال پیامک");
                        }
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        failedNumbers.Add(recipient.MobileNumber);
                        
                        _logger.LogError(ex, "❌ خطا در ارسال SMS به گیرنده: {FullName} ({Mobile}) برای MessageId: {MessageId}, زمان خطا (UTC): {ErrorTime}", 
                            recipient.FullName ?? "بدون نام", recipient.MobileNumber, messageId, DateTime.UtcNow);
                    }
                }

                    // کسر هزینه از کیف پول با Transaction و Lock (فقط برای پیام‌های موفق)
                // غیرفعال شده - دیگر از کیف پول کسر نمی‌شود
                /*
                if (actualCost > 0 && !disableWalletCheck)
                {
                        using var walletTransaction = await _context.Database.BeginTransactionAsync();
                        try
                        {
                            // خواندن مجدد User با Lock برای بررسی موجودی
                            var userWithLock = await _context.Users
                                .FromSqlRaw("SELECT * FROM Users WITH (UPDLOCK, ROWLOCK) WHERE Id = {0}", userId)
                                .FirstOrDefaultAsync();

                            if (userWithLock == null)
                            {
                                await walletTransaction.RollbackAsync();
                                return ApiResponse<DirectSendResultDto>.NotFound("کاربر یافت نشد");
                            }

                            // بررسی مجدد موجودی (با Lock)
                            if (userWithLock.WalletBalance < actualCost)
                            {
                                await walletTransaction.RollbackAsync();
                                return ApiResponse<DirectSendResultDto>.BadRequest("موجودی کیف پول کافی نیست");
                            }

                            var balanceBefore = userWithLock.WalletBalance;
                            userWithLock.WalletBalance -= actualCost;
                            deductedAmount = actualCost;
                            walletDeducted = true;
                            await _context.SaveChangesAsync();

                    // ثبت تراکنش کیف پول
                            var walletTransactionRecord = new Models.WalletTransaction
                    {
                        UserId = userId,
                        Amount = -actualCost,
                        BalanceBefore = balanceBefore,
                                BalanceAfter = userWithLock.WalletBalance,
                        TransactionType = "SmsSend",
                        Title = "ارسال پیام مستقیم",
                        Description = $"ارسال پیام مستقیم - پیام ID: {messageId}",
                        CreatedAt = DateTime.UtcNow
                    };
                            await _context.WalletTransactions.AddAsync(walletTransactionRecord);
                    await _context.SaveChangesAsync();

                            await walletTransaction.CommitAsync();

                    _logger.LogInformation("Wallet balance deducted - UserId: {UserId}, Amount: {Amount}, Remaining: {Remaining}", 
                                userId, actualCost, userWithLock.WalletBalance);
                        }
                        catch (Exception ex)
                        {
                            await walletTransaction.RollbackAsync();
                            _logger.LogError(ex, "Error deducting wallet balance - UserId: {UserId}, Amount: {Amount}", userId, actualCost);
                            // در صورت خطا در کسر موجودی، خطا را برمی‌گردانیم
                            throw;
                        }
                }
                else if (actualCost > 0 && disableWalletCheck)
                {
                    _logger.LogInformation("Wallet check disabled - UserId: {UserId}, Cost: {Cost} (not deducted)", 
                        userId, actualCost);
                }
                */
                _logger.LogInformation("Wallet check disabled - UserId: {UserId}, Cost: {Cost} (not deducted)", 
                    userId, actualCost);
                }
                catch (Exception ex)
                {
                    // Compensation Pattern: برگشت موجودی در صورت خطا (مشکل 6.1)
                    // غیرفعال شده - دیگر موجودی برگشت داده نمی‌شود
                    /*
                    if (walletDeducted && deductedAmount > 0 && !disableWalletCheck)
                    {
                        try
                        {
                            _logger.LogWarning("Compensating wallet balance - UserId: {UserId}, Amount: {Amount}", userId, deductedAmount);
                            
                            using var compensationTransaction = await _context.Database.BeginTransactionAsync();
                            try
                            {
                                var userForCompensation = await _context.Users
                                    .FromSqlRaw("SELECT * FROM Users WITH (UPDLOCK, ROWLOCK) WHERE Id = {0}", userId)
                                    .FirstOrDefaultAsync();

                                if (userForCompensation != null)
                                {
                                    var balanceBefore = userForCompensation.WalletBalance;
                                    userForCompensation.WalletBalance += deductedAmount;
                                    await _context.SaveChangesAsync();

                                    // ثبت تراکنش Compensation
                                    var compensationTransactionRecord = new Models.WalletTransaction
                                    {
                                        UserId = userId,
                                        Amount = deductedAmount,
                                        BalanceBefore = balanceBefore,
                                        BalanceAfter = userForCompensation.WalletBalance,
                                        TransactionType = "SmsSendCompensation",
                                        Title = "برگشت موجودی - خطا در ارسال",
                                        Description = $"برگشت موجودی به دلیل خطا در ارسال پیام - پیام ID: {messageId}",
                                        CreatedAt = DateTime.UtcNow
                                    };
                                    await _context.WalletTransactions.AddAsync(compensationTransactionRecord);
                                    await _context.SaveChangesAsync();

                                    await compensationTransaction.CommitAsync();

                                    _logger.LogInformation("Wallet balance compensated - UserId: {UserId}, Amount: {Amount}, New Balance: {Balance}", 
                                        userId, deductedAmount, userForCompensation.WalletBalance);
                                }
                            }
                            catch (Exception compensationEx)
                            {
                                await compensationTransaction.RollbackAsync();
                                _logger.LogError(compensationEx, "Error compensating wallet balance - UserId: {UserId}, Amount: {Amount}", 
                                    userId, deductedAmount);
                                // خطا در Compensation را لاگ می‌کنیم اما خطای اصلی را throw می‌کنیم
                            }
                        }
                        catch (Exception compensationEx)
                        {
                            _logger.LogError(compensationEx, "Critical error in compensation - UserId: {UserId}", userId);
                        }
                    }
                    */

                    // Re-throw خطای اصلی
                    _logger.LogError(ex, "Error during SMS sending - MessageId: {MessageId}, UserId: {UserId}", messageId, userId);
                    return ApiResponse<DirectSendResultDto>.InternalServerError(
                        $"خطا در ارسال پیام.");
                }

                // Session قبلاً در ConfirmAndSendMessageAsync علامت‌گذاری شده است

                // به‌روزرسانی وضعیت پیام
                message.Status = "Sent";
                message.UpdatedAt = DateTime.UtcNow;
                await _messageRepository.UpdateAsync(message);

                var result = new DirectSendResultDto
                {
                    SentCount = sentCount,
                    FailedCount = failedCount,
                    TotalCost = actualCost,
                    FailedNumbers = failedNumbers.Any() ? failedNumbers : null
                };

                var messageText = $"پیام‌ها با موفقیت ارسال شد ({sentCount} ارسال موفق، {failedCount} ناموفق)";
                if (failedCount > 0)
                {
                    messageText += $". شماره‌های ناموفق: {string.Join(", ", failedNumbers.Take(5))}";
                    if (failedNumbers.Count > 5)
                    {
                        messageText += $" و {failedNumbers.Count - 5} شماره دیگر";
                    }
                }

                var endTime = DateTime.UtcNow;
                var totalDuration = (endTime - startTime).TotalSeconds;
                
                _logger.LogInformation("=== پایان ارسال پیام مستقیم ===");
                _logger.LogInformation("MessageId: {MessageId}, UserId: {UserId}, زمان شروع (UTC): {StartTime}, زمان پایان (UTC): {EndTime}, مدت کل: {Duration}ثانیه", 
                    messageId, userId, startTime, endTime, totalDuration);
                _logger.LogInformation("نتایج: ✅ ارسال موفق: {SentCount}, ❌ ارسال ناموفق: {FailedCount}, 💰 هزینه کل: {Cost} تومان", 
                    sentCount, failedCount, actualCost);
                
                if (failedNumbers.Any())
                {
                    _logger.LogWarning("شماره‌های ناموفق: {FailedNumbers}", string.Join(", ", failedNumbers));
                }

                return ApiResponse<DirectSendResultDto>.CreateSuccess(result, messageText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending direct message - MessageId: {MessageId}, UserId: {UserId}", messageId, userId);
                return ApiResponse<DirectSendResultDto>.InternalServerError("خطا در ارسال پیام");
            }
        }

        #endregion

        #region Report Operations

        public async Task<ApiResponse<TodayReportDto>> GetTodayReportAsync(int userId)
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                var tomorrow = today.AddDays(1);

                var allCampaigns = await _campaignRepository.GetByUserIdAsync(userId);

                var sentToday = allCampaigns
                    .Where(c => c.SentAt.HasValue && c.SentAt.Value >= today && c.SentAt.Value < tomorrow && c.Status == "Sent")
                    .Sum(c => c.SentCount);

                var scheduledTomorrow = allCampaigns
                    .Where(c => c.SendType == CampaignSendType.Scheduled.ToString()
                        && c.ScheduledAt.HasValue
                        && c.ScheduledAt.Value >= tomorrow && c.ScheduledAt.Value < tomorrow.AddDays(1)
                        && (c.Status == "Pending" || c.Status == "Draft"))
                    .Count();

                var report = new TodayReportDto
                {
                    SentTodayCount = sentToday,
                    ScheduledTomorrowCount = scheduledTomorrow
                };

                return ApiResponse<TodayReportDto>.CreateSuccess(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting today report for user: {UserId}", userId);
                return ApiResponse<TodayReportDto>.InternalServerError("خطا در دریافت گزارش امروز");
            }
        }

        public async Task<ApiResponse<List<LatestCampaignsDto>>> GetLatestCampaignsAsync(int userId, int count = 5)
        {
            try
            {
                // دریافت کمپین‌های کاربر که از پیام‌های اتوماتیک ایجاد شده‌اند
                // و بررسی می‌کنیم که AutomatedMessage مربوطه حذف نشده باشد
                var campaigns = await _context.MessageCampaigns
                    .Include(c => c.AutomatedMessage)
                    .Where(c => c.UserId == userId 
                        && !c.IsDeleted 
                        && c.AutomatedMessageId.HasValue 
                        && c.AutomatedMessage != null 
                        && !c.AutomatedMessage.IsDeleted) // بررسی اینکه AutomatedMessage حذف نشده باشد
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(count)
                    .ToListAsync();

                var latestCampaigns = campaigns.Select(c => new LatestCampaignsDto
                {
                    Id = c.Id,
                    // Title بر اساس AutomationType تنظیم می‌شود
                    Title = GetCampaignTitleFromAutomationType(c),
                    SentAt = c.SentAt,
                    ScheduledAt = c.ScheduledAt,
                    // برای کمپین‌های Automated، Status از فیلد Status خود کمپین خوانده می‌شود
                    Status = c.Status == "Sent" ? "Sent" 
                        : (c.Status == "Active" ? "Active" 
                        : (c.SendType == "Scheduled" ? "Scheduled" 
                        : "Draft")),
                    IsActive = c.IsActive
                }).ToList();

                return ApiResponse<List<LatestCampaignsDto>>.CreateSuccess(latestCampaigns);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting latest campaigns for user: {UserId}", userId);
                return ApiResponse<List<LatestCampaignsDto>>.InternalServerError("خطا در دریافت آخرین کمپین‌ها");
            }
        }

        /// <summary>
        /// دریافت عنوان کمپین بر اساس AutomationType
        /// </summary>
        private string GetCampaignTitleFromAutomationType(MessageCampaign campaign)
        {
            // اولویت 1: Title کمپین (اگر تنظیم شده باشد)
            if (!string.IsNullOrWhiteSpace(campaign.Title))
            {
                return campaign.Title;
            }

            // اولویت 2: Title AutomatedMessage (اگر تنظیم شده باشد)
            if (campaign.AutomatedMessage != null && !string.IsNullOrWhiteSpace(campaign.AutomatedMessage.Title))
            {
                return campaign.AutomatedMessage.Title;
            }

            // اولویت 3: عنوان فارسی بر اساس AutomationType
            if (campaign.AutomatedMessage != null && !string.IsNullOrWhiteSpace(campaign.AutomatedMessage.AutomationType))
            {
                return GetAutomationTypePersianTitle(campaign.AutomatedMessage.AutomationType);
            }

            // پیش‌فرض
            return $"کمپین #{campaign.Id}";
        }

        /// <summary>
        /// تبدیل AutomationType به عنوان فارسی
        /// </summary>
        private string GetAutomationTypePersianTitle(string automationType)
        {
            return automationType switch
            {
                "Birthday" => "تبریک تولد",
                "CashbackExpiry" => "یادآوری انقضای کش‌بک",
                "Welcome" => "پیام خوش‌آمدگویی",
                "PurchaseReminder" => "یادآوری خرید",
                "SpecialOccasion" => "مناسبت‌های خاص",
                "Custom" => "اتوماسیون سفارشی",
                _ => "پیام خودکار"
            };
        }

        /// <summary>
        /// دریافت گزارش جامع شامل آمار کمپین‌ها، عملکرد کارگران، و بررسی داده‌ها
        /// </summary>
        public async Task<ApiResponse<ComprehensiveReportDto>> GetComprehensiveReportAsync(int userId)
        {
            try
            {
                var report = new ComprehensiveReportDto();
                var now = DateTime.UtcNow;
                var todayStart = now.Date; // Start of today in UTC
                var todayEnd = todayStart.AddDays(1); // Start of tomorrow in UTC
                var weekAgo = todayStart.AddDays(-7);
                var monthAgo = todayStart.AddMonths(-1);

                // آمار پیام‌ها
                var allMessages = await _messageRepository.GetByUserIdAsync(userId);
                var allCampaigns = await _campaignRepository.GetByUserIdAsync(userId);
                var allRecipients = await _context.MessageRecipients
                    .Where(r => r.Campaign.UserId == userId)
                    .Include(r => r.Campaign)
                    .ToListAsync();

                // شمارش ارسال‌های مستقیم از تراکنش‌های کیف پول
                var directSendsToday = await _context.WalletTransactions
                    .CountAsync(wt => wt.UserId == userId
                        && wt.TransactionType == "SmsSend"
                        && wt.Title.Contains("ارسال پیام مستقیم")
                        && wt.CreatedAt >= todayStart && wt.CreatedAt < todayEnd);

                var directSendsThisWeek = await _context.WalletTransactions
                    .CountAsync(wt => wt.UserId == userId
                        && wt.TransactionType == "SmsSend"
                        && wt.Title.Contains("ارسال پیام مستقیم")
                        && wt.CreatedAt >= weekAgo);

                var directSendsThisMonth = await _context.WalletTransactions
                    .CountAsync(wt => wt.UserId == userId
                        && wt.TransactionType == "SmsSend"
                        && wt.Title.Contains("ارسال پیام مستقیم")
                        && wt.CreatedAt >= monthAgo);

                // هزینه ارسال‌های مستقیم
                var directSendCosts = await _context.WalletTransactions
                    .Where(wt => wt.UserId == userId
                        && wt.TransactionType == "SmsSend"
                        && wt.Title.Contains("ارسال پیام مستقیم"))
                    .SumAsync(wt => -wt.Amount); // Amount is negative, so we negate it to get positive cost

                // تعداد کل ارسال‌های مستقیم
                var totalDirectSends = await _context.WalletTransactions
                    .CountAsync(wt => wt.UserId == userId
                        && wt.TransactionType == "SmsSend"
                        && wt.Title.Contains("ارسال پیام مستقیم"));

                report.MessageStatistics = new MessageStatisticsDto
                {
                    TotalMessages = allMessages.Count(),
                    SentToday = allRecipients.Count(r => r.SentAt.HasValue && r.SentAt.Value >= todayStart && r.SentAt.Value < todayEnd) + directSendsToday,
                    SentThisWeek = allRecipients.Count(r => r.SentAt.HasValue && r.SentAt.Value >= weekAgo) + directSendsThisWeek,
                    SentThisMonth = allRecipients.Count(r => r.SentAt.HasValue && r.SentAt.Value >= monthAgo) + directSendsThisMonth,
                    ScheduledCount = allCampaigns.Count(c => c.Status == "Scheduled" || c.SendType == CampaignSendType.Scheduled.ToString()),
                    FailedCount = allRecipients.Count(r => r.Status == "Failed"),
                    TotalCost = allCampaigns.Where(c => c.Status == "Sent").Sum(c => c.ActualTotalCost) + directSendCosts,
                    AverageCostPerMessage = (allRecipients.Any(r => r.Status == "Sent") || directSendsThisMonth > 0) ?
                        (allCampaigns.Where(c => c.Status == "Sent").Sum(c => c.ActualTotalCost) + directSendCosts) /
                        Math.Max(allRecipients.Count(r => r.Status == "Sent") + directSendsThisMonth, 1) : 0
                };

                // آمار کمپین‌ها
                report.CampaignStatistics = new CampaignStatisticsDto
                {
                    TotalCampaigns = allCampaigns.Count(),
                    ActiveCampaigns = allCampaigns.Count(c => c.IsActive && c.Status != "Sent" && c.Status != "Cancelled"),
                    CompletedCampaigns = allCampaigns.Count(c => c.Status == "Sent" || c.Status == "Completed"),
                    ScheduledCampaigns = allCampaigns.Count(c => c.Status == "Scheduled"),
                    FailedCampaigns = allCampaigns.Count(c => c.Status == "Failed"),
                    LastCampaignSentAt = allCampaigns.Where(c => c.SentAt.HasValue).Max(c => c.SentAt),
                    TotalRecipientsInCampaigns = allRecipients.Count,
                    TotalMessagesInCampaigns = allRecipients.Count
                };

                // عملکرد کارگران (در اینجا userId همان worker است)
                var totalRecipientsReached = allRecipients.Count(r => r.Status == "Sent") + totalDirectSends;
                var totalRecipientsAttempted = allRecipients.Count + totalDirectSends;

                report.WorkerPerformances = new List<WorkerPerformanceDto>
                {
                    new WorkerPerformanceDto
                    {
                        WorkerId = userId,
                        WorkerName = "کاربر فعلی", // می‌توانیم بعداً نام کاربر را اضافه کنیم
                        MessagesSent = report.MessageStatistics.SentToday + report.MessageStatistics.SentThisWeek + report.MessageStatistics.SentThisMonth,
                        CampaignsCreated = allCampaigns.Count(),
                        RecipientsReached = totalRecipientsReached,
                        TotalCost = report.MessageStatistics.TotalCost,
                        AverageCostPerMessage = report.MessageStatistics.AverageCostPerMessage,
                        LastActivity = allCampaigns.Any() ? allCampaigns.Max(c => c.UpdatedAt ?? c.CreatedAt) : null,
                        SuccessRate = totalRecipientsAttempted > 0 ? (int)((double)totalRecipientsReached / totalRecipientsAttempted * 100) : 0,
                        PointsEarned = totalRecipientsReached * 10 // فرض امتیاز دهی
                    }
                };

                // آمار پیام‌های خودکار
                var automatedMessages = await _context.AutomatedMessages
                    .Where(am => am.UserId == userId)
                    .Include(am => am.Campaigns)
                    .ToListAsync();

                var automationCampaigns = automatedMessages
                    .SelectMany(am => am.Campaigns)
                    .ToList();

                report.AutomatedMessageStatistics = new AutomatedMessageStatisticsDto
                {
                    TotalAutomatedMessages = automatedMessages.Count(),
                    ActiveAutomatedMessages = automatedMessages.Count(am => am.IsActive),
                    CampaignsCreatedFromAutomation = automationCampaigns.Count(),
                    MessagesSentByAutomation = automationCampaigns.Sum(c => c.Recipients?.Count() ?? 0),
                    AutomationTypeCounts = automatedMessages
                        .GroupBy(am => am.AutomationType)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    LastAutomatedCampaignCreated = automationCampaigns.Any() ? automationCampaigns.Max(c => c.CreatedAt) : null,
                    TotalAutomationRecipients = automationCampaigns.Sum(c => c.Recipients?.Count() ?? 0)
                };

                // بررسی صحت داده‌ها
                var validationIssues = new List<string>();
                var orphanedMessages = allMessages.Count(m => !allCampaigns.Any(c => c.MessageId == m.Id));
                var orphanedRecipients = allRecipients.Count(r => r.CampaignId == 0);
                var invalidContacts = allRecipients.Count(r => !IsValidMobileNumber(r.MobileNumber));
                var duplicateMessages = allMessages
                    .GroupBy(m => new { m.Content, m.UserId })
                    .Count(g => g.Count() > 1);

                if (orphanedMessages > 0) validationIssues.Add($"پیام‌های بدون کمپین: {orphanedMessages}");
                if (orphanedRecipients > 0) validationIssues.Add($"گیرندگان بدون کمپین: {orphanedRecipients}");
                if (invalidContacts > 0) validationIssues.Add($"شماره‌های موبایل نامعتبر: {invalidContacts}");
                if (duplicateMessages > 0) validationIssues.Add($"پیام‌های تکراری: {duplicateMessages}");

                var dataIntegrityScore = 100;
                if (orphanedMessages > 0) dataIntegrityScore -= 20;
                if (orphanedRecipients > 0) dataIntegrityScore -= 15;
                if (invalidContacts > 0) dataIntegrityScore -= 25;
                if (duplicateMessages > 0) dataIntegrityScore -= 10;
                dataIntegrityScore = Math.Max(0, dataIntegrityScore);

                report.DataValidation = new DataValidationDto
                {
                    IsDataValid = validationIssues.Count == 0,
                    ValidationIssues = validationIssues,
                    OrphanedMessages = orphanedMessages,
                    OrphanedRecipients = orphanedRecipients,
                    InvalidContacts = invalidContacts,
                    MissingWalletBalances = 0, // می‌توان بعداً اضافه کرد
                    DuplicateMessages = duplicateMessages,
                    DataIntegrityScore = dataIntegrityScore
                };

                return ApiResponse<ComprehensiveReportDto>.CreateSuccess(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting comprehensive report for user: {UserId}", userId);
                return ApiResponse<ComprehensiveReportDto>.InternalServerError("خطا در دریافت گزارش جامع");
            }
        }

        #endregion

        #region Preview and Personalization

        public async Task<ApiResponse<MessagePreviewDto>> GetMessagePreviewAsync(int messageId, int userId)
        {
            try
            {
                var message = await _messageRepository.GetByIdAsync(messageId);
                if (message == null || message.UserId != userId)
                {
                    return ApiResponse<MessagePreviewDto>.NotFound("پیام یافت نشد");
                }

                var preview = new MessagePreviewDto
                {
                    OriginalContent = message.Content,
                    PreviewContent = ReplacePlaceholdersWithSample(message.Content),
                    SamplePlaceholders = GetSamplePlaceholders(message.Content)
                };

                return ApiResponse<MessagePreviewDto>.CreateSuccess(preview);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting message preview: {MessageId}", messageId);
                return ApiResponse<MessagePreviewDto>.InternalServerError("خطا در دریافت پیش‌نمایش پیام");
            }
        }

        public async Task<ApiResponse<PersonalizedMessageResponseDto>> PersonalizeMessageAsync(int messageId, int userId, Dictionary<string, string> placeholders, bool saveToMessage = true)
        {
            try
            {
                var message = await _messageRepository.GetByIdAsync(messageId);
                if (message == null || message.UserId != userId)
                {
                    return ApiResponse<PersonalizedMessageResponseDto>.NotFound("پیام یافت نشد");
                }

                var originalContent = message.Content;
                var personalizedContent = PersonalizeMessage(originalContent, placeholders);

                // اگر SaveToMessage = true باشد، متن شخصی‌سازی شده را در پیام ذخیره می‌کنیم
                if (saveToMessage)
                {
                    message.Content = personalizedContent;
                    message.CharacterCount = SmsPartsCalculator.CountMessageCharacters(personalizedContent);
                    message.PartsCount = SmsPartsCalculator.CalculateParts(personalizedContent);
                    message.IsPersonalized = false; // دیگر placeholder ندارد
                    message.Placeholders = null; // همه placeholder ها جایگزین شده‌اند
                    message.UpdatedAt = DateTime.UtcNow;
                    
                    await _messageRepository.UpdateAsync(message);
                    
                    _logger.LogInformation("Personalized content saved to message {MessageId}", messageId);
                }

                var response = new PersonalizedMessageResponseDto
                {
                    OriginalContent = originalContent,
                    PersonalizedContent = personalizedContent,
                    UsedPlaceholders = placeholders
                };

                var messageText = saveToMessage 
                    ? "پیام با موفقیت شخصی‌سازی و ذخیره شد" 
                    : "پیام با موفقیت شخصی‌سازی شد (فقط پیش‌نمایش)";

                return ApiResponse<PersonalizedMessageResponseDto>.CreateSuccess(response, messageText);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid personalized message content for message: {MessageId}", messageId);
                var errorMessage = ControlledErrorHelper.SanitizeArgumentMessage(ex.Message, "محتویات پیام نامعتبر است");
                return ApiResponse<PersonalizedMessageResponseDto>.BadRequest(errorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error personalizing message: {MessageId}", messageId);
                return ApiResponse<PersonalizedMessageResponseDto>.InternalServerError("خطا در شخصی‌سازی پیام");
            }
        }

        #endregion

        #region Helper Methods

        // متد CalculateMessageParts حذف شد - از SmsPartsCalculator.CalculateParts استفاده می‌شود

        private bool ContainsPlaceholders(string content)
        {
            // بررسی وجود placeholder ها به فرمت ((نام)) یا {{نام}}
            return Regex.IsMatch(content, @"\(\(.+?\)\)|{{.+?}}");
        }

        private string? ExtractPlaceholders(string content)
        {
            var placeholders = new List<string>();

            // استخراج placeholder های فرمت ((نام))
            var matches1 = Regex.Matches(content, @"\(\((.+?)\)\)");
            foreach (Match match in matches1)
            {
                placeholders.Add(match.Groups[1].Value);
            }

            // استخراج placeholder های فرمت {{نام}}
            var matches2 = Regex.Matches(content, @"{{(.+?)}}");
            foreach (Match match in matches2)
            {
                placeholders.Add(match.Groups[1].Value);
            }

            if (placeholders.Any())
            {
                return JsonSerializer.Serialize(placeholders.Distinct().ToList());
            }

            return null;
        }

        /// <summary>
        /// شخصی‌سازی پیام با استفاده از اطلاعات مخاطب
        /// پشتیبانی از 5 فیلد ثابت: نام (نام کامل)، مبلغ کش بک، نام برند، تاریخ عضویت، تاریخ خرید
        /// </summary>
        private async Task<string> PersonalizeMessageWithContactAsync(string template, Contact contact)
        {
            string result = template;

            // دریافت نام کامل (FullName)
            var fullName = contact.FullName ?? "";

            // دریافت مبلغ کش بک (مجموع مبلغ‌های واریز شده)
            var totalCashback = await _context.CashbackTransactions
                .Where(ct => ct.ContactId == contact.Id && ct.Status == "Deposited")
                .SumAsync(ct => (decimal?)ct.Amount) ?? 0;

            // دریافت آخرین تاریخ خرید (آخرین تراکنش کش بک)
            var lastPurchaseDate = await _context.CashbackTransactions
                .Where(ct => ct.ContactId == contact.Id)
                .OrderByDescending(ct => ct.CreatedAt)
                .Select(ct => (DateTime?)ct.CreatedAt)
                .FirstOrDefaultAsync();

            // دریافت نام برند
            var brandName = contact.Brand ?? "";

            // تاریخ عضویت (تاریخ ایجاد مخاطب)
            var membershipDate = contact.CreatedAt;

            // جایگزینی placeholder ها
            // استفاده از Regex برای جایگزینی با حساسیت به حروف کوچک/بزرگ (Case-insensitive)
            
            // {{نام}} — نام کامل مخاطب
            result = ReplacePlaceholder(result, "{{نام}}", fullName);
            result = ReplacePlaceholder(result, "{{name}}", fullName, StringComparison.OrdinalIgnoreCase);
            
            // {{مبلغ کش بک}} — مجموع مبلغ‌های کش‌بک
            result = ReplacePlaceholder(result, "{{مبلغ کش بک}}", FormatAmount(totalCashback));
            result = ReplacePlaceholder(result, "{{cashback amount}}", FormatAmount(totalCashback), StringComparison.OrdinalIgnoreCase);
            result = ReplacePlaceholder(result, "{{cashbackamount}}", FormatAmount(totalCashback), StringComparison.OrdinalIgnoreCase);
            
            // {{نام برند}} — نام برند
            result = ReplacePlaceholder(result, "{{نام برند}}", brandName);
            result = ReplacePlaceholder(result, "{{brand name}}", brandName, StringComparison.OrdinalIgnoreCase);
            result = ReplacePlaceholder(result, "{{brandname}}", brandName, StringComparison.OrdinalIgnoreCase);
            
            // {{تاریخ عضویت}} — تاریخ عضویت مخاطب
            result = ReplacePlaceholder(result, "{{تاریخ عضویت}}", FormatPersianDate(membershipDate));
            result = ReplacePlaceholder(result, "{{membership date}}", FormatPersianDate(membershipDate), StringComparison.OrdinalIgnoreCase);
            result = ReplacePlaceholder(result, "{{membershipdate}}", FormatPersianDate(membershipDate), StringComparison.OrdinalIgnoreCase);
            
            // {{تاریخ خرید}} — تاریخ آخرین خرید
            result = ReplacePlaceholder(result, "{{تاریخ خرید}}", lastPurchaseDate.HasValue ? FormatPersianDate(lastPurchaseDate.Value) : "");
            result = ReplacePlaceholder(result, "{{purchase date}}", lastPurchaseDate.HasValue ? FormatPersianDate(lastPurchaseDate.Value) : "", StringComparison.OrdinalIgnoreCase);
            result = ReplacePlaceholder(result, "{{purchasedate}}", lastPurchaseDate.HasValue ? FormatPersianDate(lastPurchaseDate.Value) : "", StringComparison.OrdinalIgnoreCase);

            // پشتیبانی از فرمت قدیمی با یک آکولاد
            result = ReplacePlaceholder(result, "{نام}", fullName);
            result = ReplacePlaceholder(result, "{مبلغ کش بک}", FormatAmount(totalCashback));
            result = ReplacePlaceholder(result, "{نام برند}", brandName);
            result = ReplacePlaceholder(result, "{تاریخ عضویت}", FormatPersianDate(membershipDate));
            result = ReplacePlaceholder(result, "{تاریخ خرید}", lastPurchaseDate.HasValue ? FormatPersianDate(lastPurchaseDate.Value) : "");

            return result;
        }

        /// <summary>
        /// جایگزینی placeholder در متن با حساسیت به نوع مقایسه
        /// </summary>
        private string ReplacePlaceholder(string text, string placeholder, string value, StringComparison comparison = StringComparison.Ordinal)
        {
            // Escape کردن کاراکترهای خاص در placeholder برای استفاده در Regex
            var escapedPlaceholder = Regex.Escape(placeholder);
            var escapedValue = value;
            
            // جایگزینی با Regex برای جایگزینی همه موارد
            var pattern = escapedPlaceholder;
            return Regex.Replace(text, pattern, escapedValue, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        /// <summary>
        /// فرمت کردن مبلغ به فرمت فارسی
        /// </summary>
        private string FormatAmount(decimal amount)
        {
            return $"{amount:N0} تومان";
        }

        /// <summary>
        /// فرمت کردن تاریخ به فرمت فارسی
        /// </summary>
        private string FormatPersianDate(DateTime date)
        {
            try
            {
                var persianCalendar = new System.Globalization.PersianCalendar();
                var year = persianCalendar.GetYear(date);
                var month = persianCalendar.GetMonth(date);
                var day = persianCalendar.GetDayOfMonth(date);

                var monthNames = new[] { "", "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند" };
                
                return $"{day} {monthNames[month]} {year}";
            }
            catch
            {
                // در صورت خطا، تاریخ میلادی را برمی‌گرداند
                return date.ToString("yyyy/MM/dd");
            }
        }

        private string PersonalizeMessage(string template, Dictionary<string, string> placeholders)
        {
            string result = template;
            foreach (var placeholder in placeholders)
            {
                if (string.IsNullOrEmpty(placeholder.Key))
                    continue;

                var escapedKey = Regex.Escape(placeholder.Key);
                var value = placeholder.Value ?? "";

                // جایگزینی placeholder های فرمت ((نام))
                var pattern1 = @"\(\((" + escapedKey + @")\)\)";
                result = Regex.Replace(result, pattern1, value, RegexOptions.IgnoreCase);
                
                // جایگزینی placeholder های فرمت {{نام}}
                var pattern2 = @"\{\{(" + escapedKey + @")\}\}";
                result = Regex.Replace(result, pattern2, value, RegexOptions.IgnoreCase);
            }
            return result;
        }

        private string ReplacePlaceholdersWithSample(string content)
        {
            string result = content;

            // جایگزینی placeholder های فرمت ((نام))
            result = Regex.Replace(result, @"\(\((.+?)\)\)", match => match.Groups[1].Value);

            // جایگزینی placeholder های فرمت {{نام}}
            result = Regex.Replace(result, @"{{(.+?)}}", match => match.Groups[1].Value);

            return result;
        }

        private Dictionary<string, string> GetSamplePlaceholders(string content)
        {
            var placeholders = new Dictionary<string, string>();
            var extracted = ExtractPlaceholders(content);
            
            if (!string.IsNullOrEmpty(extracted))
            {
                var placeholderList = JsonSerializer.Deserialize<List<string>>(extracted);
                if (placeholderList != null)
                {
                    foreach (var placeholder in placeholderList)
                    {
                        placeholders[placeholder] = placeholder; // مقدار نمونه
                    }
                }
            }

            return placeholders;
        }

        // این متد برای پیام خودکار استفاده می‌شد که متد مجزایی دارد
        // برای پیام عادی، گیرندگان از Session خوانده می‌شوند
        /*
        private async Task<(bool IsValid, string ErrorMessage)> ValidateRecipientsAsync(int userId, CreateCampaignDto campaignDto)
        {
            // بررسی اینکه حداقل یک روش انتخاب گیرنده مشخص شده باشد
            bool hasNotebookIds = campaignDto.ContactNotebookIds != null && campaignDto.ContactNotebookIds.Any(id => id > 0);
            // ContactIds فقط در صورتی که دفترچه انتخاب نشده باشد به عنوان روش مستقل محسوب می‌شود
            // اگر دفترچه انتخاب شده باشد، ContactIds فقط برای فیلتر کردن از همان دفترچه است
            bool hasContactIds = campaignDto.ContactIds != null && campaignDto.ContactIds.Any(id => id > 0) 
                && (campaignDto.ContactNotebookIds == null || !campaignDto.ContactNotebookIds.Any(id => id > 0));
            // فقط شماره‌های غیرخالی را در نظر می‌گیریم
            bool hasMobileNumbers = campaignDto.MobileNumbers != null && campaignDto.MobileNumbers.Any(mn => !string.IsNullOrWhiteSpace(mn));
            bool hasTags = campaignDto.SendToSpecificTags && campaignDto.SelectedTagIds != null && campaignDto.SelectedTagIds.Any(id => id > 0);

            if (!hasNotebookIds && !hasContactIds && !hasMobileNumbers && !hasTags)
            {
                return (false, "حداقل باید یک روش انتخاب گیرنده مشخص شود (دفترچه تلفن، مخاطبین مستقیم، شماره موبایل یا تگ)");
            }

            // اعتبارسنجی دفترچه‌های تلفن
            if (hasNotebookIds)
            {
                var invalidNotebooks = new List<int>();
                foreach (var notebookId in campaignDto.ContactNotebookIds)
                {
                    if (notebookId <= 0)
                    {
                        invalidNotebooks.Add(notebookId);
                        continue;
                    }
                    var notebook = await _notebookRepository.GetByIdAsync(notebookId);
                    if (notebook == null || notebook.UserId != userId || notebook.IsDeleted)
                    {
                        invalidNotebooks.Add(notebookId);
                    }
                }
                if (invalidNotebooks.Any())
                {
                    return (false, $"دفترچه‌های تلفن با شناسه‌های [{string.Join(", ", invalidNotebooks)}] یافت نشد یا به شما تعلق ندارند");
                }
            }

            // اعتبارسنجی مخاطبین (فقط در صورتی که دفترچه انتخاب شده باشد و ContactIds برای حذف ارسال شده باشد)
            // اگر دفترچه انتخاب شده باشد و ContactIds ارسال شده باشد، باید آن مخاطبین در همان دفترچه‌ها باشند
            if (hasNotebookIds && campaignDto.ContactIds != null && campaignDto.ContactIds.Any(id => id > 0))
            {
                var invalidContacts = new List<int>();
                var validNotebookIds = campaignDto.ContactNotebookIds.Where(id => id > 0).ToList();
                
                foreach (var contactId in campaignDto.ContactIds.Where(id => id > 0))
                {
                    var contact = await _contactRepository.GetByIdAsync(contactId);
                    if (contact == null || contact.IsDeleted)
                    {
                        invalidContacts.Add(contactId);
                        continue;
                    }
                    
                    // بررسی اینکه مخاطب در یکی از دفترچه‌های انتخاب شده باشد
                    if (!validNotebookIds.Contains(contact.ContactNotebookId))
                    {
                        invalidContacts.Add(contactId);
                        continue;
                    }
                    
                    var notebook = await _notebookRepository.GetByIdAsync(contact.ContactNotebookId);
                    if (notebook == null || notebook.UserId != userId || notebook.IsDeleted)
                    {
                        invalidContacts.Add(contactId);
                    }
                }
                
                if (invalidContacts.Any())
                {
                    return (false, $"مخاطبین با شناسه‌های [{string.Join(", ", invalidContacts)}] یافت نشد یا به شما تعلق ندارند");
                }
            }

            // اعتبارسنجی شماره‌های موبایل (فقط اگر شماره‌های معتبری ارسال شده باشد)
            if (hasMobileNumbers && campaignDto.MobileNumbers != null)
            {
                var invalidNumbers = new List<string>();
                foreach (var mobileNumber in campaignDto.MobileNumbers)
                {
                    // شماره‌های خالی را نادیده می‌گیریم (اختیاری هستند)
                    if (string.IsNullOrWhiteSpace(mobileNumber))
                    {
                        continue;
                    }
                    // بررسی فرمت شماره موبایل ایرانی (09xxxxxxxxx)
                    if (!System.Text.RegularExpressions.Regex.IsMatch(mobileNumber.Trim(), @"^09\d{9}$"))
                    {
                        invalidNumbers.Add(mobileNumber);
                    }
                }
                if (invalidNumbers.Any())
                {
                    return (false, $"شماره‌های موبایل نامعتبر: [{string.Join(", ", invalidNumbers)}]. فرمت صحیح: 09xxxxxxxxx (11 رقم)");
                }
            }

            // اعتبارسنجی تگ‌ها
            // اگر سایر روش‌های انتخاب گیرنده وجود دارد، تگ‌ها کاملاً اختیاری هستند (حتی اگر نامعتبر باشند)
            // فقط اگر فقط تگ‌ها انتخاب شده باشند، باید معتبر باشند
            if (campaignDto.SendToSpecificTags && campaignDto.SelectedTagIds != null && campaignDto.SelectedTagIds.Any())
            {
                // اگر سایر روش‌ها وجود دارد، تگ‌ها را نادیده می‌گیریم (اختیاری هستند)
                if (hasNotebookIds || hasContactIds || hasMobileNumbers)
                {
                    // تگ‌ها اختیاری هستند - خطا نمی‌دهیم
                }
                else
                {
                    // فقط تگ‌ها انتخاب شده - باید معتبر باشند
                    var validTagIds = campaignDto.SelectedTagIds.Where(id => id > 0).ToList();
                    
                    if (!validTagIds.Any())
                    {
                        return (false, "اگر ارسال به تگ‌های خاص فعال است، باید حداقل یک تگ معتبر انتخاب شود یا روش دیگری (دفترچه تلفن، مخاطب، شماره موبایل) انتخاب شود");
                    }
                    
                    var invalidTags = new List<int>();
                    foreach (var tagId in validTagIds)
                    {
                        var tag = await _context.MessageTags.FirstOrDefaultAsync(t => t.Id == tagId && t.UserId == userId && !t.IsDeleted && t.IsActive);
                        if (tag == null)
                        {
                            invalidTags.Add(tagId);
                        }
                    }
                    if (invalidTags.Any())
                    {
                        return (false, $"تگ‌های با شناسه‌های [{string.Join(", ", invalidTags)}] یافت نشد یا به شما تعلق ندارند");
                    }
                }
            }

            return (true, string.Empty);
        }
        */

        // این متد برای پیام خودکار استفاده می‌شد که متد مجزایی دارد
        // برای پیام عادی، گیرندگان از Session خوانده می‌شوند
        /*
        private async Task<List<RecipientItemDto>> GetRecipientsAsync(int userId, CreateCampaignDto campaignDto)
        {
            var recipients = new List<RecipientItemDto>();

            // از دفترچه‌ها
            if (campaignDto.ContactNotebookIds != null && campaignDto.ContactNotebookIds.Any())
            {
                foreach (var notebookId in campaignDto.ContactNotebookIds.Where(id => id > 0))
                {
                    var notebook = await _notebookRepository.GetByIdAsync(notebookId);
                    if (notebook == null || notebook.UserId != userId || notebook.IsDeleted) continue;

                    // دریافت همه مخاطبین دفترچه
                    var allContacts = await _contactRepository.GetByNotebookIdAsync(notebookId);
                    var validContacts = allContacts.Where(c => !c.IsDeleted).ToList();

                    // اگر ContactIds ارسال شده باشد، آن مخاطبین را از لیست حذف می‌کنیم (یعنی به آن‌ها پیام نمی‌رود)
                    // اگر ContactIds null باشد، همه مخاطبین انتخاب می‌شوند
                    if (campaignDto.ContactIds != null && campaignDto.ContactIds.Any())
                    {
                        // حذف مخاطبینی که در ContactIds هستند (یعنی به آن‌ها پیام نمی‌رود)
                        var excludedContactIds = campaignDto.ContactIds.ToHashSet();
                        validContacts = validContacts
                            .Where(c => !excludedContactIds.Contains(c.Id))
                            .ToList();
                    }

                    recipients.AddRange(validContacts.Select(c => new RecipientItemDto
                    {
                        ContactId = c.Id,
                        MobileNumber = c.MobileNumber,
                        FullName = c.FullName
                    }));
                }
            }

            // از مخاطبین مستقیم (فقط در صورتی که دفترچه انتخاب نشده باشد)
            // اگر دفترچه انتخاب شده باشد، ContactIds قبلاً در بخش دفترچه پردازش شده است
            // پس فقط در صورتی که دفترچه انتخاب نشده باشد، اینجا پردازش می‌کنیم
            if (campaignDto.ContactIds != null && campaignDto.ContactIds.Any())
            {
                // اگر دفترچه انتخاب شده باشد، ContactIds قبلاً پردازش شده است
                if (campaignDto.ContactNotebookIds == null || !campaignDto.ContactNotebookIds.Any(id => id > 0))
                {
                    foreach (var contactId in campaignDto.ContactIds.Where(id => id > 0))
                    {
                        var contact = await _contactRepository.GetByIdAsync(contactId);
                        if (contact != null && !contact.IsDeleted)
                        {
                            var notebook = await _notebookRepository.GetByIdAsync(contact.ContactNotebookId);
                            if (notebook != null && notebook.UserId == userId && !notebook.IsDeleted)
                            {
                                recipients.Add(new RecipientItemDto
                                {
                                    ContactId = contact.Id,
                                    MobileNumber = contact.MobileNumber,
                                    FullName = contact.FullName
                                });
                            }
                        }
                    }
                }
            }

            // از شماره‌های مستقیم - اضافه کردن خودکار به دفترچه پیش‌فرض
            if (campaignDto.MobileNumbers != null && campaignDto.MobileNumbers.Any())
            {
                foreach (var mobileNumber in campaignDto.MobileNumbers.Where(mn => !string.IsNullOrWhiteSpace(mn)))
                {
                    try
                    {
                        // نرمال‌سازی شماره موبایل
                        var normalizedMobile = NormalizeMobileNumber(mobileNumber.Trim());
                        
                        // بررسی اعتبار شماره موبایل
                        if (string.IsNullOrWhiteSpace(normalizedMobile) || !IsValidMobileNumber(normalizedMobile))
                        {
                            _logger.LogWarning("Invalid mobile number skipped: {MobileNumber}", mobileNumber);
                            continue;
                        }
                        
                        // ابتدا چک می‌کنیم آیا این شماره در یکی از دفترچه‌های کاربر وجود دارد
                        var existingContact = await FindContactByMobileNumberInUserNotebooksAsync(userId, normalizedMobile);
                        
                        if (existingContact != null)
                        {
                            // اگر پیدا شد، از ContactId استفاده می‌کنیم
                            recipients.Add(new RecipientItemDto
                            {
                                ContactId = existingContact.Id,
                                MobileNumber = existingContact.MobileNumber,
                                FullName = existingContact.FullName
                            });
                        }
                        else
                        {
                            // اگر پیدا نشد، به دفترچه پیش‌فرض اضافه می‌کنیم
                            var defaultNotebook = await GetOrCreateDefaultDirectMessagesNotebookAsync(userId);
                            var newContact = await AddContactToNotebookAsync(defaultNotebook.Id, normalizedMobile);
                            
                            recipients.Add(new RecipientItemDto
                            {
                                ContactId = newContact.Id,
                                MobileNumber = newContact.MobileNumber,
                                FullName = newContact.FullName
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing mobile number: {MobileNumber}", mobileNumber);
                        // در صورت خطا، این شماره را نادیده می‌گیریم و ادامه می‌دهیم
                        continue;
                    }
                }
            }

            // حذف تکراری‌ها
            return recipients
                .GroupBy(r => r.MobileNumber)
                .Select(g => g.First())
                .ToList();
        }
        */

        private async Task<List<RecipientItemDto>> FilterDuplicateRecipientsAsync(List<RecipientItemDto> recipients, int hours, int userId)
        {
            if (!recipients.Any())
            {
                return recipients;
            }

            var cutoffTime = DateTime.UtcNow.AddHours(-hours);
            
            // استخراج لیست شماره موبایل‌ها
            var mobileNumbers = recipients.Select(r => r.MobileNumber).Distinct().ToList();

            // یک Query برای همه گیرندگان - بهینه‌سازی N+1 Query
            // دریافت آخرین ارسال برای هر شماره موبایل در یک Query
            var recentSends = await _context.MessageRecipients
                .Include(mr => mr.Campaign)
                .Where(mr => mobileNumbers.Contains(mr.MobileNumber)
                        && mr.Status == "Sent" 
                        && mr.SentAt >= cutoffTime
                    && mr.Campaign.UserId == userId)
                .GroupBy(mr => mr.MobileNumber)
                .Select(g => new
                {
                    MobileNumber = g.Key,
                    LastSentAt = g.Max(mr => mr.SentAt)
                })
                .ToDictionaryAsync(x => x.MobileNumber, x => x.LastSentAt);

            // فیلتر کردن گیرندگانی که در لیست recentSends نیستند
            var filteredRecipients = recipients
                .Where(r => !recentSends.ContainsKey(r.MobileNumber))
                .ToList();

            _logger.LogInformation("FilterDuplicateRecipients - Total: {Total}, Filtered: {Filtered}, Removed: {Removed}", 
                recipients.Count, filteredRecipients.Count, recipients.Count - filteredRecipients.Count);

            return filteredRecipients;
        }

        /// <summary>
        /// ارسال SMS با Retry Mechanism و Exponential Backoff (مشکل 6.2)
        /// </summary>
        private async Task<ApiResponse<DTOs.Sms.SendSmsResponseDto>> SendSmsWithRetryAsync(
            DTOs.Sms.SendSmsRequestDto request, 
            int maxRetries = 3,
            int initialDelayMs = 1000)
        {
            var lastException = (Exception?)null;
            var lastResult = (ApiResponse<DTOs.Sms.SendSmsResponseDto>?)null;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    if (attempt > 0)
                    {
                        // Exponential Backoff: delay = initialDelay * 2^(attempt-1)
                        var delayMs = initialDelayMs * (int)Math.Pow(2, attempt - 1);
                        _logger.LogInformation("Retrying SMS send - Attempt: {Attempt}/{MaxRetries}, Delay: {Delay}ms, Mobile: {Mobile}", 
                            attempt + 1, maxRetries + 1, delayMs, request.Mobile);
                        await Task.Delay(delayMs);
                    }

                    var result = await _smsService.SendSmsAsync(request);
                    
                    // اگر ارسال موفق بود، نتیجه را برمی‌گردانیم
                    // Sid > 0 یعنی پیام ارسال شده (حتی اگر Status = 0 باشد)
                    bool isSuccess = result.Success && result.Data != null && 
                        (result.Data.Sid > 0 || result.Data.Status > 0);
                    
                    if (isSuccess)
                    {
                        if (attempt > 0)
                        {
                            _logger.LogInformation("SMS sent successfully after {Attempt} retries - Mobile: {Mobile}", 
                                attempt + 1, request.Mobile);
                        }
                        return result;
                    }

                    // بررسی خطاهای غیرقابل Retry
                    if (result.Data != null)
                    {
                        var status = result.Data.Status;
                        var message = result.Data.Message ?? "";
                        
                        // خطاهای غیرقابل Retry:
                        // 1. Status < 0 (خطاهای API)
                        // 2. Status = 0 با پیام‌های خاص (مثل "پیام تکراری")
                        // 3. Sid = 0 و Status = 0 با پیام‌های خطای مشخص
                        bool isNonRetryable = false;
                        
                        if (status < 0)
                        {
                            isNonRetryable = true;
                        }
                        else if (status == 0)
                        {
                            // بررسی پیام‌های خطای غیرقابل Retry
                            var lowerMessage = message.ToLower();
                            if (lowerMessage.Contains("تکراری") || 
                                lowerMessage.Contains("duplicate") ||
                                lowerMessage.Contains("مجاز به ارسال پیام تکراری") ||
                                lowerMessage.Contains("شماره نامعتبر") ||
                                lowerMessage.Contains("invalid") ||
                                lowerMessage.Contains("blacklist") ||
                                lowerMessage.Contains("مشترک در لیست سیاه"))
                            {
                                isNonRetryable = true;
                            }
                        }
                        
                        if (isNonRetryable)
                        {
                            _logger.LogWarning("SMS send failed with non-retryable error - Mobile: {Mobile}, Status: {Status}, Message: {Message}", 
                                request.Mobile, status, message);
                            return result;
                        }
                    }

                    lastResult = result;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogWarning(ex, "Exception during SMS send attempt {Attempt}/{MaxRetries} - Mobile: {Mobile}", 
                        attempt + 1, maxRetries + 1, request.Mobile);

                    // اگر آخرین تلاش بود، خطا را throw می‌کنیم
                    if (attempt == maxRetries)
                    {
                        _logger.LogError(ex, "All SMS send retry attempts failed - Mobile: {Mobile}", request.Mobile);
                        break;
                    }
                }
            }

            // اگر همه Retry ها ناموفق بودند
            if (lastResult != null)
            {
                return lastResult;
            }

            // اگر Exception داشتیم
            if (lastException != null)
            {
                _logger.LogError(lastException, "SMS send failed after all retries - Mobile: {Mobile}", request.Mobile);
                return ApiResponse<DTOs.Sms.SendSmsResponseDto>.InternalServerError(ControlledErrorHelper.SmsFailed);
            }

            return ApiResponse<DTOs.Sms.SendSmsResponseDto>.InternalServerError("خطا در ارسال پیامک");
        }

        private async Task<List<RecipientItemDto>> FilterByTagsAsync(List<RecipientItemDto> recipients, List<int> tagIds, int userId)
        {
            if (!tagIds.Any() || !recipients.Any())
            {
                return recipients;
            }

            // بررسی امنیتی: فقط تگ‌های متعلق به کاربر را در نظر می‌گیریم
            var validTagIds = await _context.MessageTags
                .Where(t => t.UserId == userId 
                    && tagIds.Contains(t.Id) 
                    && !t.IsDeleted 
                    && t.IsActive)
                .Select(t => t.Id)
                .ToListAsync();

            if (!validTagIds.Any())
            {
                // اگر هیچ تگ معتبری نبود، لیست خالی برمی‌گردانیم
                return new List<RecipientItemDto>();
            }

            var filteredRecipients = new List<RecipientItemDto>();

            foreach (var recipient in recipients)
            {
                if (recipient.ContactId.HasValue)
                {
                    var contactTags = await _context.ContactTags
                        .Where(ct => ct.ContactId == recipient.ContactId.Value 
                            && validTagIds.Contains(ct.TagId))
                        .AnyAsync();

                    if (contactTags)
                    {
                        filteredRecipients.Add(recipient);
                    }
                }
                // اگر ContactId ندارد، آن را حذف می‌کنیم چون نمی‌توانیم بر اساس تگ فیلتر کنیم
                // (در GetRecipientsAsync همه شماره‌های مستقیم به دفترچه اضافه شده‌اند، پس این حالت نباید رخ دهد)
            }

            return filteredRecipients;
        }

        private Task<MessageResponseDto> MapToMessageResponseDtoAsync(Message message)
        {
            List<string>? placeholders = null;
            if (!string.IsNullOrEmpty(message.Placeholders))
            {
                placeholders = JsonSerializer.Deserialize<List<string>>(message.Placeholders);
            }

            return Task.FromResult(new MessageResponseDto
            {
                Id = message.Id,
                Content = message.Content,
                CharacterCount = message.CharacterCount,
                PartsCount = message.PartsCount,
                IsPersonalized = message.IsPersonalized,
                Placeholders = placeholders,
                Status = message.Status,
                CreatedAt = message.CreatedAt,
                UpdatedAt = message.UpdatedAt
            });
        }

        private Task<CampaignResponseDto> MapToCampaignResponseDtoAsync(MessageCampaign campaign)
        {
            List<int>? selectedTagIds = null;
            if (!string.IsNullOrEmpty(campaign.SelectedTags))
            {
                selectedTagIds = JsonSerializer.Deserialize<List<int>>(campaign.SelectedTags);
            }

            return Task.FromResult(new CampaignResponseDto
            {
                Id = campaign.Id,
                MessageId = campaign.MessageId,
                Title = campaign.Title,
                SendType = Enum.TryParse<CampaignSendType>(campaign.SendType, out var sendType) ? sendType : CampaignSendType.Quick,
                ScheduledAt = campaign.ScheduledAt,
                PreventDuplicate = campaign.PreventDuplicate,
                DuplicatePreventionHours = campaign.DuplicatePreventionHours,
                SendToSpecificTags = campaign.SendToSpecificTags,
                SelectedTagIds = selectedTagIds,
                RecipientsCount = campaign.RecipientsCount,
                PartsCount = campaign.PartsCount,
                CostPerPart = campaign.CostPerPart,
                EstimatedTotalCost = campaign.EstimatedTotalCost,
                ActualTotalCost = campaign.ActualTotalCost,
                WalletStatus = campaign.WalletStatus,
                Status = campaign.Status,
                SentAt = campaign.SentAt,
                SentCount = campaign.SentCount,
                FailedCount = campaign.FailedCount,
                CreatedAt = campaign.CreatedAt
            });
        }

        private TemplateResponseDto MapToTemplateResponseDto(MessageTemplate template)
        {
            // استفاده از Group navigation property (باید با Include لود شده باشد)
            string? groupName = null;
            if (template.GroupId.HasValue && template.Group != null && !template.Group.IsDeleted)
            {
                groupName = template.Group.Name;
            }

            return new TemplateResponseDto
            {
                Id = template.Id,
                Name = template.Name,
                Content = template.Content,
                Category = template.Category,
                Description = template.Description,
                Icon = template.Icon,
                IsDefault = template.IsDefault,
                IsActive = template.IsActive,
                GroupId = template.GroupId,
                GroupName = groupName,
                ApprovalStatus = template.ApprovalStatus,
                RejectionReason = template.RejectionReason,
                ApprovedAt = template.ApprovedAt,
                CreatedAt = template.CreatedAt
            };
        }

        private async Task<string?> ValidateTemplateApprovalForMessageAsync(Message message)
        {
            if (!message.TemplateId.HasValue)
                return null;

            var template = await _templateRepository.GetByIdAsync(message.TemplateId.Value);
            if (template == null || template.IsDeleted)
                return null;

            if (template.ApprovalStatus != AdminApprovalStatuses.Approved)
                return "قالب پیام هنوز توسط ادمین تأیید نشده است";

            return null;
        }

        private async Task UpsertCampaignApprovalRequestAsync(MessageCampaign campaign, Message message)
        {
            var existing = await _context.SmsApprovalRequests
                .FirstOrDefaultAsync(r => r.MessageCampaignId == campaign.Id
                    && r.Status == AdminApprovalStatuses.Pending
                    && !r.IsDeleted);

            if (existing != null)
            {
                existing.ContentPreview = message.Content;
                existing.TitlePreview = message.Title ?? campaign.Title;
                existing.RecipientsCount = campaign.RecipientsCount;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.SmsApprovalRequests.Add(new SmsApprovalRequest
                {
                    UserId = campaign.UserId,
                    RequestType = SmsApprovalRequestTypes.Campaign,
                    MessageCampaignId = campaign.Id,
                    MessageId = message.Id,
                    ContentPreview = message.Content,
                    TitlePreview = message.Title ?? campaign.Title,
                    RecipientsCount = campaign.RecipientsCount,
                    Status = AdminApprovalStatuses.Pending,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
        }

        private async Task UpsertDirectMessageApprovalRequestAsync(
            Message message,
            MessageSession session,
            SendDirectMessageDto sendDto,
            int recipientsCount)
        {
            var existing = await _context.SmsApprovalRequests
                .FirstOrDefaultAsync(r => r.MessageId == message.Id
                    && r.MessageSessionId == session.Id
                    && r.Status == AdminApprovalStatuses.Pending
                    && !r.IsDeleted);

            var payload = JsonSerializer.Serialize(sendDto);

            if (existing != null)
            {
                existing.ContentPreview = message.Content;
                existing.TitlePreview = message.Title;
                existing.RecipientsCount = recipientsCount;
                existing.SendPayloadJson = payload;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.SmsApprovalRequests.Add(new SmsApprovalRequest
                {
                    UserId = message.UserId,
                    RequestType = SmsApprovalRequestTypes.DirectMessage,
                    MessageId = message.Id,
                    MessageSessionId = session.Id,
                    ContentPreview = message.Content,
                    TitlePreview = message.Title,
                    RecipientsCount = recipientsCount,
                    SendPayloadJson = payload,
                    Status = AdminApprovalStatuses.Pending,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// پیدا کردن یا ایجاد دفترچه پیش‌فرض برای پیام‌های مستقیم
        /// با استفاده از Transaction برای جلوگیری از race condition
        /// </summary>
        private async Task<ContactNotebook> GetOrCreateDefaultDirectMessagesNotebookAsync(int userId)
        {
            const string defaultNotebookName = "پیام‌های مستقیم";

            // استفاده از Transaction برای جلوگیری از ایجاد duplicate در صورت درخواست همزمان
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // ابتدا سعی می‌کنیم دفترچه را پیدا کنیم (داخل transaction)
                var existingNotebook = await _context.ContactNotebooks
                    .Where(n => n.UserId == userId 
                        && n.Name == defaultNotebookName 
                        && !n.IsDeleted)
                    .FirstOrDefaultAsync();

                if (existingNotebook != null)
                {
                    await transaction.CommitAsync();
                    return existingNotebook;
                }

                // اگر پیدا نشد، ایجاد می‌کنیم
                var newNotebook = new ContactNotebook
                {
                    UserId = userId,
                    Name = defaultNotebookName,
                    Description = "دفترچه خودکار برای شماره‌های موبایل مستقیم که بدون انتخاب دفترچه وارد می‌شوند",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.ContactNotebooks.AddAsync(newNotebook);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Default direct messages notebook created for user {UserId} with ID: {NotebookId}", 
                    userId, newNotebook.Id);

                return newNotebook;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating default notebook for user {UserId}", userId);
                
                // در صورت خطا، دوباره سعی می‌کنیم پیدا کنیم (ممکن است توسط thread دیگر ایجاد شده باشد)
                var fallbackNotebook = await _context.ContactNotebooks
                    .Where(n => n.UserId == userId 
                        && n.Name == defaultNotebookName 
                        && !n.IsDeleted)
                    .FirstOrDefaultAsync();
                
                if (fallbackNotebook != null)
                {
                    return fallbackNotebook;
                }
                
                throw;
            }
        }

        /// <summary>
        /// پیدا کردن مخاطب با شماره موبایل در تمام دفترچه‌های کاربر
        /// </summary>
        private async Task<Contact?> FindContactByMobileNumberInUserNotebooksAsync(int userId, string mobileNumber)
        {
            // دریافت تمام دفترچه‌های کاربر
            var notebookIds = await _context.ContactNotebooks
                .Where(n => n.UserId == userId && !n.IsDeleted)
                .Select(n => n.Id)
                .ToListAsync();

            // پیدا کردن مخاطب با این شماره در هر کدام از دفترچه‌ها
            var contact = await _context.Contacts
                .Where(c => notebookIds.Contains(c.ContactNotebookId) 
                    && c.MobileNumber == mobileNumber 
                    && !c.IsDeleted)
                .FirstOrDefaultAsync();

            return contact;
        }

        /// <summary>
        /// اضافه کردن مخاطب به دفترچه (در صورت عدم وجود)
        /// با استفاده از Transaction برای اطمینان از یکپارچگی
        /// </summary>
        private async Task<Contact> AddContactToNotebookAsync(int notebookId, string mobileNumber)
        {
            // استفاده از Transaction برای جلوگیری از race condition
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // چک می‌کنیم آیا این شماره قبلاً در این دفترچه وجود دارد
                // این چک باید داخل transaction باشد
                var existingContact = await _context.Contacts
                    .Where(c => c.ContactNotebookId == notebookId 
                        && c.MobileNumber == mobileNumber 
                        && !c.IsDeleted)
                    .FirstOrDefaultAsync();

                if (existingContact != null)
                {
                    await transaction.CommitAsync();
                    return existingContact;
                }

                // اگر وجود ندارد، ایجاد می‌کنیم
                var newContact = new Contact
                {
                    ContactNotebookId = notebookId,
                    MobileNumber = mobileNumber,
                    FullName = null, // نام را کاربر می‌تواند بعداً اضافه کند
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Contacts.AddAsync(newContact);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Contact added to notebook {NotebookId} with mobile number {MobileNumber}", 
                    notebookId, mobileNumber);

                return newContact;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                
                // اگر خطای unique constraint رخ داد، مخاطب موجود را پیدا و برگردان
                if (ex is DbUpdateException dbEx)
                {
                    var errorMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                    if (errorMessage.ToLower().Contains("unique") || errorMessage.ToLower().Contains("duplicate"))
                    {
                        _logger.LogWarning("خطای unique constraint برای شماره موبایل {MobileNumber} در دفترچه {NotebookId}. در حال جستجوی مخاطب موجود...", 
                            mobileNumber, notebookId);
                        
                        // جستجوی مخاطب موجود (بدون transaction)
                        var existingContact = await _context.Contacts
                            .Where(c => c.ContactNotebookId == notebookId 
                                && c.MobileNumber == mobileNumber 
                                && !c.IsDeleted)
                            .FirstOrDefaultAsync();
                        
                        if (existingContact != null)
                        {
                            _logger.LogInformation("مخاطب موجود با شماره {MobileNumber} در دفترچه {NotebookId} پیدا شد.", 
                                mobileNumber, notebookId);
                            return existingContact;
                        }
                    }
                }
                
                _logger.LogError(ex, "Error adding contact to notebook {NotebookId} with mobile {MobileNumber}", 
                    notebookId, mobileNumber);
                throw;
            }
        }

        /// <summary>
        /// نرمال‌سازی شماره موبایل
        /// </summary>
        private string NormalizeMobileNumber(string mobileNumber)
        {
            if (string.IsNullOrWhiteSpace(mobileNumber))
                return "";

            // حذف فاصله‌ها و کاراکترهای اضافی
            mobileNumber = mobileNumber.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
            
            // تبدیل اعداد فارسی/عربی به انگلیسی
            mobileNumber = ConvertPersianDigitsToEnglish(mobileNumber);

            // اگر با +98 شروع شده، تبدیل به 0
            if (mobileNumber.StartsWith("+98"))
            {
                mobileNumber = "0" + mobileNumber.Substring(3);
            }
            // اگر با 98 شروع شده (بدون +)
            else if (mobileNumber.StartsWith("98") && mobileNumber.Length == 12)
            {
                mobileNumber = "0" + mobileNumber.Substring(2);
            }
            // اگر با 9 شروع شده (بدون 0)
            else if (mobileNumber.StartsWith("9") && mobileNumber.Length == 10)
            {
                mobileNumber = "0" + mobileNumber;
            }

            return mobileNumber;
        }

        /// <summary>
        /// تبدیل اعداد فارسی/عربی به انگلیسی
        /// </summary>
        private string ConvertPersianDigitsToEnglish(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            var persianDigits = "۰۱۲۳۴۵۶۷۸۹";
            var arabicDigits = "٠١٢٣٤٥٦٧٨٩";
            var englishDigits = "0123456789";

            var result = new System.Text.StringBuilder(input.Length);
            foreach (char c in input)
            {
                var persianIndex = persianDigits.IndexOf(c);
                if (persianIndex >= 0)
                {
                    result.Append(englishDigits[persianIndex]);
                }
                else
                {
                    var arabicIndex = arabicDigits.IndexOf(c);
                    if (arabicIndex >= 0)
                    {
                        result.Append(englishDigits[arabicIndex]);
                    }
                    else
                    {
                        result.Append(c);
                    }
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// بررسی اعتبار شماره موبایل ایرانی
        /// </summary>
        private bool IsValidMobileNumber(string mobileNumber)
        {
            if (string.IsNullOrWhiteSpace(mobileNumber))
                return false;

            // بررسی فرمت شماره موبایل ایرانی (09xxxxxxxxx)
            return System.Text.RegularExpressions.Regex.IsMatch(mobileNumber, @"^09\d{9}$");
        }

        /// <summary>
        /// ایجاد قالب‌های پیش‌فرض برای کاربران جدید
        /// </summary>
        public async Task CreateDefaultTemplatesForUserAsync(int userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // بررسی اینکه آیا کاربر قبلاً قالب‌های پیش‌فرض دارد یا نه (داخل Transaction)
                var existingTemplates = await _context.MessageTemplates
                    .Where(t => t.UserId == userId && t.IsDefault && !t.IsDeleted)
                    .ToListAsync();
                
                if (existingTemplates.Any())
                {
                    // فقط قالب‌های سیستمی که هرگز ویرایش نشده‌اند (UpdatedAt == null)
                    // قالب پیش‌فرض ویرایش‌شده باید دوباره تأیید ادمین بگیرد
                    var pendingDefaults = existingTemplates
                        .Where(t => t.ApprovalStatus != AdminApprovalStatuses.Approved && t.UpdatedAt == null)
                        .ToList();

                    if (pendingDefaults.Count > 0)
                    {
                        var now = DateTime.UtcNow;
                        foreach (var template in pendingDefaults)
                        {
                            template.ApprovalStatus = AdminApprovalStatuses.Approved;
                            template.ApprovedAt = now;
                            template.ApprovedByUserId = null;
                            template.RejectionReason = null;
                            // UpdatedAt را عمداً null نگه می‌داریم تا «ویرایش‌نشده سیستمی» مشخص بماند
                        }

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                        _logger.LogInformation(
                            "Auto-approved {Count} existing default templates for user {UserId}",
                            pendingDefaults.Count, userId);
                    }
                    else
                    {
                        await transaction.RollbackAsync();
                        _logger.LogInformation("User {UserId} already has default templates", userId);
                    }

                    return;
                }

                var defaultTemplates = new List<MessageTemplate>
                {
                    // دسته‌بندی مناسبت‌ها
                    new MessageTemplate
                    {
                        UserId = userId,
                        Name = "تبریک تولد",
                        Content = "سلام {{نام}} عزیز!\n\nتولدت مبارک 🎂\nامیدوارم سالی پر از شادی و موفقیت داشته باشی.\n\n{{نام برند}}",
                        Category = "مناسبت‌ها",
                        Description = "پیام تبریک تولد با امکان شخصی‌سازی نام و برند",
                        Icon = "🎂",
                        IsDefault = true,
                        IsActive = true,
                        ApprovalStatus = AdminApprovalStatuses.Approved,
                        ApprovedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    },
                    new MessageTemplate
                    {
                        UserId = userId,
                        Name = "سالگرد ازدواج",
                        Content = "سلام {{نام}} عزیز!\n\nسالگرد ازدواجتان مبارک 🎉\nآغاز سالی جدید از زندگی مشترکتان را تبریک می‌گوییم.\n\n{{نام برند}}",
                        Category = "مناسبت‌ها",
                        Description = "پیام تبریک سالگرد ازدواج",
                        Icon = "💍",
                        IsDefault = true,
                        IsActive = true,
                        ApprovalStatus = AdminApprovalStatuses.Approved,
                        ApprovedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    },
                    new MessageTemplate
                    {
                        UserId = userId,
                        Name = "نوروز مبارک",
                        Content = "سلام {{نام}} عزیز!\n\nنوروزتان پیروز و سالی پر از سلامتی، شادی و موفقیت مبارک! 🌸\n\n{{نام برند}}",
                        Category = "مناسبت‌ها",
                        Description = "پیام تبریک نوروز",
                        Icon = "🌸",
                        IsDefault = true,
                        IsActive = true,
                        ApprovalStatus = AdminApprovalStatuses.Approved,
                        ApprovedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    },

                    // دسته‌بندی فروشگاهی
                    new MessageTemplate
                    {
                        UserId = userId,
                        Name = "معرفی فروشگاه",
                        Content = "سلام {{نام}} عزیز!\n\nاز فروشگاه {{نام برند}} دیدن کنید.\nتخفیف‌های ویژه برای مشتریان وفادار داریم!\n\n📍 آدرس: [آدرس فروشگاه شما]",
                        Category = "فروشگاهی",
                        Description = "معرفی فروشگاه و دعوت به بازدید",
                        Icon = "🏪",
                        IsDefault = true,
                        IsActive = true,
                        ApprovalStatus = AdminApprovalStatuses.Approved,
                        ApprovedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    },
                    new MessageTemplate
                    {
                        UserId = userId,
                        Name = "تخفیف ویژه",
                        Content = "سلام {{نام}} عزیز!\n\nپیشنهاد ویژه برای شما:\n🎁 تخفیف ۲۰٪ روی تمام محصولات!\n\nکد تخفیف: SPECIAL20\n{{نام برند}}",
                        Category = "فروشگاهی",
                        Description = "اطلاع‌رسانی تخفیف‌های ویژه",
                        Icon = "🎁",
                        IsDefault = true,
                        IsActive = true,
                        ApprovalStatus = AdminApprovalStatuses.Approved,
                        ApprovedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    },
                    new MessageTemplate
                    {
                        UserId = userId,
                        Name = "یادآوری خرید",
                        Content = "سلام {{نام}} عزیز!\n\n{{نام برند}} را فراموش نکنید!\nآخرین خرید شما: {{تاریخ خرید}}\n\nاز خرید بعدی شما استقبال می‌کنیم! 🛒",
                        Category = "فروشگاهی",
                        Description = "یادآوری فروشگاه و تاریخ آخرین خرید",
                        Icon = "🛒",
                        IsDefault = true,
                        IsActive = true,
                        ApprovalStatus = AdminApprovalStatuses.Approved,
                        ApprovedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    },

                    // دسته‌بندی خوش‌آمدگویی
                    new MessageTemplate
                    {
                        UserId = userId,
                        Name = "خوش‌آمدگویی مشتری جدید",
                        Content = "سلام {{نام}} عزیز! 👋\n\nبه خانواده {{نام برند}} خوش آمدید!\nاز عضویت شما سپاسگزاریم.\n\n🎁 هدیه ویژه عضویت: {{مبلغ کش بک}} تومان اعتبار\nتاریخ عضویت: {{تاریخ عضویت}}",
                        Category = "خوش‌آمدگویی",
                        Description = "خوش‌آمدگویی به مشتریان جدید با نمایش اعتبار هدیه",
                        Icon = "👋",
                        IsDefault = true,
                        IsActive = true,
                        ApprovalStatus = AdminApprovalStatuses.Approved,
                        ApprovedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    },

                    // دسته‌بندی یادآوری
                    new MessageTemplate
                    {
                        UserId = userId,
                        Name = "یادآوری شارژ حساب",
                        Content = "سلام {{نام}} عزیز!\n\nاعتبار کیف پول شما: {{مبلغ کش بک}} تومان\n\nبرای شارژ حساب و استفاده از اعتبار خود اقدام کنید.\n\n{{نام برند}} 💰",
                        Category = "یادآوری",
                        Description = "یادآوری موجودی کیف پول و دعوت به شارژ",
                        Icon = "💰",
                        IsDefault = true,
                        IsActive = true,
                        ApprovalStatus = AdminApprovalStatuses.Approved,
                        ApprovedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    },
                    new MessageTemplate
                    {
                        UserId = userId,
                        Name = "یادآوری انقضای اعتبار",
                        Content = "سلام {{نام}} عزیز!\n\nاعتبار {{مبلغ کش بک}} تومانی شما تا ۲ روز دیگر منقضی می‌شود!\n\nبرای استفاده از اعتبار خود هرچه سریع‌تر اقدام کنید. ⏰\n\n{{نام برند}}",
                        Category = "یادآوری",
                        Description = "یادآوری نزدیک بودن انقضای اعتبار کیف پول",
                        Icon = "⏰",
                        IsDefault = true,
                        IsActive = true,
                        ApprovalStatus = AdminApprovalStatuses.Approved,
                        ApprovedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    },

                    // دسته‌بندی عمومی
                    new MessageTemplate
                    {
                        UserId = userId,
                        Name = "پیام عمومی",
                        Content = "سلام {{نام}} عزیز!\n\n{{نام برند}}\n\n[متن پیام شما]",
                        Category = "عمومی",
                        Description = "قالب عمومی برای پیام‌های مختلف",
                        Icon = "💬",
                        IsDefault = true,
                        IsActive = true,
                        ApprovalStatus = AdminApprovalStatuses.Approved,
                        ApprovedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    },
                    new MessageTemplate
                    {
                        UserId = userId,
                        Name = "اطلاع‌رسانی",
                        Content = "سلام {{نام}} عزیز!\n\nاطلاعیه مهم از {{نام برند}}:\n\n[متن اطلاعیه شما]\n\nبا تشکر",
                        Category = "عمومی",
                        Description = "برای ارسال اطلاعیه‌های مهم",
                        Icon = "📢",
                        IsDefault = true,
                        IsActive = true,
                        ApprovalStatus = AdminApprovalStatuses.Approved,
                        ApprovedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    }
                };

                // اضافه کردن قالب‌ها به دیتابیس (استفاده از Context مستقیم در Transaction)
                foreach (var template in defaultTemplates)
                {
                    await _context.MessageTemplates.AddAsync(template);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("✅ Created {Count} default templates for user {UserId}", defaultTemplates.Count, userId);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(ex, "⚠️ Concurrency conflict while creating default templates for user {UserId}", userId);
                // در صورت تداخل، قالب‌ها ممکن است توسط درخواست دیگری ایجاد شده باشند
                // این خطا را نادیده می‌گیریم و ادامه می‌دهیم
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "❌ Database error creating default templates for user {UserId}", userId);
                throw;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "❌ Error creating default templates for user {UserId}", userId);
                throw;
            }
        }

        #endregion
    }
}

