using Api_Vapp.DTOs.Automation;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text.Json;

namespace Api_Vapp.Services
{
    /// <summary>
    /// پیاده‌سازی سرویس مدیریت پیام‌های خودکار
    /// </summary>
    public class AutomatedMessageService : IAutomatedMessageService
    {
        private readonly IAutomatedMessageRepository _automatedMessageRepository;
        private readonly IMessageRepository _messageRepository;
        private readonly IContactRepository _contactRepository;
        private readonly IContactNotebookRepository _notebookRepository;
        private readonly IMessageSessionRepository _sessionRepository;
        private readonly ISpecialOccasionRepository _specialOccasionRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMessageCampaignRepository _campaignRepository;
        private readonly Api_Vapp.Data.Api_Context _context;
        private readonly ILogger<AutomatedMessageService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly IServiceProvider _serviceProvider;

        // هزینه هر قسمت پیام (قابل تنظیم از appsettings)
        private readonly decimal _costPerPart = 160; // تومان

        /// <summary>
        /// بررسی اینکه آیا چک کردن کیف پول غیرفعال است یا نه
        /// </summary>
        private bool IsWalletCheckDisabled()
        {
            var environmentName = _hostEnvironment.EnvironmentName;
            return _configuration.GetValue<bool>($"{environmentName}:DisableWalletCheck", false);
        }

        public AutomatedMessageService(
            IAutomatedMessageRepository automatedMessageRepository,
            IMessageRepository messageRepository,
            IContactRepository contactRepository,
            IContactNotebookRepository notebookRepository,
            IMessageSessionRepository sessionRepository,
            ISpecialOccasionRepository specialOccasionRepository,
            IUserRepository userRepository,
            IMessageCampaignRepository campaignRepository,
            Api_Vapp.Data.Api_Context context,
            ILogger<AutomatedMessageService> logger,
            IConfiguration configuration,
            IHostEnvironment hostEnvironment,
            IServiceProvider serviceProvider)
        {
            _automatedMessageRepository = automatedMessageRepository;
            _messageRepository = messageRepository;
            _contactRepository = contactRepository;
            _notebookRepository = notebookRepository;
            _sessionRepository = sessionRepository;
            _specialOccasionRepository = specialOccasionRepository;
            _userRepository = userRepository;
            _campaignRepository = campaignRepository;
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _hostEnvironment = hostEnvironment;
            _serviceProvider = serviceProvider;
        }

        public Task<ApiResponse<AutomationTypeListResponseDto>> GetAutomationTypesAsync(int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                // اعتبارسنجی پارامترهای پیجینیشن
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                var allTypes = new List<AutomationTypeDto>
                {
                    new AutomationTypeDto
                    {
                        Type = "Birthday",
                        Name = "تبریک تولد",
                        Description = "ارسال پیام خودکار در روز تولد مشتریان",
                        Icon = "🎂"
                    },
                    new AutomationTypeDto
                    {
                        Type = "CashbackExpiry",
                        Name = "یادآوری انقضای کش بک",
                        Description = "۲ روز قبل از پایان اعتبار کش بک برای مشتری پیام ارسال می‌شود",
                        Icon = "💰"
                    },
                    new AutomationTypeDto
                    {
                        Type = "Welcome",
                        Name = "پیام خوش آمدگویی",
                        Description = "پس از اولین ثبت شماره مشتری، پیام خوش آمدگویی ارسال می‌شود",
                        Icon = "👋"
                    },
                    new AutomationTypeDto
                    {
                        Type = "PurchaseReminder",
                        Name = "یادآوری خرید",
                        Description = "اگر مشتری ۳۰ روز خرید نداشته باشد، پیام ارسال می‌شود",
                        Icon = "🛒"
                    },
                    new AutomationTypeDto
                    {
                        Type = "SpecialOccasion",
                        Name = "مناسبت های خاص",
                        Description = "ارسال پیام در مناسبت‌های مخصوص سال",
                        Icon = "🎉"
                    },
                    new AutomationTypeDto
                    {
                        Type = "Custom",
                        Name = "اتوماسیون سفارشی",
                        Description = "شرط، زمان و پیام را خودتان مشخص کنید",
                        Icon = "⚡"
                    }
                };

                var totalCount = allTypes.Count;
                var totalPages = totalCount > 0 ? (int)Math.Ceiling(totalCount / (double)pageSize) : 0;

                // بررسی اینکه pageNumber از totalPages بیشتر نباشد
                if (totalPages > 0 && pageNumber > totalPages)
                {
                    pageNumber = totalPages;
                }

                // اعمال پیجینیشن
                var paginatedTypes = allTypes
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var response = new AutomationTypeListResponseDto
                {
                    Types = paginatedTypes,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                return Task.FromResult(ApiResponse<AutomationTypeListResponseDto>.CreateSuccess(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت انواع اتوماسیون");
                return Task.FromResult(ApiResponse<AutomationTypeListResponseDto>.InternalServerError("خطا در دریافت انواع اتوماسیون"));
            }
        }

        public async Task<ApiResponse<AutomatedMessageResponseDto>> CreateAutomatedMessageDraftAsync(int userId, CreateAutomatedMessageDraftDto createDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // بررسی صحت نوع اتوماسیون
                var validTypes = new[] { "Birthday", "CashbackExpiry", "Welcome", "PurchaseReminder", "SpecialOccasion", "Custom" };
                if (!validTypes.Contains(createDto.AutomationType))
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageResponseDto>.BadRequest("نوع اتوماسیون نامعتبر است");
                }

                // ایجاد Message با Status = "Draft" و Content = ""
                var message = new Message
                {
                    UserId = userId,
                    Content = "",
                    CharacterCount = 0,
                    PartsCount = 0,
                    IsPersonalized = false,
                    Placeholders = null,
                    Status = "Draft",
                    CreatedAt = DateTime.UtcNow
                };

                var createdMessage = await _messageRepository.AddAsync(message);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Message created for automated message draft - MessageId: {MessageId}, UserId: {UserId}", 
                    createdMessage.Id, userId);

                // ایجاد AutomatedMessage با Status = "Draft"
                var automatedMessage = new AutomatedMessage
                {
                    UserId = userId,
                    AutomationType = createDto.AutomationType,
                    Title = GetDefaultTitle(createDto.AutomationType),
                    Description = GetDefaultDescription(createDto.AutomationType),
                    MessageId = createdMessage.Id,
                    Status = "Active", // پیش‌فرض فعال است
                    Icon = GetDefaultIcon(createDto.AutomationType),
                    IsActive = true, // پیش‌فرض فعال است
                    CreatedAt = DateTime.UtcNow
                };

                var created = await _automatedMessageRepository.AddAsync(automatedMessage);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                _logger.LogInformation("Automated message draft created successfully - AutomatedMessageId: {Id}, MessageId: {MessageId}, UserId: {UserId}", 
                    created.Id, createdMessage.Id, userId);

                return ApiResponse<AutomatedMessageResponseDto>.CreateSuccess(
                    MapToAutomatedMessageResponseDto(created),
                    "پیام خودکار با موفقیت ایجاد شد",
                    201
                );
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "خطای دیتابیس در ایجاد پیش‌نویس پیام خودکار برای کاربر: {UserId}", userId);
                return ApiResponse<AutomatedMessageResponseDto>.InternalServerError("خطا در ذخیره‌سازی پیام خودکار. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "خطای غیرمنتظره در ایجاد پیش‌نویس پیام خودکار برای کاربر: {UserId}", userId);
                return ApiResponse<AutomatedMessageResponseDto>.InternalServerError("خطای غیرمنتظره در ایجاد پیام خودکار");
            }
        }

        public async Task<ApiResponse<AutomatedMessageResponseDto>> CreateAutomatedMessageAsync(int userId, CreateAutomatedMessageDto createDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // بررسی صحت نوع اتوماسیون
                var validTypes = new[] { "Birthday", "CashbackExpiry", "Welcome", "PurchaseReminder", "SpecialOccasion", "Custom" };
                if (!validTypes.Contains(createDto.AutomationType))
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageResponseDto>.BadRequest("نوع اتوماسیون نامعتبر است");
                }

                // بررسی نیاز به MessageId یا MessageContent
                if (createDto.MessageId == null && string.IsNullOrWhiteSpace(createDto.MessageContent))
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageResponseDto>.BadRequest("باید پیام یا شناسه پیام مشخص شود");
                }

                // بررسی وجود MessageId در صورت ارسال
                if (createDto.MessageId.HasValue)
                {
                    var message = await _messageRepository.GetByIdAsync(createDto.MessageId.Value);
                    if (message == null || message.UserId != userId)
                    {
                        await transaction.RollbackAsync();
                        return ApiResponse<AutomatedMessageResponseDto>.NotFound("پیام یافت نشد");
                    }
                }

                // بررسی نیاز به DaysBeforeEvent برای برخی انواع
                if ((createDto.AutomationType == "CashbackExpiry" || createDto.AutomationType == "PurchaseReminder") 
                    && !createDto.DaysBeforeEvent.HasValue)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageResponseDto>.BadRequest("تعداد روز قبل از رویداد باید مشخص شود");
                }

                // بررسی نیاز به SpecialOccasionId
                if (createDto.AutomationType == "SpecialOccasion" && !createDto.SpecialOccasionId.HasValue)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageResponseDto>.BadRequest("مناسبت خاص باید انتخاب شود");
                }

                // بررسی صحت ScheduledTime
                if (createDto.ScheduledTime.HasValue)
                {
                    if (createDto.ScheduledTime.Value.TotalHours < 0 || createDto.ScheduledTime.Value.TotalHours >= 24)
                    {
                        await transaction.RollbackAsync();
                        return ApiResponse<AutomatedMessageResponseDto>.BadRequest("زمان ارسال باید بین 00:00 تا 23:59 باشد");
                    }
                }

                var automatedMessage = new AutomatedMessage
                {
                    UserId = userId,
                    AutomationType = createDto.AutomationType,
                    Title = createDto.Title,
                    Description = createDto.Description,
                    MessageId = createDto.MessageId,
                    MessageContent = createDto.MessageContent,
                    DaysBeforeEvent = createDto.DaysBeforeEvent,
                    SpecialOccasionId = createDto.SpecialOccasionId,
                    ActivationConditions = createDto.ActivationConditions,
                    ScheduledTime = createDto.ScheduledTime,
                    Icon = createDto.Icon ?? GetDefaultIcon(createDto.AutomationType),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var created = await _automatedMessageRepository.AddAsync(automatedMessage);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Automated message created successfully with ID: {Id} by user: {UserId}", 
                    created.Id, userId);

                return ApiResponse<AutomatedMessageResponseDto>.CreateSuccess(
                    MapToAutomatedMessageResponseDto(created),
                    "پیام خودکار با موفقیت ایجاد شد",
                    201
                );
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "خطای دیتابیس در ایجاد پیام خودکار برای کاربر: {UserId}", userId);
                return ApiResponse<AutomatedMessageResponseDto>.InternalServerError("خطا در ذخیره‌سازی پیام خودکار. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "خطای غیرمنتظره در ایجاد پیام خودکار برای کاربر: {UserId}", userId);
                return ApiResponse<AutomatedMessageResponseDto>.InternalServerError("خطای غیرمنتظره در ایجاد پیام خودکار");
            }
        }

        public async Task<ApiResponse<RecipientListForAutomatedMessageResponseDto>> SelectRecipientsForAutomatedMessageAsync(int userId, int automatedMessageId, SelectRecipientsForAutomatedMessageDto selectDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            List<RecipientItemForAutomatedMessageDto> recipients = new List<RecipientItemForAutomatedMessageDto>();
            try
            {
                // بررسی وجود AutomatedMessage و مالکیت
                var automatedMessage = await _automatedMessageRepository.GetByIdWithMessageAsync(automatedMessageId);
                if (automatedMessage == null || automatedMessage.UserId != userId)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<RecipientListForAutomatedMessageResponseDto>.NotFound(
                        "پیام خودکار یافت نشد یا شما مجاز به دسترسی به این پیام خودکار نیستید");
                }

                // بررسی وجود Message
                if (automatedMessage.MessageId == null)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<RecipientListForAutomatedMessageResponseDto>.BadRequest(
                        "پیام مربوطه به این پیام خودکار یافت نشد");
                }

                var message = await _messageRepository.GetByIdAsync(automatedMessage.MessageId.Value);
                if (message == null || message.UserId != userId)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<RecipientListForAutomatedMessageResponseDto>.BadRequest(
                        "پیام یافت نشد یا شما مجاز به دسترسی به این پیام نیستید");
                }

                recipients = new List<RecipientItemForAutomatedMessageDto>();

                // دریافت مخاطبین بر اساس ApplyToAllContacts
                if (selectDto.ApplyToAllContacts)
                {
                    // دریافت همه دفترچه‌های کاربر
                    var notebookIds = await _context.ContactNotebooks
                        .Where(n => n.UserId == userId && !n.IsDeleted)
                        .Select(n => n.Id)
                        .ToListAsync();

                    // دریافت همه مخاطبین از همه دفترچه‌ها
                    var allContacts = await _context.Contacts
                        .Include(c => c.AdditionalInfo)
                        .Where(c => notebookIds.Contains(c.ContactNotebookId) && !c.IsDeleted)
                        .ToListAsync();

                    // اعمال فیلتر بر اساس نوع پیام خودکار
                    var filteredContacts = await FilterContactsByAutomationTypeAsync(
                        allContacts, 
                        automatedMessage.AutomationType, 
                        userId);

                    recipients.AddRange(filteredContacts);
                }
                else
                {
                    // اگر ApplyToAllContacts = false، باید ContactNotebookId مشخص شود
                    if (!selectDto.ContactNotebookId.HasValue || selectDto.ContactNotebookId.Value <= 0)
                    {
                        await transaction.RollbackAsync();
                        return ApiResponse<RecipientListForAutomatedMessageResponseDto>.BadRequest(
                            "باید یا 'اعمال برای همه مخاطبین' را انتخاب کنید یا یک دفترچه تلفن مشخص کنید");
                    }

                    // بررسی مالکیت دفترچه
                    var notebook = await _notebookRepository.GetByIdAsync(selectDto.ContactNotebookId.Value);
                    if (notebook == null || notebook.UserId != userId || notebook.IsDeleted)
                    {
                        await transaction.RollbackAsync();
                        return ApiResponse<RecipientListForAutomatedMessageResponseDto>.BadRequest(
                            "دفترچه تلفن یافت نشد یا شما مجاز به دسترسی به این دفترچه نیستید");
                    }

                    // دریافت همه مخاطبین دفترچه
                    var allContacts = await _contactRepository.GetByNotebookIdAsync(selectDto.ContactNotebookId.Value);
                    var validContacts = allContacts
                        .Where(c => !c.IsDeleted)
                        .ToList();

                    // حذف مخاطبینی که در ExcludedContactIds هستند
                    if (selectDto.ExcludedContactIds != null && selectDto.ExcludedContactIds.Any())
                    {
                        var excludedContactIds = selectDto.ExcludedContactIds.ToHashSet();
                        validContacts = validContacts
                            .Where(c => !excludedContactIds.Contains(c.Id))
                            .ToList();
                    }

                    // اعمال فیلتر بر اساس نوع پیام خودکار
                    var filteredContacts = await FilterContactsByAutomationTypeAsync(
                        validContacts, 
                        automatedMessage.AutomationType, 
                        userId);

                    recipients.AddRange(filteredContacts);
                }

                // حذف تکراری‌ها بر اساس شماره موبایل
                recipients = recipients
                    .GroupBy(r => r.MobileNumber)
                    .Select(g => g.First())
                    .ToList();

                // محاسبه تعداد واجد شرایط و غیر واجد شرایط
                var eligibleCount = recipients.Count(r => r.IsEligible);
                var ineligibleCount = recipients.Count(r => !r.IsEligible);

                // ایجاد اطلاعات واجد شرایط بودن
                var eligibilityInfo = CreateEligibilityInfo(
                    automatedMessage.AutomationType,
                    recipients.Count,
                    eligibleCount,
                    ineligibleCount);

                var response = new RecipientListForAutomatedMessageResponseDto
                {
                    Recipients = recipients,
                    TotalCount = recipients.Count,
                    EligibleCount = eligibleCount,
                    IneligibleCount = ineligibleCount,
                    EligibilityInfo = eligibilityInfo
                };

                // ذخیره Session - استفاده از Pessimistic Lock
                var existingSession = await _context.MessageSessions
                    .FromSqlRaw(
                        "SELECT * FROM MessageSessions WITH (UPDLOCK, ROWLOCK) WHERE MessageId = {0} AND UserId = {1} AND IsDeleted = 0",
                        message.Id, userId)
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefaultAsync();

                var selectionCriteria = new
                {
                    AutomatedMessageId = automatedMessageId,
                    ApplyToAllContacts = selectDto.ApplyToAllContacts,
                    ContactNotebookId = selectDto.ContactNotebookId,
                    ExcludedContactIds = selectDto.ExcludedContactIds
                };

                var selectionCriteriaJson = JsonSerializer.Serialize(selectionCriteria);
                var recipientsJson = JsonSerializer.Serialize(recipients);

                MessageSession session;
                if (existingSession != null)
                {
                    existingSession.SelectionCriteria = selectionCriteriaJson;
                    existingSession.RecipientsJson = recipientsJson;
                    existingSession.IsUsed = false;
                    existingSession.ExpiresAt = DateTime.UtcNow.AddHours(24);
                    existingSession.UpdatedAt = DateTime.UtcNow;

                    session = await _sessionRepository.UpdateAsync(existingSession);
                    _logger.LogInformation("Updated MessageSession for automated message - SessionId: {SessionId}, AutomatedMessageId: {AutomatedMessageId}, UserId: {UserId}",
                        session.Id, automatedMessageId, userId);
                }
                else
                {
                    session = new MessageSession
                    {
                        MessageId = message.Id,
                        UserId = userId,
                        SelectionCriteria = selectionCriteriaJson,
                        RecipientsJson = recipientsJson,
                        IsUsed = false,
                        ExpiresAt = DateTime.UtcNow.AddHours(24),
                        CreatedAt = DateTime.UtcNow
                    };

                    session = await _sessionRepository.AddAsync(session);
                    _logger.LogInformation("Created MessageSession for automated message - SessionId: {SessionId}, AutomatedMessageId: {AutomatedMessageId}, UserId: {UserId}",
                        session.Id, automatedMessageId, userId);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                response.SessionId = session.Id;

                _logger.LogInformation("Recipients selected for automated message - AutomatedMessageId: {AutomatedMessageId}, TotalCount: {TotalCount}, EligibleCount: {EligibleCount}, UserId: {UserId}",
                    automatedMessageId, recipients.Count, eligibleCount, userId);

                // اگر هیچ مخاطبی واجد شرایط نیست، لاگ دقیق مخاطبان غیر واجد شرایط
                if (eligibleCount == 0)
                {
                    _logger.LogWarning("❌ گیرنده واجد شرایطی برای اتوماسیون تولد {AutomatedMessageId} یافت نشد. جزئیات تمام گیرندگان:", automatedMessageId);
                    foreach (var recipient in recipients)
                    {
                        _logger.LogWarning("❌ گیرنده ناموفق - ContactId: {ContactId}, Name: {Name}, HasDateOfBirth: {HasDateOfBirth}, IsEligible: {IsEligible}",
                            recipient.ContactId, recipient.FullName, recipient.HasDateOfBirth, recipient.IsEligible);
                    }
                }

                return ApiResponse<RecipientListForAutomatedMessageResponseDto>.CreateSuccess(response);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(ex, "تداخل همزمانی در انتخاب گیرندگان برای پیام خودکار - AutomatedMessageId: {AutomatedMessageId}, UserId: {UserId}",
                    automatedMessageId, userId);
                return ApiResponse<RecipientListForAutomatedMessageResponseDto>.BadRequest(
                    "این Session در حال استفاده توسط درخواست دیگری است. لطفاً دوباره تلاش کنید.");
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "خطای دیتابیس در انتخاب گیرندگان برای پیام خودکار - AutomatedMessageId: {AutomatedMessageId}, UserId: {UserId}",
                    automatedMessageId, userId);
                return ApiResponse<RecipientListForAutomatedMessageResponseDto>.InternalServerError("خطا در ذخیره‌سازی گیرندگان. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "خطای غیرمنتظره در انتخاب گیرندگان برای پیام خودکار - AutomatedMessageId: {AutomatedMessageId}, UserId: {UserId}",
                    automatedMessageId, userId);
                return ApiResponse<RecipientListForAutomatedMessageResponseDto>.InternalServerError("خطای غیرمنتظره در انتخاب گیرندگان");
            }
        }

        public async Task<ApiResponse<AutomatedMessageResponseDto>> GetAutomatedMessageByIdAsync(int id, int userId)
        {
            try
            {
                var automatedMessage = await _automatedMessageRepository.GetByIdAsync(id);

                if (automatedMessage == null)
                {
                    return ApiResponse<AutomatedMessageResponseDto>.NotFound("پیام خودکار یافت نشد");
                }

                if (automatedMessage.UserId != userId)
                {
                    return ApiResponse<AutomatedMessageResponseDto>.Forbidden("شما مجاز به دسترسی به این پیام خودکار نیستید");
                }

                return ApiResponse<AutomatedMessageResponseDto>.CreateSuccess(
                    MapToAutomatedMessageResponseDto(automatedMessage)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت پیام خودکار: {Id}", id);
                return ApiResponse<AutomatedMessageResponseDto>.InternalServerError("خطا در دریافت پیام خودکار");
            }
        }

        public async Task<ApiResponse<AutomatedMessageListResponseDto>> GetAutomatedMessagesAsync(int userId, int pageNumber = 1, int pageSize = 10, string? filter = null)
        {
            try
            {
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                IEnumerable<AutomatedMessage> automatedMessages;

                if (filter == "Active")
                {
                    automatedMessages = await _automatedMessageRepository.GetActiveByUserIdAsync(userId);
                }
                else if (filter == "Inactive")
                {
                    var all = await _automatedMessageRepository.GetByUserIdAsync(userId);
                    automatedMessages = all.Where(am => !am.IsActive);
                }
                else
                {
                    automatedMessages = await _automatedMessageRepository.GetByUserIdAsync(userId);
                }

                var messagesList = automatedMessages.ToList();
                var totalCount = messagesList.Count;
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var pagedMessages = messagesList
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var messageDtos = pagedMessages.Select(MapToAutomatedMessageResponseDto).ToList();

                var response = new AutomatedMessageListResponseDto
                {
                    AutomatedMessages = messageDtos,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                return ApiResponse<AutomatedMessageListResponseDto>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت پیام‌های خودکار برای کاربر: {UserId}", userId);
                return ApiResponse<AutomatedMessageListResponseDto>.InternalServerError("خطا در دریافت لیست پیام‌های خودکار");
            }
        }

        /// <summary>
        /// ارسال پیام خودکار برای تست (نسخه ساده‌شده)
        /// </summary>
        private async Task SendAutomatedMessageForTestAsync(Api_Vapp.Data.Api_Context context, AutomatedMessage automatedMessage, Contact contact)
        {
            try
            {
                // دریافت محتوای پیام
                string messageContent = automatedMessage.MessageContent ?? string.Empty;

                if (automatedMessage.MessageId.HasValue)
                {
                    var message = await context.Messages
                        .FirstOrDefaultAsync(m => m.Id == automatedMessage.MessageId.Value);

                    if (message != null)
                    {
                        messageContent = message.Content;

                        // شخصی‌سازی پیام با اطلاعات مخاطب
                        messageContent = await PersonalizeMessageAsync(messageContent, contact, context);
                    }
                }
                else if (string.IsNullOrEmpty(messageContent))
                {
                    _logger.LogWarning("محتوای پیام برای پیام خودکار {Id} یافت نشد", automatedMessage.Id);
                    return;
                }

                // لاگ کردن ارسال
                _logger.LogInformation(
                    "ارسال آزمایشی: پیام خودکار {AutomationId} به مخاطب {ContactId} ({MobileNumber}). محتوا: {Content}",
                    automatedMessage.Id, contact.Id, contact.MobileNumber, messageContent);

                // ثبت اجرا
                var execution = new AutomationExecution
                {
                    AutomatedMessageId = automatedMessage.Id,
                    ContactId = contact.Id,
                    ExecutedAt = DateTime.UtcNow,
                    Status = "Success",
                    MessageContent = messageContent,
                    SentCount = 1
                };

                await context.AutomationExecutions.AddAsync(execution);

                // به‌روزرسانی LastExecutedAt
                automatedMessage.LastExecutedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ارسال پیام خودکار آزمایشی {AutomationId} به مخاطب {ContactId}",
                    automatedMessage.Id, contact.Id);
            }
        }

        /// <summary>
        /// شخصی‌سازی پیام با اطلاعات مخاطب
        /// </summary>
        private async Task<string> PersonalizeMessageAsync(string messageContent, Contact contact, Api_Vapp.Data.Api_Context context)
        {
            string result = messageContent;

            // دریافت نام کامل (FullName)
            var fullName = contact.FullName ?? "";

            // دریافت مبلغ کش بک (مجموع مبلغ‌های واریز شده)
            var totalCashback = await context.CashbackTransactions
                .Where(ct => ct.ContactId == contact.Id && ct.Status == "Deposited")
                .SumAsync(ct => (decimal?)ct.Amount) ?? 0;

            // دریافت آخرین تاریخ خرید (آخرین تراکنش کش بک)
            var lastPurchaseDate = await context.CashbackTransactions
                .Where(ct => ct.ContactId == contact.Id)
                .OrderByDescending(ct => ct.CreatedAt)
                .Select(ct => (DateTime?)ct.CreatedAt)
                .FirstOrDefaultAsync();

            // دریافت نام برند
            var brandName = contact.Brand ?? "";

            // تاریخ عضویت (تاریخ ایجاد مخاطب)
            var membershipDate = contact.CreatedAt;

            // جایگزینی placeholder ها با استفاده از Regex

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

            // پشتیبانی از فرمت‌های قدیمی (برای سازگاری با کد قبلی)
            result = result.Replace("{{FirstName}}", fullName);
            result = result.Replace("{{LastName}}", "");
            result = result.Replace("{{FullName}}", fullName);
            result = result.Replace("{{Brand}}", brandName);
            result = result.Replace("{{MobileNumber}}", contact.MobileNumber);

            return result;
        }

        private string ReplacePlaceholder(string text, string placeholder, string value, StringComparison comparison = StringComparison.Ordinal)
        {
            // Escape کردن کاراکترهای خاص در placeholder برای استفاده در Regex
            var escapedPlaceholder = System.Text.RegularExpressions.Regex.Escape(placeholder);
            var escapedValue = value;

            // جایگزینی با Regex برای جایگزینی همه موارد
            var pattern = escapedPlaceholder;
            return System.Text.RegularExpressions.Regex.Replace(text, pattern, escapedValue, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        }

        private string FormatAmount(decimal amount)
        {
            return $"{amount:N0} تومان";
        }

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

        /// <summary>
        /// تست فوری ارسال پیام خودکار تولد (فقط برای توسعه)
        /// </summary>
        public async Task<ApiResponse<string>> TestSendBirthdayMessagesNowAsync(int userId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<Api_Vapp.Data.Api_Context>();

                // دریافت تمام پیام‌های خودکار تولد فعال
                var birthdayAutomations = await context.AutomatedMessages
                    .Where(am => am.UserId == userId && am.IsActive && am.AutomationType == "Birthday" && !am.IsDeleted)
                    .Include(am => am.Message)
                    .ToListAsync();

                if (!birthdayAutomations.Any())
                {
                    return ApiResponse<string>.BadRequest("هیچ پیام خودکار تولد فعالی یافت نشد");
                }

                var today = DateTime.UtcNow.Date;
                var messagesSent = 0;

                foreach (var automation in birthdayAutomations)
                {
                    // دریافت مخاطبینی که امروز تولد دارند
                    var contactsWithBirthdayToday = await context.Contacts
                        .Include(c => c.ContactNotebook)
                        .Include(c => c.AdditionalInfo)
                        .Where(c => !c.IsDeleted
                            && c.ContactNotebook.UserId == userId
                            && c.AdditionalInfo != null
                            && c.AdditionalInfo.DateOfBirth.HasValue
                            && c.AdditionalInfo.DateOfBirth.Value.Date == today)
                        .ToListAsync();

                    foreach (var contact in contactsWithBirthdayToday)
                    {
                        // چک کردن آیا امروز ارسال شده یا نه
                        var todayExecutions = await context.AutomationExecutions
                            .Where(ae => ae.AutomatedMessageId == automation.Id
                                && ae.ContactId == contact.Id
                                && ae.ExecutedAt >= today && ae.ExecutedAt < today.AddDays(1))
                            .AnyAsync();

                        _logger.LogInformation("تست تولد: مخاطب {ContactId} ({Name}), اتوماسیون {AutomationId}, AlreadyExecutedToday: {AlreadyExecuted}",
                            contact.Id, contact.FullName, automation.Id, todayExecutions);

                        if (!todayExecutions)
                        {
                            // ارسال پیام
                            await SendAutomatedMessageForTestAsync(context, automation, contact);
                            messagesSent++;

                            _logger.LogInformation("ارسال تست: پیام تولد ارسال شد به مخاطب {ContactId} ({Name}) از طریق اتوماسیون {AutomationId}",
                                contact.Id, contact.FullName, automation.Id);
                        }
                    }

                    // بروزرسانی LastExecutedAt
                    automation.LastExecutedAt = DateTime.UtcNow;
                }

                await context.SaveChangesAsync();

                return ApiResponse<string>.CreateSuccess($"تست ارسال موفق: {messagesSent} پیام تولد ارسال شد", $"تعداد پیام‌های ارسال شده: {messagesSent}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ارسال آزمایشی پیام‌های تولد برای کاربر: {UserId}", userId);
                return ApiResponse<string>.InternalServerError("خطا در تست ارسال پیام‌های تولد");
            }
        }

        public async Task<ApiResponse<AutomatedMessageResponseDto>> UpdateAutomatedMessageAsync(int id, int userId, UpdateAutomatedMessageDto updateDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var automatedMessage = await _automatedMessageRepository.GetByIdAsync(id);

                if (automatedMessage == null)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageResponseDto>.NotFound("پیام خودکار یافت نشد");
                }

                if (automatedMessage.UserId != userId)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageResponseDto>.Forbidden("شما مجاز به ویرایش این پیام خودکار نیستید");
                }

                bool hasChanges = false;

                // به‌روزرسانی Title فقط در صورت ارسال مقدار غیرخالی
                if (updateDto.Title != null && updateDto.Title != automatedMessage.Title)
                {
                    automatedMessage.Title = updateDto.Title.Trim();
                    hasChanges = true;
                }

                // به‌روزرسانی Description فقط در صورت ارسال مقدار
                if (updateDto.Description != null && updateDto.Description != automatedMessage.Description)
                {
                    automatedMessage.Description = string.IsNullOrWhiteSpace(updateDto.Description) ? null : updateDto.Description.Trim();
                    hasChanges = true;
                }

                // به‌روزرسانی MessageId
                if (updateDto.MessageId.HasValue)
                {
                    // اگر MessageId صفر ارسال شد، یعنی حذف MessageId
                    if (updateDto.MessageId.Value == 0)
                    {
                        if (automatedMessage.MessageId != null)
                        {
                            automatedMessage.MessageId = null;
                            hasChanges = true;
                        }
                    }
                    // اگر MessageId جدید ارسال شد
                    else if (updateDto.MessageId != automatedMessage.MessageId)
                    {
                        var message = await _messageRepository.GetByIdAsync(updateDto.MessageId.Value);
                        if (message == null || message.UserId != userId)
                        {
                            await transaction.RollbackAsync();
                            return ApiResponse<AutomatedMessageResponseDto>.NotFound("پیام یافت نشد");
                        }
                        automatedMessage.MessageId = updateDto.MessageId;
                        hasChanges = true;
                    }
                }

                // به‌روزرسانی MessageContent فقط در صورت ارسال مقدار غیرخالی
                if (updateDto.MessageContent != null && updateDto.MessageContent != automatedMessage.MessageContent)
                {
                    automatedMessage.MessageContent = string.IsNullOrWhiteSpace(updateDto.MessageContent) ? null : updateDto.MessageContent.Trim();
                    hasChanges = true;
                }

                // به‌روزرسانی DaysBeforeEvent
                if (updateDto.DaysBeforeEvent.HasValue && updateDto.DaysBeforeEvent != automatedMessage.DaysBeforeEvent)
                {
                    automatedMessage.DaysBeforeEvent = updateDto.DaysBeforeEvent;
                    hasChanges = true;
                }

                // به‌روزرسانی SpecialOccasionId
                if (updateDto.SpecialOccasionId.HasValue)
                {
                    // اگر صفر ارسال شد، یعنی حذف SpecialOccasionId
                    if (updateDto.SpecialOccasionId.Value == 0)
                    {
                        if (automatedMessage.SpecialOccasionId != null)
                        {
                            automatedMessage.SpecialOccasionId = null;
                            hasChanges = true;
                        }
                    }
                    else if (updateDto.SpecialOccasionId != automatedMessage.SpecialOccasionId)
                    {
                        automatedMessage.SpecialOccasionId = updateDto.SpecialOccasionId;
                        hasChanges = true;
                    }
                }

                // به‌روزرسانی ActivationConditions فقط در صورت ارسال مقدار
                if (updateDto.ActivationConditions != null && updateDto.ActivationConditions != automatedMessage.ActivationConditions)
                {
                    automatedMessage.ActivationConditions = string.IsNullOrWhiteSpace(updateDto.ActivationConditions) ? null : updateDto.ActivationConditions.Trim();
                    hasChanges = true;
                }

                // به‌روزرسانی ScheduledTime
                if (updateDto.ScheduledTime.HasValue)
                {
                    if (updateDto.ScheduledTime.Value.TotalHours < 0 || updateDto.ScheduledTime.Value.TotalHours >= 24)
                    {
                        await transaction.RollbackAsync();
                        return ApiResponse<AutomatedMessageResponseDto>.BadRequest("زمان ارسال باید بین 00:00 تا 23:59 باشد");
                    }

                    if (updateDto.ScheduledTime != automatedMessage.ScheduledTime)
                    {
                        automatedMessage.ScheduledTime = updateDto.ScheduledTime;
                        hasChanges = true;
                    }
                }

                // به‌روزرسانی Icon فقط در صورت ارسال مقدار
                if (updateDto.Icon != null && updateDto.Icon != automatedMessage.Icon)
                {
                    automatedMessage.Icon = string.IsNullOrWhiteSpace(updateDto.Icon) ? null : updateDto.Icon.Trim();
                    hasChanges = true;
                }

                // به‌روزرسانی IsActive
                if (updateDto.IsActive.HasValue && updateDto.IsActive.Value != automatedMessage.IsActive)
                {
                    automatedMessage.IsActive = updateDto.IsActive.Value;
                    hasChanges = true;
                }

                // اگر هیچ تغییری ایجاد نشد
                if (!hasChanges)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageResponseDto>.CreateSuccess(
                        MapToAutomatedMessageResponseDto(automatedMessage),
                        "هیچ تغییری اعمال نشد"
                    );
                }

                automatedMessage.UpdatedAt = DateTime.UtcNow;

                var updated = await _automatedMessageRepository.UpdateAsync(automatedMessage);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Automated message updated successfully with ID: {Id}", id);

                return ApiResponse<AutomatedMessageResponseDto>.CreateSuccess(
                    MapToAutomatedMessageResponseDto(updated),
                    "پیام خودکار با موفقیت به‌روزرسانی شد"
                );
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "خطای دیتابیس در به‌روزرسانی پیام خودکار: {Id}", id);
                return ApiResponse<AutomatedMessageResponseDto>.InternalServerError("خطا در به‌روزرسانی پیام خودکار. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "خطای غیرمنتظره در به‌روزرسانی پیام خودکار: {Id}", id);
                return ApiResponse<AutomatedMessageResponseDto>.InternalServerError("خطای غیرمنتظره در به‌روزرسانی پیام خودکار");
            }
        }

        public async Task<ApiResponse<bool>> DeleteAutomatedMessageAsync(int id, int userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var automatedMessage = await _automatedMessageRepository.GetByIdAsync(id);

                if (automatedMessage == null)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<bool>.NotFound("پیام خودکار یافت نشد");
                }

                if (automatedMessage.UserId != userId)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<bool>.Forbidden("شما مجاز به حذف این پیام خودکار نیستید");
                }

                // بررسی استفاده در کمپین‌های فعال
                var activeCampaigns = await _context.MessageCampaigns
                    .Where(c => c.AutomatedMessageId == id
                                && c.IsActive
                                && c.Status != "Draft"
                                && c.Status != "Cancelled"
                                && c.Status != "Completed"
                                && !c.IsDeleted)
                    .ToListAsync();

                if (activeCampaigns.Any())
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<bool>.BadRequest(
                        $"این پیام خودکار در {activeCampaigns.Count} کمپین فعال استفاده شده و قابل حذف نیست. لطفاً ابتدا کمپین‌ها را لغو یا تکمیل کنید.");
                }

                // Soft Delete
                automatedMessage.IsDeleted = true;
                automatedMessage.UpdatedAt = DateTime.UtcNow;
                await _automatedMessageRepository.UpdateAsync(automatedMessage);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Automated message deleted successfully with ID: {Id}", id);

                return ApiResponse<bool>.CreateSuccess(true, "پیام خودکار با موفقیت حذف شد");
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "خطای دیتابیس در حذف پیام خودکار: {Id}", id);
                return ApiResponse<bool>.InternalServerError("خطا در حذف پیام خودکار. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "خطای غیرمنتظره در حذف پیام خودکار: {Id}", id);
                return ApiResponse<bool>.InternalServerError("خطای غیرمنتظره در حذف پیام خودکار");
            }
        }

        public async Task<ApiResponse<bool>> ToggleAutomatedMessageStatusAsync(int id, int userId, bool isActive)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var automatedMessage = await _automatedMessageRepository.GetByIdAsync(id);

                if (automatedMessage == null)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<bool>.NotFound("پیام خودکار یافت نشد");
                }

                if (automatedMessage.UserId != userId)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<bool>.Forbidden("شما مجاز به تغییر وضعیت این پیام خودکار نیستید");
                }

                automatedMessage.IsActive = isActive;
                automatedMessage.UpdatedAt = DateTime.UtcNow;
                await _automatedMessageRepository.UpdateAsync(automatedMessage);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Automated message status toggled to {Status} for ID: {Id}", isActive ? "Active" : "Inactive", id);

                return ApiResponse<bool>.CreateSuccess(true, $"پیام خودکار با موفقیت {(isActive ? "فعال" : "غیرفعال")} شد");
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "خطای دیتابیس در تغییر وضعیت پیام خودکار: {Id}", id);
                return ApiResponse<bool>.InternalServerError("خطا در تغییر وضعیت پیام خودکار. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "خطای غیرمنتظره در تغییر وضعیت پیام خودکار: {Id}", id);
                return ApiResponse<bool>.InternalServerError("خطای غیرمنتظره در تغییر وضعیت پیام خودکار");
            }
        }

        #region Helper Methods

        /// <summary>
        /// فیلتر کردن مخاطبین بر اساس نوع پیام خودکار
        /// </summary>
        private async Task<List<RecipientItemForAutomatedMessageDto>> FilterContactsByAutomationTypeAsync(
            List<Contact> contacts, 
            string automationType, 
            int userId)
        {
            var recipients = new List<RecipientItemForAutomatedMessageDto>();

            // بهینه‌سازی: دریافت همه CashbackTransactions یکجا (فقط برای CashbackExpiry)
            HashSet<int>? contactsWithCashback = null;
            if (automationType == "CashbackExpiry")
            {
                var contactIds = contacts.Select(c => c.Id).ToList();
                var cashbackContactIds = await _context.CashbackTransactions
                    .Where(ct => contactIds.Contains(ct.ContactId) && 
                                ct.Status == "Deposited")
                    .Select(ct => ct.ContactId)
                    .Distinct()
                    .ToListAsync();
                contactsWithCashback = cashbackContactIds.ToHashSet();
            }

            foreach (var contact in contacts)
            {
                var recipient = new RecipientItemForAutomatedMessageDto
                {
                    ContactId = contact.Id,
                    MobileNumber = contact.MobileNumber,
                    FullName = contact.FullName,
                    IsEligible = true
                };

                // فیلتر بر اساس نوع پیام خودکار
                if (automationType == "Birthday")
                {
                    // بررسی وضعیت هر مخاطب برای birthday
                    var additionalInfo = contact.AdditionalInfo;
                    var hasAdditionalInfo = additionalInfo != null;
                    var hasDateOfBirth = additionalInfo?.DateOfBirth.HasValue == true;

                    // لاگ دقیق وضعیت هر مخاطب
                    _logger.LogInformation("بررسی تولد - مخاطب {ContactId} ({Name}): " +
                        "HasAdditionalInfo: {HasAdditionalInfo}, " +
                        "DateOfBirth: {DateOfBirth}, " +
                        "IsEligible: {IsEligible}",
                        contact.Id, contact.FullName ?? "بدون نام",
                        hasAdditionalInfo,
                        additionalInfo?.DateOfBirth?.ToString("yyyy-MM-dd HH:mm:ss") ?? "null",
                        hasDateOfBirth);

                    recipient.HasDateOfBirth = hasDateOfBirth;
                    recipient.IsEligible = hasDateOfBirth;
                }
                else if (automationType == "CashbackExpiry")
                {
                    // بررسی کش‌بک (از HashSet که قبلاً ایجاد کردم)
                    var hasCashback = contactsWithCashback != null && 
                                     contactsWithCashback.Contains(contact.Id);
                    recipient.HasCashback = hasCashback;
                    // فعلاً همه واجد شرایط هستند (سیستم کامل نیست)
                    recipient.IsEligible = true;
                }
                else
                {
                    // برای سایر انواع (Welcome, PurchaseReminder, SpecialOccasion, Custom)
                    // همه واجد شرایط هستند
                    recipient.IsEligible = true;
                }

                recipients.Add(recipient);
            }

            return recipients;
        }

        /// <summary>
        /// ایجاد اطلاعات واجد شرایط بودن
        /// </summary>
        private EligibilityInfoDto CreateEligibilityInfo(
            string automationType,
            int totalCount,
            int eligibleCount,
            int ineligibleCount)
        {
            var info = new EligibilityInfoDto();

            switch (automationType)
            {
                case "Birthday":
                    info.Message = $"از {totalCount} مخاطب، {eligibleCount} نفر تاریخ تولد دارند";
                    if (ineligibleCount > 0)
                    {
                        info.Warning = "⚠️ توجه: فقط به مخاطبینی که تاریخ تولد ثبت شده دارند، پیام ارسال می‌شود.";
                    }
                    break;

                case "CashbackExpiry":
                    info.Message = $"از {totalCount} مخاطب انتخاب شده";
                    info.Warning = "⚠️ توجه: سیستم کش‌بک در حال توسعه است. فعلاً به همه مخاطبین انتخاب شده پیام ارسال می‌شود.";
                    break;

                case "Welcome":
                    info.Message = $"از {totalCount} مخاطب انتخاب شده";
                    info.Warning = "ℹ️ این پیام فقط به مخاطبین جدید (اولین بار ثبت شماره) ارسال می‌شود.";
                    break;

                case "PurchaseReminder":
                    info.Message = $"از {totalCount} مخاطب انتخاب شده";
                    info.Warning = "⚠️ توجه: سیستم خرید در حال توسعه است. فعلاً به همه مخاطبین انتخاب شده پیام ارسال می‌شود.";
                    break;

                case "SpecialOccasion":
                    info.Message = $"از {totalCount} مخاطب انتخاب شده";
                    info.Warning = "ℹ️ این پیام در تاریخ مناسبت به همه مخاطبین انتخاب شده ارسال می‌شود.";
                    break;

                case "Custom":
                    info.Message = $"از {totalCount} مخاطب انتخاب شده";
                    info.Warning = "ℹ️ این پیام بر اساس شرایط سفارشی که تعریف کرده‌اید ارسال می‌شود.";
                    break;

                default:
                    info.Message = $"از {totalCount} مخاطب انتخاب شده";
                    break;
            }

            return info;
        }

        /// <summary>
        /// ایجاد پیام خطای سفارشی برای عدم وجود گیرندگان واجد شرایط
        /// </summary>
        private string GetEligibilityErrorMessage(string automationType, int totalRecipients, int eligibleCount)
        {
            var ineligibleCount = totalRecipients - eligibleCount;

            switch (automationType.ToLower())
            {
                case "birthday":
                    if (eligibleCount == 0)
                    {
                        return $"هیچ یک از {totalRecipients} مخاطب انتخاب شده تاریخ تولد ندارند. لطفاً مخاطبینی با تاریخ تولد انتخاب کنید.";
                    }
                    else
                    {
                        return $"از {totalRecipients} مخاطب انتخاب شده، {eligibleCount} نفر تاریخ تولد دارند و {ineligibleCount} نفر ندارند. پیام فقط به {eligibleCount} نفر ارسال خواهد شد.";
                    }

                case "cashbackexpiry":
                    if (eligibleCount == 0)
                    {
                        return $"هیچ یک از {totalRecipients} مخاطب انتخاب شده دارای اعتبار کش‌بک منقضی شده نیست. لطفاً مخاطبینی با اعتبار کش‌بک انتخاب کنید.";
                    }
                    else
                    {
                        return $"از {totalRecipients} مخاطب انتخاب شده، {eligibleCount} نفر اعتبار کش‌بک منقضی شده دارند و {ineligibleCount} نفر ندارند. پیام فقط به {eligibleCount} نفر ارسال خواهد شد.";
                    }

                case "purchasereminder":
                    if (eligibleCount == 0)
                    {
                        return $"هیچ یک از {totalRecipients} مخاطب انتخاب شده شرایط یادآوری خرید را ندارند. سیستم خرید در حال توسعه است.";
                    }
                    else
                    {
                        return $"از {totalRecipients} مخاطب انتخاب شده، {eligibleCount} نفر شرایط یادآوری خرید دارند و {ineligibleCount} نفر ندارند. پیام فقط به {eligibleCount} نفر ارسال خواهد شد.";
                    }

                case "specialoccasion":
                    if (eligibleCount == 0)
                    {
                        return $"هیچ یک از {totalRecipients} مخاطب انتخاب شده مناسبت تعریف شده ندارد. لطفاً مناسبت‌های خاص اضافه کنید.";
                    }
                    else
                    {
                        return $"از {totalRecipients} مخاطب انتخاب شده، {eligibleCount} نفر مناسبت تعریف شده دارند و {ineligibleCount} نفر ندارند. پیام فقط به {eligibleCount} نفر ارسال خواهد شد.";
                    }

                case "welcome":
                    // Welcome همیشه همه را eligible در نظر می‌گیرد
                    return "خطای غیرمنتظره: هیچ گیرنده واجد شرایط‌ای یافت نشد.";

                case "custom":
                    if (eligibleCount == 0)
                    {
                        return $"هیچ یک از {totalRecipients} مخاطب انتخاب شده شرایط اتوماسیون سفارشی شما را ندارند. لطفاً شرایط را بررسی کنید.";
                    }
                    else
                    {
                        return $"از {totalRecipients} مخاطب انتخاب شده، {eligibleCount} نفر شرایط اتوماسیون سفارشی شما را دارند و {ineligibleCount} نفر ندارند. پیام فقط به {eligibleCount} نفر ارسال خواهد شد.";
                    }

                default:
                    return $"هیچ گیرنده واجد شرایط‌ای برای این نوع پیام خودکار ({automationType}) یافت نشد.";
            }
        }

        /// <summary>
        /// ایجاد پیام هشدار برای ارسال جزئی (برخی واجد شرایط و برخی نیستند)
        /// </summary>
        private string GetPartialEligibilityWarningMessage(string automationType, int totalRecipients, int eligibleCount)
        {
            var ineligibleCount = totalRecipients - eligibleCount;

            switch (automationType.ToLower())
            {
                case "birthday":
                    return $"هشدار: از {totalRecipients} مخاطب انتخاب شده، فقط {eligibleCount} نفر تاریخ تولد دارند. {ineligibleCount} نفر بدون تاریخ تولد هستند و پیام دریاقت نخواهند کرد.";

                case "cashbackexpiry":
                    return $"هشدار: از {totalRecipients} مخاطب انتخاب شده، فقط {eligibleCount} نفر اعتبار کش‌بک منقضی شده دارند. {ineligibleCount} نفر شرایط لازم را ندارند.";

                case "purchasereminder":
                    return $"هشدار: از {totalRecipients} مخاطب انتخاب شده، فقط {eligibleCount} نفر شرایط یادآوری خرید دارند. سیستم خرید در حال توسعه است.";

                case "specialoccasion":
                    return $"هشدار: از {totalRecipients} مخاطب انتخاب شده، فقط {eligibleCount} نفر مناسبت تعریف شده دارند. {ineligibleCount} نفر مناسبت ندارند.";

                case "custom":
                    return $"هشدار: از {totalRecipients} مخاطب انتخاب شده، فقط {eligibleCount} نفر شرایط اتوماسیون سفارشی شما را دارند. {ineligibleCount} نفر شرایط را ندارند.";

                default:
                    return $"هشدار: از {totalRecipients} مخاطب انتخاب شده، فقط {eligibleCount} نفر واجد شرایط هستند. {ineligibleCount} نفر شرایط لازم را ندارند.";
            }
        }

        private string GetDefaultIcon(string automationType)
        {
            return automationType switch
            {
                "Birthday" => "🎂",
                "CashbackExpiry" => "💰",
                "Welcome" => "👋",
                "PurchaseReminder" => "🛒",
                "SpecialOccasion" => "🎉",
                "Custom" => "⚡",
                _ => "ðŸ“¨"
            };
        }

        private string GetDefaultTitle(string automationType)
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

        private string GetDefaultDescription(string automationType)
        {
            return automationType switch
            {
                "Birthday" => "ارسال پیام خودکار در روز تولد مشتریان",
                "CashbackExpiry" => "۲ روز قبل از پایان اعتبار کش بک برای مشتری پیام ارسال می‌شود",
                "Welcome" => "پس از اولین ثبت شماره مشتری، پیام خوش آمدگویی ارسال می‌شود",
                "PurchaseReminder" => "اگر مشتری ۳۰ روز خرید نداشته باشد، پیام ارسال می‌شود",
                "SpecialOccasion" => "ارسال پیام در مناسبت‌های مخصوص سال",
                "Custom" => "شرط، زمان و پیام را خودتان مشخص کنید",
                _ => "پیام خودکار سفارشی"
            };
        }

        private AutomatedMessageResponseDto MapToAutomatedMessageResponseDto(AutomatedMessage automatedMessage)
        {
            return new AutomatedMessageResponseDto
            {
                Id = automatedMessage.Id,
                AutomationType = automatedMessage.AutomationType,
                Title = automatedMessage.Title,
                Description = automatedMessage.Description,
                MessageId = automatedMessage.MessageId,
                MessageContent = automatedMessage.MessageContent,
                Icon = automatedMessage.Icon,
                Status = automatedMessage.Status,
                IsActive = automatedMessage.IsActive,
                LastExecutedAt = automatedMessage.LastExecutedAt,
                DaysBeforeEvent = automatedMessage.DaysBeforeEvent,
                ScheduledTime = automatedMessage.ScheduledTime,
                CreatedAt = automatedMessage.CreatedAt
            };
        }

        /// <summary>
        /// محاسبه تعداد روز باقی‌مانده تا مناسبت
        /// </summary>
        private int? CalculateDaysRemaining(DateTime occasionDate, DateTime today)
        {
            // فقط روز و ماه را در نظر می‌گیریم (سال مهم نیست)
            var thisYearOccasion = new DateTime(today.Year, occasionDate.Month, occasionDate.Day);
            var nextYearOccasion = new DateTime(today.Year + 1, occasionDate.Month, occasionDate.Day);

            DateTime targetOccasion;
            if (thisYearOccasion >= today)
            {
                targetOccasion = thisYearOccasion;
            }
            else
            {
                targetOccasion = nextYearOccasion;
            }

            return (int)(targetOccasion - today).TotalDays;
        }

        #endregion

        #region تنظیمات پایه (مرحله 3)

        public async Task<ApiResponse<AutomatedMessageResponseDto>> SaveBirthdaySettingsAsync(int automatedMessageId, int userId, BirthdaySettingsDto settingsDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // بررسی وجود AutomatedMessage و مالکیت
                var automatedMessage = await _automatedMessageRepository.GetByIdAsync(automatedMessageId);
                if (automatedMessage == null || automatedMessage.UserId != userId)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageResponseDto>.NotFound(
                        "پیام خودکار یافت نشد یا شما مجاز به دسترسی به این پیام خودکار نیستید");
                }

                // بررسی نوع اتوماسیون
                if (automatedMessage.AutomationType != "Birthday")
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageResponseDto>.BadRequest(
                        "این تنظیمات فقط برای نوع پیام خودکار 'تبریک تولد' قابل استفاده است");
                }

                // اعتبارسنجی ساعت
                if (!TimeSpan.TryParse(settingsDto.SendTime, out var sendTime))
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageResponseDto>.BadRequest(
                        "فرمت ساعت نامعتبر است. باید به صورت HH:mm باشد (مثال: 10:00)");
                }

                // ذخیره تنظیمات در ActivationConditions (JSON)
                var activationConditions = new
                {
                    sendTime = settingsDto.SendTime,
                    repeatYearly = settingsDto.RepeatYearly
                };

                automatedMessage.ActivationConditions = System.Text.Json.JsonSerializer.Serialize(activationConditions);
                automatedMessage.ScheduledTime = sendTime; // تنظیم زمان برنامه‌ریزی شده
                automatedMessage.UpdatedAt = DateTime.UtcNow;

                await _automatedMessageRepository.UpdateAsync(automatedMessage);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Birthday settings saved for automated message {Id} by user {UserId}",
                    automatedMessageId, userId);

                return ApiResponse<AutomatedMessageResponseDto>.CreateSuccess(
                    MapToAutomatedMessageResponseDto(automatedMessage),
                    "تنظیمات با موفقیت ذخیره شد");
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Database error saving birthday settings for automated message {Id}", automatedMessageId);
                return ApiResponse<AutomatedMessageResponseDto>.InternalServerError("خطا در ذخیره‌سازی تنظیمات. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Unexpected error saving birthday settings for automated message {Id}", automatedMessageId);
                return ApiResponse<AutomatedMessageResponseDto>.InternalServerError("خطای غیرمنتظره در ذخیره‌سازی تنظیمات");
            }
        }

        public async Task<ApiResponse<AutomatedMessageResponseDto>> SaveCashbackExpirySettingsAsync(int automatedMessageId, int userId, CashbackExpirySettingsDto settingsDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // بررسی وجود AutomatedMessage و مالکیت
                var automatedMessage = await _automatedMessageRepository.GetByIdAsync(automatedMessageId);
                if (automatedMessage == null || automatedMessage.UserId != userId)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageResponseDto>.NotFound(
                        "پیام خودکار یافت نشد یا شما مجاز به دسترسی به این پیام خودکار نیستید");
                }

                // بررسی نوع اتوماسیون
                if (automatedMessage.AutomationType != "CashbackExpiry")
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageResponseDto>.BadRequest(
                        "این تنظیمات فقط برای نوع پیام خودکار 'یادآوری انقضای کش‌بک' قابل استفاده است");
                }

                // ذخیره DaysBeforeEvent
                automatedMessage.DaysBeforeEvent = settingsDto.DaysBeforeExpiry;

                // ذخیره executionMode در ActivationConditions
                var activationConditions = new
                {
                    executionMode = settingsDto.ExecutionMode
                };

                automatedMessage.ActivationConditions = System.Text.Json.JsonSerializer.Serialize(activationConditions);
                automatedMessage.UpdatedAt = DateTime.UtcNow;

                await _automatedMessageRepository.UpdateAsync(automatedMessage);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Cashback expiry settings saved for automated message {Id} by user {UserId}",
                    automatedMessageId, userId);

                return ApiResponse<AutomatedMessageResponseDto>.CreateSuccess(
                    MapToAutomatedMessageResponseDto(automatedMessage),
                    "تنظیمات با موفقیت ذخیره شد");
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Database error saving cashback expiry settings for automated message {Id}", automatedMessageId);
                return ApiResponse<AutomatedMessageResponseDto>.InternalServerError("خطا در ذخیره‌سازی تنظیمات. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Unexpected error saving cashback expiry settings for automated message {Id}", automatedMessageId);
                return ApiResponse<AutomatedMessageResponseDto>.InternalServerError("خطای غیرمنتظره در ذخیره‌سازی تنظیمات");
            }
        }

        public async Task<ApiResponse<AutomatedMessageResponseDto>> SaveWelcomeSettingsAsync(int automatedMessageId, int userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // بررسی وجود AutomatedMessage و مالکیت
                var automatedMessage = await _automatedMessageRepository.GetByIdAsync(automatedMessageId);
                if (automatedMessage == null || automatedMessage.UserId != userId)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageResponseDto>.NotFound(
                        "پیام خودکار یافت نشد یا شما مجاز به دسترسی به این پیام خودکار نیستید");
                }

                // بررسی نوع اتوماسیون
                if (automatedMessage.AutomationType != "Welcome")
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageResponseDto>.BadRequest(
                        "این تنظیمات فقط برای نوع پیام خودکار 'پیام خوش‌آمدگویی' قابل استفاده است");
                }

                // پیام خوش‌آمدگویی معمولاً تنظیمات خاصی ندارد
                // فقط UpdatedAt را به‌روزرسانی می‌کنیم
                automatedMessage.UpdatedAt = DateTime.UtcNow;

                await _automatedMessageRepository.UpdateAsync(automatedMessage);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Welcome settings saved for automated message {Id} by user {UserId}",
                    automatedMessageId, userId);

                return ApiResponse<AutomatedMessageResponseDto>.CreateSuccess(
                    MapToAutomatedMessageResponseDto(automatedMessage),
                    "تنظیمات با موفقیت ذخیره شد");
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Database error saving welcome settings for automated message {Id}", automatedMessageId);
                return ApiResponse<AutomatedMessageResponseDto>.InternalServerError("خطا در ذخیره‌سازی تنظیمات. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Unexpected error saving welcome settings for automated message {Id}", automatedMessageId);
                return ApiResponse<AutomatedMessageResponseDto>.InternalServerError("خطای غیرمنتظره در ذخیره‌سازی تنظیمات");
            }
        }

        public async Task<ApiResponse<AutomatedMessageResponseDto>> SavePurchaseReminderSettingsAsync(int automatedMessageId, int userId, PurchaseReminderSettingsDto settingsDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // بررسی وجود AutomatedMessage و مالکیت
                var automatedMessage = await _automatedMessageRepository.GetByIdAsync(automatedMessageId);
                if (automatedMessage == null || automatedMessage.UserId != userId)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageResponseDto>.NotFound(
                        "پیام خودکار یافت نشد یا شما مجاز به دسترسی به این پیام خودکار نیستید");
                }

                // بررسی نوع اتوماسیون
                if (automatedMessage.AutomationType != "PurchaseReminder")
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageResponseDto>.BadRequest(
                        "این تنظیمات فقط برای نوع پیام خودکار 'یادآوری خرید' قابل استفاده است");
                }

                // ذخیره DaysBeforeEvent
                automatedMessage.DaysBeforeEvent = settingsDto.DaysWithoutPurchase;
                automatedMessage.UpdatedAt = DateTime.UtcNow;

                await _automatedMessageRepository.UpdateAsync(automatedMessage);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Purchase reminder settings saved for automated message {Id} by user {UserId}",
                    automatedMessageId, userId);

                return ApiResponse<AutomatedMessageResponseDto>.CreateSuccess(
                    MapToAutomatedMessageResponseDto(automatedMessage),
                    "تنظیمات با موفقیت ذخیره شد");
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Database error saving purchase reminder settings for automated message {Id}", automatedMessageId);
                return ApiResponse<AutomatedMessageResponseDto>.InternalServerError("خطا در ذخیره‌سازی تنظیمات. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Unexpected error saving purchase reminder settings for automated message {Id}", automatedMessageId);
                return ApiResponse<AutomatedMessageResponseDto>.InternalServerError("خطای غیرمنتظره در ذخیره‌سازی تنظیمات");
            }
        }

        public async Task<ApiResponse<SpecialOccasionManagementResponseDto>> ManageSpecialOccasionsAsync(int automatedMessageId, int userId, SpecialOccasionManagementDto managementDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // بررسی وجود AutomatedMessage و مالکیت
                var automatedMessage = await _automatedMessageRepository.GetByIdAsync(automatedMessageId);
                if (automatedMessage == null || automatedMessage.UserId != userId)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<SpecialOccasionManagementResponseDto>.NotFound(
                        "پیام خودکار یافت نشد یا شما مجاز به دسترسی به این پیام خودکار نیستید");
                }

                // بررسی نوع اتوماسیون
                if (automatedMessage.AutomationType != "SpecialOccasion")
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<SpecialOccasionManagementResponseDto>.BadRequest(
                        "این تنظیمات فقط برای نوع پیام خودکار 'مناسبت‌های خاص' قابل استفاده است");
                }

                if (managementDto.Action == "Add")
                {
                    // افزودن مناسبت جدید
                    if (string.IsNullOrWhiteSpace(managementDto.OccasionName) || !managementDto.OccasionDate.HasValue)
                    {
                        await transaction.RollbackAsync();
                        return ApiResponse<SpecialOccasionManagementResponseDto>.BadRequest(
                            "نام مناسبت و تاریخ مناسبت الزامی است");
                    }

                    var specialOccasion = new SpecialOccasion
                    {
                        UserId = userId,
                        Name = managementDto.OccasionName.Trim(),
                        // تبدیل تاریخ مناسبت به UTC
                        OccasionDate = managementDto.OccasionDate.Value.EnsureUtc(),
                        Type = "Custom",
                        IsSystem = false,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _specialOccasionRepository.AddAsync(specialOccasion);
                    await _context.SaveChangesAsync();

                    // لینک کردن به AutomatedMessage
                    automatedMessage.SpecialOccasionId = specialOccasion.Id;
                    automatedMessage.UpdatedAt = DateTime.UtcNow;

                    await _automatedMessageRepository.UpdateAsync(automatedMessage);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Special occasion added for automated message {Id} by user {UserId} - OccasionId: {OccasionId}",
                        automatedMessageId, userId, specialOccasion.Id);
                }
                else if (managementDto.Action == "Remove")
                {
                    // حذف مناسبت (Soft Delete)
                    if (!managementDto.OccasionId.HasValue)
                    {
                        await transaction.RollbackAsync();
                        return ApiResponse<SpecialOccasionManagementResponseDto>.BadRequest(
                            "شناسه مناسبت برای حذف الزامی است");
                    }

                    var occasion = await _specialOccasionRepository.GetByIdAsync(managementDto.OccasionId.Value);
                    if (occasion == null || occasion.UserId != userId)
                    {
                        await transaction.RollbackAsync();
                        return ApiResponse<SpecialOccasionManagementResponseDto>.NotFound(
                            "مناسبت یافت نشد یا شما مجاز به حذف این مناسبت نیستید");
                    }

                    // Soft Delete
                    occasion.IsDeleted = true;
                    occasion.UpdatedAt = DateTime.UtcNow;

                    await _specialOccasionRepository.UpdateAsync(occasion);
                    await _context.SaveChangesAsync();

                    // اگر این مناسبت به AutomatedMessage لینک شده بود، لینک را حذف می‌کنیم
                    if (automatedMessage.SpecialOccasionId == managementDto.OccasionId.Value)
                    {
                        automatedMessage.SpecialOccasionId = null;
                        automatedMessage.UpdatedAt = DateTime.UtcNow;
                        await _automatedMessageRepository.UpdateAsync(automatedMessage);
                        await _context.SaveChangesAsync();
                    }

                    _logger.LogInformation("Special occasion removed for automated message {Id} by user {UserId} - OccasionId: {OccasionId}",
                        automatedMessageId, userId, managementDto.OccasionId.Value);
                }
                else
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<SpecialOccasionManagementResponseDto>.BadRequest(
                        "نوع عملیات نامعتبر است. باید Add یا Remove باشد");
                }

                await transaction.CommitAsync();

                // دریافت لیست مناسبت‌های کاربر
                var userOccasions = await _specialOccasionRepository.GetActiveByUserIdAsync(userId);
                var today = DateTime.UtcNow.Date;
                var occasionsList = userOccasions
                    .Where(o => !o.IsDeleted)
                    .Select(o => new SpecialOccasionItemDto
                    {
                        Id = o.Id,
                        Name = o.Name,
                        Date = o.OccasionDate,
                        DaysRemaining = CalculateDaysRemaining(o.OccasionDate, today)
                    })
                    .OrderBy(o => o.DaysRemaining)
                    .ToList();

                var response = new SpecialOccasionManagementResponseDto
                {
                    AutomatedMessageId = automatedMessageId,
                    Occasions = occasionsList
                };

                return ApiResponse<SpecialOccasionManagementResponseDto>.CreateSuccess(response,
                    managementDto.Action == "Add" ? "مناسبت با موفقیت افزوده شد" : "مناسبت با موفقیت حذف شد");
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Database error managing special occasions for automated message {Id}", automatedMessageId);
                return ApiResponse<SpecialOccasionManagementResponseDto>.InternalServerError("خطا در مدیریت مناسبت‌ها. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Unexpected error managing special occasions for automated message {Id}", automatedMessageId);
                return ApiResponse<SpecialOccasionManagementResponseDto>.InternalServerError("خطایی غیرمنتظره در مدیریت مناسبت‌ها");
            }
        }

        public async Task<ApiResponse<AutomatedMessageResponseDto>> SaveCustomAutomationSettingsAsync(int automatedMessageId, int userId, CustomAutomationSettingsDto settingsDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // بررسی وجود AutomatedMessage و مالکیت
                var automatedMessage = await _automatedMessageRepository.GetByIdAsync(automatedMessageId);
                if (automatedMessage == null || automatedMessage.UserId != userId)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageResponseDto>.NotFound(
                        "پیام خودکار یافت نشد یا شما مجاز به دسترسی به این پیام خودکار نیستید");
                }

                // بررسی نوع اتوماسیون
                if (automatedMessage.AutomationType != "Custom")
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageResponseDto>.BadRequest(
                        "این تنظیمات فقط برای نوع پیام خودکار 'اتوماسیون سفارشی' قابل استفاده است");
                }

                // اعتبارسنجی JSON
                try
                {
                    System.Text.Json.JsonDocument.Parse(settingsDto.ActivationConditions);
                }
                catch
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageResponseDto>.BadRequest(
                        "فرمت JSON شرایط فعال‌سازی نامعتبر است");
                }

                // ذخیره ActivationConditions
                automatedMessage.ActivationConditions = settingsDto.ActivationConditions;
                automatedMessage.UpdatedAt = DateTime.UtcNow;

                await _automatedMessageRepository.UpdateAsync(automatedMessage);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Custom automation settings saved for automated message {Id} by user {UserId}",
                    automatedMessageId, userId);

                return ApiResponse<AutomatedMessageResponseDto>.CreateSuccess(
                    MapToAutomatedMessageResponseDto(automatedMessage),
                    "تنظیمات با موفقیت ذخیره شد");
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Database error saving custom automation settings for automated message {Id}", automatedMessageId);
                return ApiResponse<AutomatedMessageResponseDto>.InternalServerError("خطا در ذخیره‌سازی تنظیمات. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Unexpected error saving custom automation settings for automated message {Id}", automatedMessageId);
                return ApiResponse<AutomatedMessageResponseDto>.InternalServerError("خطای غیرمنتظره در ذخیره‌سازی تنظیمات");
            }
        }

        /// <summary>
        /// ذخیره تنظیمات یکپارچه برای همه انواع پیام خودکار
        /// این متد جایگزین همه endpointهای جداگانه تنظیمات است
        /// </summary>
        public async Task<ApiResponse<object>> SaveUnifiedSettingsAsync(int automatedMessageId, int userId, UnifiedSettingsDto unifiedDto)
        {
            // اعتبارسنجی اولیه
            if (unifiedDto == null || string.IsNullOrWhiteSpace(unifiedDto.Type))
            {
                return ApiResponse<object>.BadRequest("نوع تنظیمات الزامی است");
            }

            // هدایت به متد مناسبت بر اساس نوع
            switch (unifiedDto.Type.ToLower())
            {
                case "birthday":
                    return await HandleBirthdaySettingsAsync(automatedMessageId, userId, unifiedDto.BirthdaySettings);

                case "cashbackexpiry":
                    return await HandleCashbackExpirySettingsAsync(automatedMessageId, userId, unifiedDto.CashbackExpirySettings);

                case "welcome":
                    return await HandleWelcomeSettingsAsync(automatedMessageId, userId);

                case "purchasereminder":
                    return await HandlePurchaseReminderSettingsAsync(automatedMessageId, userId, unifiedDto.PurchaseReminderSettings);

                case "specialoccasion":
                    return await HandleSpecialOccasionSettingsAsync(automatedMessageId, userId, unifiedDto.SpecialOccasionSettings);

                case "custom":
                    return await HandleCustomAutomationSettingsAsync(automatedMessageId, userId, unifiedDto.CustomAutomationSettings);

                default:
                    return ApiResponse<object>.BadRequest($"نوع تنظیمات نامعتبر است: {unifiedDto.Type}");
            }
        }

        /// <summary>
        /// پردازش تنظیمات تبریک تولد
        /// </summary>
        private async Task<ApiResponse<object>> HandleBirthdaySettingsAsync(int automatedMessageId, int userId, BirthdaySettingsData? data)
        {
            if (data == null)
            {
                return ApiResponse<object>.BadRequest("داده‌های تنظیمات تبریک تولد الزامی است");
            }

            var settingsDto = new BirthdaySettingsDto
            {
                SendTime = data.SendTime,
                RepeatYearly = data.RepeatYearly
            };

            var result = await SaveBirthdaySettingsAsync(automatedMessageId, userId, settingsDto);
            return new ApiResponse<object>
            {
                Success = result.Success,
                Message = result.Message,
                Data = result.Data,
                StatusCode = result.StatusCode
            };
        }

        /// <summary>
        /// پردازش تنظیمات یادآوری انقضای کش‌بک
        /// </summary>
        private async Task<ApiResponse<object>> HandleCashbackExpirySettingsAsync(int automatedMessageId, int userId, CashbackExpirySettingsData? data)
        {
            if (data == null)
            {
                return ApiResponse<object>.BadRequest("داده‌های تنظیمات یادآوری انقضای کش‌بک الزامی است");
            }

            var settingsDto = new CashbackExpirySettingsDto
            {
                DaysBeforeExpiry = data.DaysBeforeExpiry,
                ExecutionMode = data.ExecutionMode
            };

            var result = await SaveCashbackExpirySettingsAsync(automatedMessageId, userId, settingsDto);
            return new ApiResponse<object>
            {
                Success = result.Success,
                Message = result.Message,
                Data = result.Data,
                StatusCode = result.StatusCode
            };
        }

        /// <summary>
        /// پردازش تنظیمات پیام خوش‌آمدگویی
        /// </summary>
        private async Task<ApiResponse<object>> HandleWelcomeSettingsAsync(int automatedMessageId, int userId)
        {
            var result = await SaveWelcomeSettingsAsync(automatedMessageId, userId);
            return new ApiResponse<object>
            {
                Success = result.Success,
                Message = result.Message,
                Data = result.Data,
                StatusCode = result.StatusCode
            };
        }

        /// <summary>
        /// پردازش تنظیمات یادآوری خرید
        /// </summary>
        private async Task<ApiResponse<object>> HandlePurchaseReminderSettingsAsync(int automatedMessageId, int userId, PurchaseReminderSettingsData? data)
        {
            if (data == null)
            {
                return ApiResponse<object>.BadRequest("داده‌های تنظیمات یادآوری خرید الزامی است");
            }

            var settingsDto = new PurchaseReminderSettingsDto
            {
                DaysWithoutPurchase = data.DaysWithoutPurchase
            };

            var result = await SavePurchaseReminderSettingsAsync(automatedMessageId, userId, settingsDto);
            return new ApiResponse<object>
            {
                Success = result.Success,
                Message = result.Message,
                Data = result.Data,
                StatusCode = result.StatusCode
            };
        }

        /// <summary>
        /// پردازش تنظیمات مناسبت‌های خاص
        /// </summary>
        private async Task<ApiResponse<object>> HandleSpecialOccasionSettingsAsync(int automatedMessageId, int userId, SpecialOccasionSettingsData? data)
        {
            if (data == null)
            {
                return ApiResponse<object>.BadRequest("داده‌های تنظیمات مناسبت‌های خاص الزامی است");
            }

            var managementDto = new SpecialOccasionManagementDto
            {
                Action = data.Action,
                OccasionName = data.OccasionName,
                OccasionDate = data.OccasionDate,
                OccasionId = data.OccasionId
            };

            var result = await ManageSpecialOccasionsAsync(automatedMessageId, userId, managementDto);
            return new ApiResponse<object>
            {
                Success = result.Success,
                Message = result.Message,
                Data = result.Data,
                StatusCode = result.StatusCode
            };
        }

        /// <summary>
        /// پردازش تنظیمات اتوماسیون سفارشی
        /// </summary>
        private async Task<ApiResponse<object>> HandleCustomAutomationSettingsAsync(int automatedMessageId, int userId, CustomAutomationSettingsData? data)
        {
            if (data == null)
            {
                return ApiResponse<object>.BadRequest("داده‌های تنظیمات اتوماسیون سفارشی الزامی است");
            }

            // اعتبارسنجی JSON
            try
            {
                System.Text.Json.JsonDocument.Parse(data.ActivationConditions);
            }
            catch
            {
                return ApiResponse<object>.BadRequest("فرمت JSON شرایط فعال‌سازی نامعتبر است");
            }

            var settingsDto = new CustomAutomationSettingsDto
            {
                ActivationConditions = data.ActivationConditions
            };

            var result = await SaveCustomAutomationSettingsAsync(automatedMessageId, userId, settingsDto);
            return new ApiResponse<object>
            {
                Success = result.Success,
                Message = result.Message,
                Data = result.Data,
                StatusCode = result.StatusCode
            };
        }

        #endregion

        #region ساخت پیام (مرحله 4)

        public async Task<ApiResponse<MessageContentResponseDto>> SaveMessageContentAsync(int automatedMessageId, int userId, SaveMessageContentDto contentDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // بررسی وجود AutomatedMessage و مالکیت
                var automatedMessage = await _automatedMessageRepository.GetByIdWithMessageAsync(automatedMessageId);
                if (automatedMessage == null || automatedMessage.UserId != userId)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<MessageContentResponseDto>.NotFound(
                        "پیام خودکار یافت نشد یا شما مجاز به دسترسی به این پیام خودکار نیستید");
                }

                // بررسی وجود Message
                if (automatedMessage.MessageId == null)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<MessageContentResponseDto>.BadRequest(
                        "پیام مربوطه به این پیام خودکار یافت نشد");
                }

                var message = await _messageRepository.GetByIdAsync(automatedMessage.MessageId.Value);
                if (message == null || message.UserId != userId)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<MessageContentResponseDto>.BadRequest(
                        "پیام یافت نشد یا شما مجاز به دسترسی به این پیام نیستید");
                }

                // به‌روزرسانی محتوا
                message.Content = contentDto.Content.Trim();
                message.CharacterCount = SmsPartsCalculator.CountMessageCharacters(message.Content);
                message.PartsCount = SmsPartsCalculator.CalculateParts(message.Content);
                message.IsPersonalized = ContainsPlaceholders(message.Content);
                message.Placeholders = ExtractPlaceholders(message.Content);
                message.UpdatedAt = DateTime.UtcNow;

                await _messageRepository.UpdateAsync(message);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Message content saved for automated message {Id} by user {UserId} - MessageId: {MessageId}",
                    automatedMessageId, userId, message.Id);

                var response = new MessageContentResponseDto
                {
                    AutomatedMessageId = automatedMessageId,
                    MessageId = message.Id,
                    Content = message.Content,
                    CharacterCount = message.CharacterCount,
                    PartsCount = message.PartsCount,
                    IsPersonalized = message.IsPersonalized,
                    Placeholders = message.Placeholders
                };

                return ApiResponse<MessageContentResponseDto>.CreateSuccess(response, "محتوی پیام با موفقیت ذخیره شد");
            }
            catch (ArgumentException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(ex, "Invalid message content for automated message {Id}", automatedMessageId);
                return ApiResponse<MessageContentResponseDto>.BadRequest(ControlledErrorHelper.SanitizeArgumentMessage(ex.Message, ControlledErrorHelper.InvalidInput));
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Database error saving message content for automated message {Id}", automatedMessageId);
                return ApiResponse<MessageContentResponseDto>.InternalServerError("خطا در ذخیره‌سازی محتوی پیام. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Unexpected error saving message content for automated message {Id}", automatedMessageId);
                return ApiResponse<MessageContentResponseDto>.InternalServerError("خطایی غیرمنتظره در ذخیره‌سازی محتوی پیام");
            }
        }

        // متد CalculateMessageParts حذف شد - از SmsPartsCalculator.CalculateParts استفاده می‌شود

        /// <summary>
        /// بررسی وجود placeholder ها در محتوا
        /// </summary>
        private bool ContainsPlaceholders(string content)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(content, @"\(\(.+?\)\)|{{.+?}}");
        }

        /// <summary>
        /// استخراج placeholder ها از محتوا
        /// </summary>
        private string? ExtractPlaceholders(string content)
        {
            var placeholders = new List<string>();

            // استخراج placeholder های فرمت ((نام))
            var matches1 = System.Text.RegularExpressions.Regex.Matches(content, @"\(\((.+?)\)\)");
            foreach (System.Text.RegularExpressions.Match match in matches1)
            {
                placeholders.Add(match.Groups[1].Value);
            }

            // استخراج placeholder های فرمت {{نام}}
            var matches2 = System.Text.RegularExpressions.Regex.Matches(content, @"{{(.+?)}}");
            foreach (System.Text.RegularExpressions.Match match in matches2)
            {
                placeholders.Add(match.Groups[1].Value);
            }

            if (placeholders.Any())
            {
                return JsonSerializer.Serialize(placeholders.Distinct().ToList());
            }

            return null;
        }

        #endregion

        #region خلاصه و تنظیمات (مرحله 5)

        /// <summary>
        /// دریافت خلاصه پیام خودکار از Session (بدون تغییر)
        /// </summary>
        public async Task<ApiResponse<AutomatedMessageSummaryResponseDto>> GetAutomatedMessageSummaryAsync(int automatedMessageId, int userId)
        {
            try
            {
                // بررسی وجود AutomatedMessage و مالکیت
                var automatedMessage = await _automatedMessageRepository.GetByIdWithMessageAsync(automatedMessageId);
                if (automatedMessage == null || automatedMessage.UserId != userId)
                {
                    return ApiResponse<AutomatedMessageSummaryResponseDto>.NotFound(
                        "پیام خودکار یافت نشد یا شما مجاز به دسترسی به این پیام خودکار نیستید");
                }

                // بررسی وجود Message
                if (automatedMessage.MessageId == null)
                {
                    return ApiResponse<AutomatedMessageSummaryResponseDto>.BadRequest(
                        "پیام مربوطه به این پیام خودکار یافت نشد");
                }

                var message = await _messageRepository.GetByIdAsync(automatedMessage.MessageId.Value);
                if (message == null || message.UserId != userId)
                {
                    return ApiResponse<AutomatedMessageSummaryResponseDto>.BadRequest(
                        "پیام یافت نشد یا شما مجاز به دسترسی به این پیام نیستید");
                }

                // خواندن Session و تنظیمات (بدون Lock - فقط خواندن)
                var session = await _sessionRepository.GetActiveSessionByMessageIdAsync(message.Id, userId);
                if (session == null || string.IsNullOrEmpty(session.RecipientsJson))
                {
                    return ApiResponse<AutomatedMessageSummaryResponseDto>.BadRequest(
                        "هیچ گیرنده‌ای برای این پیام انتخاب نشده است. لطفاً ابتدا گیرندگان را انتخاب کنید.");
                }

                // خواندن گیرندگان از Session
                List<RecipientItemForAutomatedMessageDto> recipients;
                try
                {
                    recipients = JsonSerializer.Deserialize<List<RecipientItemForAutomatedMessageDto>>(session.RecipientsJson)
                        ?? new List<RecipientItemForAutomatedMessageDto>();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deserializing recipients from Session - SessionId: {SessionId}", session.Id);
                    return ApiResponse<AutomatedMessageSummaryResponseDto>.InternalServerError("خطا در خواندن لیست گیرندگان");
                }

                // خواندن تنظیمات از Session
                bool preventDuplicate = false;
                int duplicatePreventionHours = 24;
                bool sendToSpecificTags = false;
                List<int>? selectedTagIds = null;

                try
                {
                    var selectionCriteria = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(session.SelectionCriteria ?? "{}")
                        ?? new Dictionary<string, JsonElement>();

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
                    _logger.LogWarning(ex, "Error reading settings from Session - using defaults - AutomatedMessageId: {Id}", automatedMessageId);
                }

                // محاسبه تعداد گیرندگان
                var recipientsCount = recipients.Count;
                var eligibleRecipientsCount = recipients.Count(r => r.IsEligible);
                var ineligibleRecipientsCount = recipients.Count(r => !r.IsEligible);

                // محاسبه هزینه بر اساس eligibleRecipientsCount
                var partsCount = message.PartsCount;
                var estimatedTotalCost = eligibleRecipientsCount * partsCount * _costPerPart;

                // بررسی موجودی کیف پول
                var user = await _userRepository.GetByIdAsync(userId);
                var disableWalletCheck = IsWalletCheckDisabled();
                var walletStatus = disableWalletCheck || (user?.WalletBalance >= estimatedTotalCost) ? "Sufficient" : "Insufficient";

                // ایجاد ExecutionTime بر اساس نوع پیام خودکار
                var executionTime = GetExecutionTimeDescription(automatedMessage);

                // ایجاد EligibilityInfo
                var eligibilityInfo = CreateEligibilityInfo(
                    automatedMessage.AutomationType,
                    recipientsCount,
                    eligibleRecipientsCount,
                    ineligibleRecipientsCount);

                var response = new AutomatedMessageSummaryResponseDto
                {
                    AutomationType = automatedMessage.AutomationType,
                    ExecutionTime = executionTime,
                    RecipientsCount = recipientsCount,
                    EligibleRecipientsCount = eligibleRecipientsCount,
                    IneligibleRecipientsCount = ineligibleRecipientsCount,
                    EligibilityInfo = eligibilityInfo,
                    CostPerPart = _costPerPart,
                    EstimatedTotalCost = estimatedTotalCost,
                    WalletStatus = walletStatus,
                    WalletBalance = user?.WalletBalance ?? 0,
                    PreventDuplicate = preventDuplicate,
                    DuplicatePreventionHours = duplicatePreventionHours,
                    SendToSpecificTags = sendToSpecificTags,
                    SelectedTagIds = selectedTagIds
                };

                return ApiResponse<AutomatedMessageSummaryResponseDto>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting automated message summary for: {AutomatedMessageId}", automatedMessageId);
                return ApiResponse<AutomatedMessageSummaryResponseDto>.InternalServerError("خطا در دریافت خلاصه پیام خودکار");
            }
        }

        /// <summary>
        /// ذخیره تنظیمات تکمیلی پیام خودکار (فقط تنظیمات، بدون محاسبه مجدد)
        /// </summary>
        public async Task<ApiResponse<AutomatedMessageSummaryResponseDto>> SaveAutomatedMessageSettingsAsync(int automatedMessageId, int userId, SaveAutomatedMessageSettingsDto settingsDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // بررسی وجود AutomatedMessage و مالکیت
                var automatedMessage = await _automatedMessageRepository.GetByIdWithMessageAsync(automatedMessageId);
                if (automatedMessage == null || automatedMessage.UserId != userId)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageSummaryResponseDto>.NotFound(
                        "پیام خودکار یافت نشد یا شما مجاز به دسترسی به این پیام خودکار نیستید");
                }

                // بررسی وجود Message
                if (automatedMessage.MessageId == null)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageSummaryResponseDto>.BadRequest(
                        "پیام مربوطه به این پیام خودکار یافت نشد");
                }

                var message = await _messageRepository.GetByIdAsync(automatedMessage.MessageId.Value);
                if (message == null || message.UserId != userId)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageSummaryResponseDto>.BadRequest(
                        "پیام یافت نشد یا شما مجاز به دسترسی به این پیام نیستید");
                }

                // خواندن Session با Pessimistic Lock
                var session = await _context.MessageSessions
                    .FromSqlRaw(
                        "SELECT * FROM MessageSessions WITH (UPDLOCK, ROWLOCK) WHERE MessageId = {0} AND UserId = {1} AND IsDeleted = 0",
                        message.Id, userId)
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefaultAsync();

                if (session == null || string.IsNullOrEmpty(session.RecipientsJson))
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageSummaryResponseDto>.BadRequest(
                        "هیچ گیرنده‌ای برای این پیام انتخاب نشده است. لطفاً ابتدا گیرندگان را انتخاب کنید.");
                }

                // خواندن گیرندگان از Session
                List<RecipientItemForAutomatedMessageDto> recipients;
                try
                {
                    recipients = JsonSerializer.Deserialize<List<RecipientItemForAutomatedMessageDto>>(session.RecipientsJson)
                        ?? new List<RecipientItemForAutomatedMessageDto>();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageSummaryResponseDto>.BadRequest(
                        "خطا در خواندن لیست گیرندگان از Session");
                }

                // ذخیره تنظیمات در Session
                try
                {
                    var selectionCriteria = JsonSerializer.Deserialize<Dictionary<string, object>>(session.SelectionCriteria ?? "{}")
                        ?? new Dictionary<string, object>();

                    selectionCriteria["PreventDuplicate"] = settingsDto.PreventDuplicate;
                    selectionCriteria["DuplicatePreventionHours"] = settingsDto.DuplicatePreventionHours;
                    selectionCriteria["SendToSpecificTags"] = settingsDto.SendToSpecificTags;
                    
                    if (settingsDto.SelectedTagIds != null && settingsDto.SelectedTagIds.Any())
                    {
                        selectionCriteria["SelectedTagIds"] = JsonSerializer.Serialize(settingsDto.SelectedTagIds);
                    }
                    else if (settingsDto.SendToSpecificTags == false)
                    {
                        selectionCriteria.Remove("SelectedTagIds");
                    }

                    session.SelectionCriteria = JsonSerializer.Serialize(selectionCriteria);
                    session.UpdatedAt = DateTime.UtcNow;
                    await _sessionRepository.UpdateAsync(session);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error saving settings to Session - AutomatedMessageId: {Id}", automatedMessageId);
                    return ApiResponse<AutomatedMessageSummaryResponseDto>.InternalServerError("خطا در ذخیره تنظیمات");
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // محاسبه تعداد گیرندگان
                var recipientsCount = recipients.Count;
                var eligibleRecipientsCount = recipients.Count(r => r.IsEligible);
                var ineligibleRecipientsCount = recipients.Count(r => !r.IsEligible);

                // محاسبه هزینه بر اساس eligibleRecipientsCount
                var partsCount = message.PartsCount;
                var estimatedTotalCost = eligibleRecipientsCount * partsCount * _costPerPart;

                // بررسی موجودی کیف پول
                var user = await _userRepository.GetByIdAsync(userId);
                var disableWalletCheck = IsWalletCheckDisabled();
                var walletStatus = disableWalletCheck || (user?.WalletBalance >= estimatedTotalCost) ? "Sufficient" : "Insufficient";

                // ایجاد ExecutionTime بر اساس نوع پیام خودکار
                var executionTime = GetExecutionTimeDescription(automatedMessage);

                // ایجاد EligibilityInfo
                var eligibilityInfo = CreateEligibilityInfo(
                    automatedMessage.AutomationType,
                    recipientsCount,
                    eligibleRecipientsCount,
                    ineligibleRecipientsCount);

                var response = new AutomatedMessageSummaryResponseDto
                {
                    AutomationType = automatedMessage.AutomationType,
                    ExecutionTime = executionTime,
                    RecipientsCount = recipientsCount,
                    EligibleRecipientsCount = eligibleRecipientsCount,
                    IneligibleRecipientsCount = ineligibleRecipientsCount,
                    EligibilityInfo = eligibilityInfo,
                    CostPerPart = _costPerPart,
                    EstimatedTotalCost = estimatedTotalCost,
                    WalletStatus = walletStatus,
                    WalletBalance = user?.WalletBalance ?? 0,
                    PreventDuplicate = settingsDto.PreventDuplicate,
                    DuplicatePreventionHours = settingsDto.DuplicatePreventionHours,
                    SendToSpecificTags = settingsDto.SendToSpecificTags,
                    SelectedTagIds = settingsDto.SelectedTagIds
                };

                _logger.LogInformation("Automated message settings saved - AutomatedMessageId: {Id}, UserId: {UserId}",
                    automatedMessageId, userId);

                return ApiResponse<AutomatedMessageSummaryResponseDto>.CreateSuccess(response);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(ex, "Concurrency conflict while saving settings for automated message {Id}", automatedMessageId);
                return ApiResponse<AutomatedMessageSummaryResponseDto>.BadRequest(
                    "این Session در حال استفاده توسط درخواست دیگری است. لطفاً دوباره تلاش کنید.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error saving settings for automated message {Id}", automatedMessageId);
                return ApiResponse<AutomatedMessageSummaryResponseDto>.InternalServerError("خطا در ذخیره تنظیمات");
            }
        }

        public async Task<ApiResponse<AutomatedMessageSummaryResponseDto>> CalculateAutomatedMessageSummaryAsync(int automatedMessageId, int userId, CalculateAutomatedMessageSummaryDto summaryDto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // بررسی وجود AutomatedMessage و مالکیت
                var automatedMessage = await _automatedMessageRepository.GetByIdWithMessageAsync(automatedMessageId);
                if (automatedMessage == null || automatedMessage.UserId != userId)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageSummaryResponseDto>.NotFound(
                        "پیام خودکار یافت نشد یا شما مجاز به دسترسی به این پیام خودکار نیستید");
                }

                // بررسی وجود Message
                if (automatedMessage.MessageId == null)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageSummaryResponseDto>.BadRequest(
                        "پیام مربوطه به این پیام خودکار یافت نشد");
                }

                var message = await _messageRepository.GetByIdAsync(automatedMessage.MessageId.Value);
                if (message == null || message.UserId != userId)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageSummaryResponseDto>.BadRequest(
                        "پیام یافت نشد یا شما مجاز به دسترسی به این پیام نیستید");
                }

                // خواندن Session با Pessimistic Lock
                var session = await _context.MessageSessions
                    .FromSqlRaw(
                        "SELECT * FROM MessageSessions WITH (UPDLOCK, ROWLOCK) WHERE MessageId = {0} AND UserId = {1} AND IsDeleted = 0",
                        message.Id, userId)
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefaultAsync();

                if (session == null || string.IsNullOrEmpty(session.RecipientsJson))
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageSummaryResponseDto>.BadRequest(
                        "هیچ گیرنده‌ای برای این پیام انتخاب نشده است. لطفاً ابتدا گیرندگان را انتخاب کنید.");
                }

                // خواندن گیرندگان از Session
                List<RecipientItemForAutomatedMessageDto> recipients;
                try
                {
                    recipients = JsonSerializer.Deserialize<List<RecipientItemForAutomatedMessageDto>>(session.RecipientsJson)
                        ?? new List<RecipientItemForAutomatedMessageDto>();

                    // بررسی اینکه آیا Recipients شامل IsEligible هستند یا نه (برای سازگاری با داده‌های قدیمی)
                    if (recipients.Any() && recipients.All(r => r.IsEligible == false))
                    {
                        // اگر همه IsEligible = false هستند، احتمالاً داده قدیمی است، دوباره فیلتر کن
                        _logger.LogInformation("Recipients in session have no eligible recipients, re-filtering for automation {AutomationId}", automatedMessageId);

                        // دریافت مخاطبان از Recipients موجود
                        var contactIds = recipients
                            .Where(r => r.ContactId.HasValue)
                            .Select(r => r.ContactId!.Value)
                            .ToList();

                        if (contactIds.Any())
                        {
                            var contacts = await _context.Contacts
                                .Include(c => c.AdditionalInfo)
                                .Where(c => contactIds.Contains(c.Id) && !c.IsDeleted)
                                .ToListAsync();

                            // دوباره فیلتر کن
                            var filteredRecipients = await FilterContactsByAutomationTypeAsync(contacts, automatedMessage.AutomationType, userId);
                            recipients = filteredRecipients;

                            // بروزرسانی Session با داده‌های جدید
                            var updatedRecipientsJson = JsonSerializer.Serialize(recipients);
                            session.RecipientsJson = updatedRecipientsJson;
                            await _context.SaveChangesAsync();
                        }
                    }
                }
                catch
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageSummaryResponseDto>.BadRequest(
                        "خطا در خواندن لیست گیرندگان از Session");
                }

                // محاسبه تعداد گیرندگان
                var recipientsCount = recipients.Count;
                var eligibleRecipientsCount = recipients.Count(r => r.IsEligible);
                var ineligibleRecipientsCount = recipients.Count(r => !r.IsEligible);

                // محاسبه هزینه بر اساس eligibleRecipientsCount
                var partsCount = message.PartsCount;
                var estimatedTotalCost = eligibleRecipientsCount * partsCount * _costPerPart;

                // بررسی موجودی کیف پول
                var user = await _userRepository.GetByIdAsync(userId);
                var disableWalletCheck = IsWalletCheckDisabled();
                var walletStatus = disableWalletCheck || (user?.WalletBalance >= estimatedTotalCost) ? "Sufficient" : "Insufficient";

                // ایجاد ExecutionTime بر اساس نوع پیام خودکار
                var executionTime = GetExecutionTimeDescription(automatedMessage);

                // ایجاد EligibilityInfo
                var eligibilityInfo = CreateEligibilityInfo(
                    automatedMessage.AutomationType,
                    recipientsCount,
                    eligibleRecipientsCount,
                    ineligibleRecipientsCount);

                // ذخیره تنظیمات در Session
                try
                {
                    var selectionCriteria = JsonSerializer.Deserialize<Dictionary<string, object>>(session.SelectionCriteria ?? "{}")
                        ?? new Dictionary<string, object>();

                    selectionCriteria["PreventDuplicate"] = summaryDto.PreventDuplicate;
                    selectionCriteria["DuplicatePreventionHours"] = summaryDto.DuplicatePreventionHours;
                    selectionCriteria["SendToSpecificTags"] = summaryDto.SendToSpecificTags;
                    
                    if (summaryDto.SelectedTagIds != null && summaryDto.SelectedTagIds.Any())
                    {
                        selectionCriteria["SelectedTagIds"] = JsonSerializer.Serialize(summaryDto.SelectedTagIds);
                    }
                    else if (summaryDto.SendToSpecificTags == false)
                    {
                        selectionCriteria.Remove("SelectedTagIds");
                    }

                    session.SelectionCriteria = JsonSerializer.Serialize(selectionCriteria);
                    session.UpdatedAt = DateTime.UtcNow;
                    await _sessionRepository.UpdateAsync(session);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error saving summary settings to Session - continuing anyway");
                }

                // بررسی IsUsed
                if (session.IsUsed)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageSummaryResponseDto>.BadRequest(
                        "این Session قبلاً استفاده شده است. لطفاً گیرندگان را دوباره انتخاب کنید.");
                }

                // فقط گیرندگان واجد شرایط را در نظر می‌گیریم
                var eligibleRecipients = recipients.Where(r => r.IsEligible).ToList();

                if (!eligibleRecipients.Any())
                {
                    await transaction.RollbackAsync();

                    // پیام خطای سفارشی بر اساس نوع اتوماسیون
                    string errorMessage = GetEligibilityErrorMessage(automatedMessage.AutomationType, recipientsCount, eligibleRecipientsCount);
                    return ApiResponse<AutomatedMessageSummaryResponseDto>.BadRequest(errorMessage);
                }

                // اگر برخی واجد شرایط هستند و برخی نیستند، پیام هشدار اضافه کنیم
                if (eligibleRecipientsCount < recipientsCount)
                {
                    // پیام هشدار بر اساس نوع اتوماسیون
                    string warningMessage = GetPartialEligibilityWarningMessage(automatedMessage.AutomationType, recipientsCount, eligibleRecipientsCount);
                    _logger.LogWarning(warningMessage);
                }

                // بررسی موجودی کیف پول (دوباره با تعداد دقیق)
                // غیرفعال شده - دیگر کیف پول چک نمی‌شود
                /*
                if (!disableWalletCheck && (user == null || user.WalletBalance < estimatedTotalCost))
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<AutomatedMessageSummaryResponseDto>.BadRequest(
                        $"موجودی کیف پول کافی نیست. موجودی: {user?.WalletBalance ?? 0} تومان، هزینه تخمینی: {estimatedTotalCost} تومان");
                }
                */

                // ایجاد MessageCampaign
                // Title بر اساس AutomationType تنظیم می‌شود
                var campaignTitle = !string.IsNullOrWhiteSpace(automatedMessage.Title) 
                    ? automatedMessage.Title 
                    : GetDefaultTitle(automatedMessage.AutomationType);

                var campaign = new MessageCampaign
                {
                    UserId = userId,
                    MessageId = message.Id,
                    AutomatedMessageId = automatedMessageId,
                    Title = campaignTitle,
                    SendType = "Automated",
                    Status = "Active",
                    RecipientsCount = eligibleRecipients.Count,
                    PartsCount = partsCount,
                    CostPerPart = _costPerPart,
                    EstimatedTotalCost = estimatedTotalCost,
                    CreatedAt = DateTime.UtcNow
                };

                await _campaignRepository.AddAsync(campaign);
                await _context.SaveChangesAsync();

                // ایجاد MessageRecipient برای هر گیرنده
                var messageRecipients = eligibleRecipients.Select(r => new MessageRecipient
                {
                    CampaignId = campaign.Id,
                    ContactId = r.ContactId,
                    MobileNumber = r.MobileNumber,
                    FullName = r.FullName,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                }).ToList();

                await _context.MessageRecipients.AddRangeAsync(messageRecipients);
                await _context.SaveChangesAsync();

                // به‌روزرسانی AutomatedMessage
                automatedMessage.IsActive = true;
                automatedMessage.Status = "Active";
                automatedMessage.UpdatedAt = DateTime.UtcNow;
                await _automatedMessageRepository.UpdateAsync(automatedMessage);

                // به‌روزرسانی Session
                session.IsUsed = true;
                session.UpdatedAt = DateTime.UtcNow;
                await _sessionRepository.UpdateAsync(session);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Automated message activated and campaign created - AutomatedMessageId: {Id}, CampaignId: {CampaignId}, RecipientsCount: {Count}",
                    automatedMessageId, campaign.Id, eligibleRecipients.Count);

                var response = new AutomatedMessageSummaryResponseDto
                {
                    AutomationType = automatedMessage.AutomationType,
                    ExecutionTime = executionTime,
                    RecipientsCount = recipientsCount,
                    EligibleRecipientsCount = eligibleRecipientsCount,
                    IneligibleRecipientsCount = ineligibleRecipientsCount,
                    EligibilityInfo = eligibilityInfo,
                    CostPerPart = _costPerPart,
                    EstimatedTotalCost = estimatedTotalCost,
                    WalletStatus = walletStatus,
                    WalletBalance = user?.WalletBalance ?? 0,
                    PreventDuplicate = summaryDto.PreventDuplicate,
                    DuplicatePreventionHours = summaryDto.DuplicatePreventionHours,
                    SendToSpecificTags = summaryDto.SendToSpecificTags,
                    SelectedTagIds = summaryDto.SelectedTagIds
                };

                _logger.LogInformation("Automated message summary calculated and send started - AutomatedMessageId: {Id}, RecipientsCount: {Count}, EstimatedCost: {Cost}",
                    automatedMessageId, recipientsCount, estimatedTotalCost);

                return ApiResponse<AutomatedMessageSummaryResponseDto>.CreateSuccess(response);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning(ex, "Concurrency conflict while calculating summary for automated message {Id}", automatedMessageId);
                return ApiResponse<AutomatedMessageSummaryResponseDto>.BadRequest(
                    "این Session در حال استفاده توسط درخواست دیگری است. لطفاً دوباره تلاش کنید.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error calculating summary for automated message {Id}", automatedMessageId);
                return ApiResponse<AutomatedMessageSummaryResponseDto>.InternalServerError("خطا در محاسبه خلاصه");
            }
        }

        /// <summary>
        /// ایجاد توضیحات زمان اجرا بر اساس نوع پیام خودکار
        /// </summary>
        private string GetExecutionTimeDescription(AutomatedMessage automatedMessage)
        {
            switch (automatedMessage.AutomationType)
            {
                case "Birthday":
                    try
                    {
                        var activationConditions = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(automatedMessage.ActivationConditions ?? "{}");
                        if (activationConditions != null && activationConditions.TryGetValue("sendTime", out var sendTimeElement))
                        {
                            var sendTime = sendTimeElement.GetString();
                            return $"{sendTime} روز تولد";
                        }
                    }
                    catch { }
                    return "روز تولد";

                case "CashbackExpiry":
                    var days = automatedMessage.DaysBeforeEvent ?? 2;
                    return $"{days} روز قبل از انقضای کش‌بک";

                case "Welcome":
                    return "بلافاصله پس از ثبت شماره";

                case "PurchaseReminder":
                    var daysWithoutPurchase = automatedMessage.DaysBeforeEvent ?? 30;
                    return $"پس از {daysWithoutPurchase} روز بدون خرید";

                case "SpecialOccasion":
                    if (automatedMessage.SpecialOccasionId.HasValue)
                    {
                        var occasion = _context.SpecialOccasions.FirstOrDefault(o => o.Id == automatedMessage.SpecialOccasionId.Value);
                        if (occasion != null)
                        {
                            return $"روز {occasion.Name} ({occasion.OccasionDate:MM/dd})";
                        }
                    }
                    return "روز مناسبت خاص";

                case "Custom":
                    return "بر اساس شرایط سفارشی";

                default:
                    return "نامشخص";
            }
        }

        #endregion
    }
}


