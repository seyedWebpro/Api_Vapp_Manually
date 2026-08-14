using Api_Vapp.Configuration;
using Api_Vapp.Constants;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Contact;
using Api_Vapp.DTOs.NumberSeeker;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api_Vapp.Services
{
    public class NumberSeekerService : INumberSeekerService
    {
        private static readonly HashSet<string> TerminalStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "completed", "partial", "failed", "cancelled"
        };

        private static readonly HashSet<string> ImportableStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "completed", "partial"
        };

        private static readonly List<NumberSeekerSourceInfoDto> KnownSources = new()
        {
            new() { Code = "divar", DisplayName = "دیوار", IconKey = "divar", SortOrder = 1, Enabled = true },
            new() { Code = "googlemaps", DisplayName = "گوگل مپ", IconKey = "googlemaps", SortOrder = 2, Enabled = true },
            new() { Code = "sheypoor", DisplayName = "شیپور", IconKey = "sheypoor", SortOrder = 3, Enabled = true },
            new() { Code = "nshan", DisplayName = "نشان", IconKey = "nshan", SortOrder = 4, Enabled = true },
            new() { Code = "balad", DisplayName = "بلد", IconKey = "balad", SortOrder = 5, Enabled = true }
        };

        private static readonly string[] KnownCities =
        {
            "تهران", "مشهد", "اصفهان", "شیراز", "تبریز", "کرج", "اهواز", "قم",
            "کرمانشاه", "رشت", "یزد", "کرمان", "همدان", "ارومیه", "زاهدان",
            "اردبیل", "بندرعباس", "زنجان", "سنندج", "قزوین", "ساری", "گرگان",
            "اراک", "بوشهر", "خرم‌آباد", "سمنان", "شهرکرد", "یاسوج", "ایلام", "بجنورد"
        };

        private static readonly string[] KnownCategories =
        {
            "رستوران", "کافه", "کافه رستوران", "فست‌فود", "شیرینی‌فروشی",
            "آرایشگاه", "سالن زیبایی", "پوشاک", "موبایل فروشی", "لوازم خانگی",
            "املاک", "خودرو", "کلینیک", "داروخانه", "سوپرمارکت",
            "میوه و تره‌بار", "نانوایی", "آموزشگاه", "باشگاه ورزشی", "هتل"
        };

        private readonly INumberScraperClient _scraperClient;
        private readonly INumberSeekerTaskRepository _taskRepository;
        private readonly IContactService _contactService;
        private readonly INumberSeekerRateLimiter _rateLimiter;
        private readonly INumberSeekerPhoneAccessService _phoneAccess;
        private readonly NumberSeekerOptions _options;
        private readonly IAuditService _audit;
        private readonly ILogger<NumberSeekerService> _logger;

        public NumberSeekerService(
            INumberScraperClient scraperClient,
            INumberSeekerTaskRepository taskRepository,
            IContactService contactService,
            INumberSeekerRateLimiter rateLimiter,
            INumberSeekerPhoneAccessService phoneAccess,
            IOptions<NumberSeekerOptions> options,
            IAuditService audit,
            ILogger<NumberSeekerService> logger)
        {
            _scraperClient = scraperClient;
            _taskRepository = taskRepository;
            _contactService = contactService;
            _rateLimiter = rateLimiter;
            _phoneAccess = phoneAccess;
            _options = options.Value;
            _audit = audit;
            _logger = logger;
        }

        public async Task<ApiResponse<NumberSeekerTaskCreatedDto>> StartScrapeAsync(
            int userId,
            StartNumberSeekerScrapeDto request)
        {
            if (!_scraperClient.IsEnabled)
            {
                return ApiResponse<NumberSeekerTaskCreatedDto>.Error(
                    NumberSeekerUserMessages.ServiceDisabled,
                    503,
                    errorCode: "SCRAPER_DISABLED");
            }

            var (allowed, retryAfter) = await _rateLimiter.CheckScrapeAsync(userId);
            if (!allowed)
            {
                return ApiResponse<NumberSeekerTaskCreatedDto>.Error(
                    NumberSeekerUserMessages.RateLimited,
                    429,
                    errorCode: "RATE_LIMITED");
            }

            try
            {
                var created = await _scraperClient.StartScrapeAsync(request);

                var ownedTask = new NumberSeekerTask
                {
                    UserId = userId,
                    ScraperTaskId = created.TaskId,
                    Source = created.Source,
                    City = request.City.Trim(),
                    Category = request.Category.Trim(),
                    TargetCount = request.MaxPhones,
                    Status = created.Status,
                    CurrentCount = 0,
                    Message = NumberSeekerUserMessages.SanitizeIncomingUserMessage(
                        created.Message,
                        "درخواست شما ثبت شد و در حال پردازش است."),
                    CreatedAt = DateTime.UtcNow
                };

                try
                {
                    await _taskRepository.AddAsync(ownedTask);
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "DB save failed after scraper task {TaskId} — attempting cancel", created.TaskId);
                    try
                    {
                        await _scraperClient.CancelTaskAsync(created.TaskId);
                    }
                    catch (Exception cancelEx)
                    {
                        _logger.LogWarning(cancelEx, "Failed to cancel orphan scraper task {TaskId}", created.TaskId);
                    }

                    throw;
                }

                await _rateLimiter.RecordScrapeAsync(userId);

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.NumberSeeker,
                    Action = AuditActions.NumberSeekerTaskCreated,
                    EntityType = AuditEntityTypes.NumberSeekerTask,
                    EntityId = ownedTask.ScraperTaskId,
                    ActorUserId = userId,
                    After = new { source = ownedTask.Source, city = ownedTask.City, category = ownedTask.Category, targetCount = ownedTask.TargetCount }
                });

                created.PollUrl = $"/api/NumberSeeker/task/{created.TaskId}";
                created.SourceDisplayName = NumberSeekerUiMapper.GetSourceDisplayName(created.Source);
                created.StatusDisplayName = NumberSeekerUiMapper.GetStatusDisplayName(created.Status);
                created.Message = ownedTask.Message;

                return ApiResponse<NumberSeekerTaskCreatedDto>.CreateSuccess(
                    created,
                    created.Message,
                    StatusCodes.Status201Created);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Scraper API key rejected for user {UserId}", userId);
                return ApiResponse<NumberSeekerTaskCreatedDto>.Error(
                    NumberSeekerUserMessages.ExtractionFailed,
                    503,
                    errorCode: "SCRAPER_AUTH_FAILED");
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid scrape input for user {UserId}", userId);
                return ApiResponse<NumberSeekerTaskCreatedDto>.BadRequest(
                    NumberSeekerUserMessages.InvalidInput,
                    errorCode: ErrorCodes.InvalidInput);
            }
            catch (InvalidOperationException ex) when (
                ex.Message.Contains("RATE_LIMITED", StringComparison.Ordinal) ||
                ex.Message.Contains("محدودیت", StringComparison.Ordinal))
            {
                return ApiResponse<NumberSeekerTaskCreatedDto>.Error(
                    NumberSeekerUserMessages.RateLimited,
                    429,
                    errorCode: "RATE_LIMITED");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("SCRAPER_DISABLED", StringComparison.Ordinal))
            {
                return ApiResponse<NumberSeekerTaskCreatedDto>.Error(
                    NumberSeekerUserMessages.ServiceDisabled,
                    503,
                    errorCode: "SCRAPER_DISABLED");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start number seeker scrape for user {UserId}", userId);
                return ApiResponse<NumberSeekerTaskCreatedDto>.Error(
                    NumberSeekerUserMessages.ExtractionFailed,
                    503,
                    errorCode: "SCRAPER_UNAVAILABLE");
            }
        }

        public async Task<ApiResponse<NumberSeekerTaskStatusDto>> GetTaskStatusAsync(
            int userId,
            string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId))
            {
                return ApiResponse<NumberSeekerTaskStatusDto>.BadRequest(
                    NumberSeekerUserMessages.TaskIdRequired,
                    errorCode: ErrorCodes.InvalidInput);
            }

            var ownedTask = await _taskRepository.GetByScraperTaskIdAndUserIdAsync(taskId.Trim(), userId);
            if (ownedTask == null)
            {
                return ApiResponse<NumberSeekerTaskStatusDto>.NotFound(NumberSeekerUserMessages.TaskNotFound);
            }

            // Performance: terminal + cached phones → بدون فراخوانی ربات
            var cachedPhones = NumberSeekerPhoneStorage.Deserialize(ownedTask.PhonesJson);
            if (TerminalStatuses.Contains(ownedTask.Status) && cachedPhones.Count > 0)
            {
                var cachedStatus = BuildStatusFromOwnedTask(ownedTask, cachedPhones);
                EnrichStatusForUi(cachedStatus, ownedTask.CreatedAt);
                await ApplyPhoneVisibilityAsync(userId, cachedStatus);
                return ApiResponse<NumberSeekerTaskStatusDto>.CreateSuccess(cachedStatus);
            }

            try
            {
                var status = await _scraperClient.GetTaskStatusAsync(taskId.Trim());
                await SyncOwnedTaskAsync(ownedTask, status);
                EnrichStatusForUi(status, ownedTask.CreatedAt);
                await ApplyPhoneVisibilityAsync(userId, status);
                return ApiResponse<NumberSeekerTaskStatusDto>.CreateSuccess(status);
            }
            catch (KeyNotFoundException)
            {
                if (cachedPhones.Count > 0)
                {
                    var cachedStatus = BuildStatusFromOwnedTask(ownedTask, cachedPhones);
                    EnrichStatusForUi(cachedStatus, ownedTask.CreatedAt);
                    await ApplyPhoneVisibilityAsync(userId, cachedStatus);
                    return ApiResponse<NumberSeekerTaskStatusDto>.CreateSuccess(cachedStatus);
                }

                return ApiResponse<NumberSeekerTaskStatusDto>.NotFound(NumberSeekerUserMessages.TaskNotFound);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get task status {TaskId} for user {UserId}", taskId, userId);

                if (cachedPhones.Count > 0)
                {
                    var cachedStatus = BuildStatusFromOwnedTask(ownedTask, cachedPhones);
                    EnrichStatusForUi(cachedStatus, ownedTask.CreatedAt);
                    await ApplyPhoneVisibilityAsync(userId, cachedStatus);
                    return ApiResponse<NumberSeekerTaskStatusDto>.CreateSuccess(cachedStatus);
                }

                return ApiResponse<NumberSeekerTaskStatusDto>.Error(
                    NumberSeekerUserMessages.ExtractionFailed,
                    503,
                    errorCode: "SCRAPER_UNAVAILABLE");
            }
        }

        public async Task<ApiResponse<NumberSeekerCancelResultDto>> CancelTaskAsync(
            int userId,
            string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId))
            {
                return ApiResponse<NumberSeekerCancelResultDto>.BadRequest(
                    NumberSeekerUserMessages.TaskIdRequired,
                    errorCode: ErrorCodes.InvalidInput);
            }

            var ownedTask = await _taskRepository.GetByScraperTaskIdAndUserIdAsync(taskId.Trim(), userId);
            if (ownedTask == null)
            {
                return ApiResponse<NumberSeekerCancelResultDto>.NotFound(NumberSeekerUserMessages.TaskNotFound);
            }

            // قبلاً لغو شده — پاسخ موفق یکسان (idempotent)
            if (string.Equals(ownedTask.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponse<NumberSeekerCancelResultDto>.CreateSuccess(
                    BuildCancelResult(ownedTask),
                    NumberSeekerUserMessages.Cancelled);
            }

            if (NumberSeekerUiMapper.IsTerminal(ownedTask.Status))
            {
                return ApiResponse<NumberSeekerCancelResultDto>.BadRequest(
                    NumberSeekerUserMessages.CancelNotAllowed,
                    errorCode: ErrorCodes.InvalidInput);
            }

            var scraperCancelOk = false;
            try
            {
                await _scraperClient.CancelTaskAsync(taskId.Trim());
                scraperCancelOk = true;
            }
            catch (KeyNotFoundException)
            {
                _logger.LogWarning(
                    "Scraper task {TaskId} not found during cancel — marking cancelled locally",
                    taskId);
            }
            catch (Exception ex)
            {
                // کاربر وسط کار لغو کرده — حتی اگر اسکرپر لحظه‌ای در دسترس نباشد، وضعیت محلی را لغو می‌کنیم
                _logger.LogWarning(
                    ex,
                    "Scraper cancel failed for task {TaskId}; cancelling locally",
                    taskId);
            }

            // سعی کن شماره‌های جزئی را قبل از بستن تسک ذخیره کنی
            try
            {
                var status = await _scraperClient.GetTaskStatusAsync(taskId.Trim());
                if (status.Phones is { Count: > 0 })
                    PersistPhones(ownedTask, status.Phones);
                if (status.CurrentCount > ownedTask.CurrentCount)
                    ownedTask.CurrentCount = status.CurrentCount;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Post-cancel status fetch skipped for task {TaskId}", taskId);
            }

            ownedTask.Status = "cancelled";
            ownedTask.CompletedAt = DateTime.UtcNow;
            ownedTask.UpdatedAt = DateTime.UtcNow;
            ownedTask.Message = NumberSeekerUserMessages.Cancelled;
            ownedTask.ResultCode = "cancelled";
            await _taskRepository.UpdateAsync(ownedTask);

            await _audit.WriteAsync(new AuditEntry
            {
                Category = AuditCategories.NumberSeeker,
                Action = AuditActions.NumberSeekerTaskCancelled,
                EntityType = AuditEntityTypes.NumberSeekerTask,
                EntityId = ownedTask.ScraperTaskId,
                ActorUserId = userId,
                Metadata = scraperCancelOk ? null : new { localOnly = true }
            });

            return ApiResponse<NumberSeekerCancelResultDto>.CreateSuccess(
                BuildCancelResult(ownedTask),
                NumberSeekerUserMessages.Cancelled);
        }

        public async Task<ApiResponse<NumberSeekerImportResultDto>> ImportPhonesAsync(
            int userId,
            string taskId,
            ImportNumberSeekerPhonesDto request)
        {
            if (string.IsNullOrWhiteSpace(taskId))
            {
                return ApiResponse<NumberSeekerImportResultDto>.BadRequest(
                    NumberSeekerUserMessages.TaskIdRequired,
                    errorCode: ErrorCodes.InvalidInput);
            }

            var (allowed, retryAfter) = await _rateLimiter.CheckImportAsync(userId);
            if (!allowed)
            {
                return ApiResponse<NumberSeekerImportResultDto>.Error(
                    NumberSeekerUserMessages.RateLimited,
                    429,
                    errorCode: "RATE_LIMITED");
            }

            var ownedTask = await _taskRepository.GetByScraperTaskIdAndUserIdAsync(taskId.Trim(), userId);
            if (ownedTask == null)
            {
                return ApiResponse<NumberSeekerImportResultDto>.NotFound(NumberSeekerUserMessages.TaskNotFound);
            }

            if (ownedTask.ImportedAt != null && !request.Force)
            {
                return ApiResponse<NumberSeekerImportResultDto>.Error(
                    NumberSeekerUserMessages.AlreadyImported,
                    409,
                    errorCode: "ALREADY_IMPORTED");
            }

            // Performance: اول از کش پایدار Vapp؛ در صورت نبود از ربات
            var phones = NumberSeekerPhoneStorage.Deserialize(ownedTask.PhonesJson);
            var taskStatus = ownedTask.Status;

            if (phones.Count == 0 || !ImportableStatuses.Contains(taskStatus))
            {
                try
                {
                    var status = await _scraperClient.GetTaskStatusAsync(taskId.Trim());
                    await SyncOwnedTaskAsync(ownedTask, status);
                    taskStatus = status.Status;
                    if (status.Phones is { Count: > 0 })
                    {
                        phones = status.Phones;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch phones for import task {TaskId}", taskId);
                    if (phones.Count == 0)
                    {
                        return ApiResponse<NumberSeekerImportResultDto>.Error(
                            NumberSeekerUserMessages.ExtractionFailed,
                            503,
                            errorCode: "SCRAPER_UNAVAILABLE");
                    }
                }
            }

            if (!ImportableStatuses.Contains(taskStatus))
            {
                return ApiResponse<NumberSeekerImportResultDto>.BadRequest(
                    NumberSeekerUserMessages.NotReadyForImport,
                    errorCode: ErrorCodes.InvalidInput);
            }

            if (phones.Count == 0)
            {
                return ApiResponse<NumberSeekerImportResultDto>.BadRequest(
                    NumberSeekerUserMessages.NoPhonesForAction,
                    errorCode: ErrorCodes.InvalidInput);
            }

            var namePrefix = string.IsNullOrWhiteSpace(request.ContactNamePrefix)
                ? _options.DefaultContactNamePrefix
                : request.ContactNamePrefix.Trim();

            var importDto = new ImportContactsFromListDto
            {
                ContactNotebookId = request.ContactNotebookId,
                Contacts = phones
                    .Select((phone, index) => new ImportContactItemDto
                    {
                        MobileNumber = phone,
                        Name = $"{namePrefix} {index + 1}",
                        HideMobileNumber = true
                    })
                    .ToList()
            };

            var importResult = await _contactService.ImportFromListAsync(userId, importDto);
            if (!importResult.Success)
            {
                // پیام سرویس مخاطبین اگر امن نبود، پیام عمومی کاربر-محور
                var safeMessage = NumberSeekerUserMessages.SanitizeIncomingUserMessage(
                    importResult.Message,
                    NumberSeekerUserMessages.NoPhonesForAction);
                return ApiResponse<NumberSeekerImportResultDto>.Error(
                    safeMessage,
                    importResult.StatusCode,
                    importResult.Errors,
                    importResult.ErrorCode ?? ErrorCodes.InvalidInput);
            }

            await _rateLimiter.RecordImportAsync(userId);

            ownedTask.ImportedAt = DateTime.UtcNow;
            ownedTask.ImportedCount = importResult.Data?.SuccessCount ?? 0;
            ownedTask.ImportedNotebookId = request.ContactNotebookId;
            await _taskRepository.UpdateAsync(ownedTask);

            await _audit.WriteAsync(new AuditEntry
            {
                Category = AuditCategories.NumberSeeker,
                Action = AuditActions.NumberSeekerTaskImported,
                EntityType = AuditEntityTypes.NumberSeekerTask,
                EntityId = ownedTask.ScraperTaskId,
                ActorUserId = userId,
                After = new { importedCount = ownedTask.ImportedCount, notebookId = request.ContactNotebookId }
            });

            var canViewPhones = await _phoneAccess.CanViewPhonesAsync(userId);
            var data = importResult.Data!;
            var result = new NumberSeekerImportResultDto
            {
                TaskId = taskId.Trim(),
                ContactNotebookId = request.ContactNotebookId,
                TotalPhones = phones.Count,
                SuccessCount = data.SuccessCount,
                DuplicateCount = data.DuplicateCount,
                SkippedCount = data.SkippedCount,
                ErrorCount = data.ErrorCount,
                Errors = data.Errors.Select(e => new ImportRowErrorDto
                {
                    RowNumber = e.RowNumber,
                    MobileNumber = PhoneNumberMasker.ForClient(e.MobileNumber, hideMobileNumber: true, canViewPhones),
                    ErrorMessage = e.ErrorMessage
                }).ToList(),
                ImportedAt = ownedTask.ImportedAt.Value
            };

            return ApiResponse<NumberSeekerImportResultDto>.CreateSuccess(
                result,
                $"{data.SuccessCount} مخاطب با موفقیت import شد.");
        }

        public async Task<ApiResponse<bool>> HandleWebhookAsync(NumberSeekerWebhookDto webhook)
        {
            if (string.IsNullOrWhiteSpace(webhook.TaskId))
            {
                return ApiResponse<bool>.BadRequest(
                    NumberSeekerUserMessages.TaskIdRequired,
                    errorCode: ErrorCodes.InvalidInput);
            }

            var ownedTask = await _taskRepository.GetByScraperTaskIdTrackedAsync(webhook.TaskId.Trim());
            if (ownedTask == null)
            {
                _logger.LogWarning("Webhook for unknown task {TaskId} — ignored", webhook.TaskId);
                return ApiResponse<bool>.CreateSuccess(true, "تسک در Vapp ثبت نشده — نادیده گرفته شد.");
            }

            ownedTask.Status = webhook.Status;
            ownedTask.CurrentCount = webhook.CurrentCount > 0
                ? webhook.CurrentCount
                : (webhook.Phones?.Count ?? ownedTask.CurrentCount);
            ownedTask.ResultCode = webhook.ResultCode;
            ownedTask.Message = NumberSeekerUserMessages.ForTaskStatus(
                webhook.Status,
                webhook.ResultCode,
                ownedTask.CurrentCount);
            ownedTask.UpdatedAt = DateTime.UtcNow;

            if (webhook.Phones is { Count: > 0 })
            {
                PersistPhones(ownedTask, webhook.Phones);
                _logger.LogInformation(
                    "Webhook persisted {PhoneCount} phones for task {TaskId}",
                    webhook.Phones.Count,
                    webhook.TaskId);
            }

            if (TerminalStatuses.Contains(webhook.Status) && ownedTask.CompletedAt == null)
            {
                ownedTask.CompletedAt = DateTime.UtcNow;
            }

            await _taskRepository.UpdateAsync(ownedTask);

            if (TerminalStatuses.Contains(webhook.Status))
            {
                var action = string.Equals(webhook.Status, "failed", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(webhook.Status, "cancelled", StringComparison.OrdinalIgnoreCase)
                    ? (string.Equals(webhook.Status, "cancelled", StringComparison.OrdinalIgnoreCase)
                        ? AuditActions.NumberSeekerTaskCancelled
                        : AuditActions.NumberSeekerTaskFailed)
                    : AuditActions.NumberSeekerTaskCompleted;

                try
                {
                    await _audit.WriteAsync(new AuditEntry
                    {
                        Category = AuditCategories.NumberSeeker,
                        Action = action,
                        EntityType = AuditEntityTypes.NumberSeekerTask,
                        EntityId = ownedTask.ScraperTaskId,
                        ActorUserId = ownedTask.UserId,
                        After = new
                        {
                            status = ownedTask.Status,
                            currentCount = ownedTask.CurrentCount,
                            resultCode = ownedTask.ResultCode,
                            phonesPersisted = !string.IsNullOrEmpty(ownedTask.PhonesJson)
                        }
                    });
                }
                catch (Exception auditEx)
                {
                    _logger.LogWarning(auditEx, "Audit write failed for webhook task {TaskId}", webhook.TaskId);
                }
            }

            return ApiResponse<bool>.CreateSuccess(true, "وضعیت تسک به‌روزرسانی شد.");
        }

        public async Task<ApiResponse<NumberSeekerTaskListDto>> GetRecentTasksAsync(
            int userId,
            int limit = 20)
        {
            var tasks = await _taskRepository.GetRecentByUserIdAsync(userId, Math.Clamp(limit, 1, 100));

            var summaries = tasks.Select(MapSummary).ToList();

            return ApiResponse<NumberSeekerTaskListDto>.CreateSuccess(new NumberSeekerTaskListDto
            {
                Count = summaries.Count,
                Tasks = summaries
            });
        }

        public async Task<ApiResponse<NumberSeekerHealthDto>> GetHealthAsync()
        {
            var health = await _scraperClient.GetHealthAsync();

            if (!health.ScraperReachable)
            {
                _logger.LogWarning("NumberSeeker health: scraper unreachable — status={Status}", health.Status);
            }
            else if (!health.ApiKeyValid)
            {
                _logger.LogError("ALERT NumberSeeker API key mismatch between Vapp and scraper");
            }
            else if (health.TokenAlerts.Any(a =>
                string.Equals(a.Level, "critical", StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogError(
                    "ALERT NumberSeeker critical token alerts: {Count}",
                    health.TokenAlertsCount);
            }

            return ApiResponse<NumberSeekerHealthDto>.CreateSuccess(health);
        }

        public ApiResponse<NumberSeekerSourcesDto> GetSources()
        {
            return ApiResponse<NumberSeekerSourcesDto>.CreateSuccess(new NumberSeekerSourcesDto
            {
                Sources = KnownSources.OrderBy(s => s.SortOrder).ToList()
            });
        }

        public ApiResponse<NumberSeekerCitiesDto> GetCities()
        {
            var cities = KnownCities
                .Select((name, index) => new NumberSeekerCityDto { Name = name, SortOrder = index + 1 })
                .ToList();

            return ApiResponse<NumberSeekerCitiesDto>.CreateSuccess(new NumberSeekerCitiesDto
            {
                Cities = cities,
                DefaultCity = "تهران"
            });
        }

        public ApiResponse<NumberSeekerCategoriesDto> GetCategories()
        {
            var categories = KnownCategories
                .Select((name, index) => new NumberSeekerCategoryDto { Name = name, SortOrder = index + 1 })
                .ToList();

            return ApiResponse<NumberSeekerCategoriesDto>.CreateSuccess(new NumberSeekerCategoriesDto
            {
                Categories = categories,
                Placeholder = "مثال : کافه - رستوران و ..."
            });
        }

        public ApiResponse<NumberSeekerFormMetaDto> GetFormMeta()
        {
            return ApiResponse<NumberSeekerFormMetaDto>.CreateSuccess(new NumberSeekerFormMetaDto
            {
                Sources = KnownSources.OrderBy(s => s.SortOrder).ToList(),
                Cities = KnownCities
                    .Select((name, index) => new NumberSeekerCityDto { Name = name, SortOrder = index + 1 })
                    .ToList(),
                Categories = KnownCategories
                    .Select((name, index) => new NumberSeekerCategoryDto { Name = name, SortOrder = index + 1 })
                    .ToList(),
                DefaultCity = "تهران",
                CategoryPlaceholder = "مثال : کافه - رستوران و ...",
                MinPhones = 1,
                MaxPhones = 1000,
                DefaultPhones = 50
            });
        }

        public async Task<ApiResponse<NumberSeekerFormMetaDto>> GetFormMetaAsync(int userId)
        {
            var result = GetFormMeta();
            if (result.Data != null)
                result.Data.CanViewPhones = await _phoneAccess.CanViewPhonesAsync(userId);
            return result;
        }

        public async Task<ApiResponse<NumberSeekerExportDto>> ExportPhonesAsync(int userId, string taskId)
        {
            var resolved = await ResolvePhonesForExportAsync(userId, taskId);
            if (!resolved.Success)
            {
                return ApiResponse<NumberSeekerExportDto>.Error(
                    resolved.Message ?? NumberSeekerUserMessages.ExtractionFailed,
                    resolved.StatusCode,
                    errorCode: resolved.ErrorCode);
            }

            var (ownedTask, phones) = resolved.Data!;
            var canViewPhones = await _phoneAccess.CanViewPhonesAsync(userId);
            var visiblePhones = PhoneNumberMasker.ForClient(phones, canViewPhones);
            return ApiResponse<NumberSeekerExportDto>.CreateSuccess(new NumberSeekerExportDto
            {
                TaskId = ownedTask.ScraperTaskId,
                Source = ownedTask.Source,
                SourceDisplayName = NumberSeekerUiMapper.GetSourceDisplayName(ownedTask.Source),
                City = ownedTask.City,
                Category = ownedTask.Category,
                Status = ownedTask.Status,
                Count = visiblePhones.Count,
                Phones = visiblePhones,
                Format = "json",
                TextContent = string.Join("\n", visiblePhones),
                CanViewPhones = canViewPhones,
                IsPhonesMasked = !canViewPhones
            });
        }

        public async Task<ApiResponse<ExportExcelResultDto>> ExportPhonesToExcelAsync(int userId, string taskId)
        {
            try
            {
                var resolved = await ResolvePhonesForExportAsync(userId, taskId);
                if (!resolved.Success)
                {
                    return ApiResponse<ExportExcelResultDto>.Error(
                        resolved.Message ?? NumberSeekerUserMessages.ExtractionFailed,
                        resolved.StatusCode,
                        errorCode: resolved.ErrorCode);
                }

                var (ownedTask, phones) = resolved.Data!;
                var visiblePhones = PhoneNumberMasker.ForClient(
                    phones,
                    await _phoneAccess.CanViewPhonesAsync(userId));
                var bytes = BuildPhonesExcel(
                    visiblePhones,
                    NumberSeekerUiMapper.GetSourceDisplayName(ownedTask.Source),
                    ownedTask.City,
                    ownedTask.Category,
                    ownedTask.ScraperTaskId);

                var safeSource = SanitizeFilePart(ownedTask.Source);
                var safeCity = SanitizeFilePart(ownedTask.City);
                var fileName =
                    $"NumberSeeker_{safeSource}_{safeCity}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

                return ApiResponse<ExportExcelResultDto>.CreateSuccess(
                    new ExportExcelResultDto
                    {
                        FileContent = bytes,
                        FileName = fileName,
                        ContentType =
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        TotalCount = phones.Count,
                        ExportedCount = phones.Count,
                        PageNumber = 1,
                        PageSize = phones.Count,
                        TotalPages = 1
                    },
                    "فایل اکسل شماره‌ها آماده دانلود است");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excel export failed for NumberSeeker task {TaskId}", taskId);
                return ApiResponse<ExportExcelResultDto>.InternalServerError(ControlledErrorHelper.Unexpected);
            }
        }

        private async Task<ApiResponse<(NumberSeekerTask Task, List<string> Phones)>> ResolvePhonesForExportAsync(
            int userId,
            string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId))
            {
                return ApiResponse<(NumberSeekerTask, List<string>)>.BadRequest(
                    NumberSeekerUserMessages.TaskIdRequired,
                    errorCode: ErrorCodes.InvalidInput);
            }

            var ownedTask = await _taskRepository.GetByScraperTaskIdAndUserIdAsync(taskId.Trim(), userId);
            if (ownedTask == null)
            {
                return ApiResponse<(NumberSeekerTask, List<string>)>.NotFound(NumberSeekerUserMessages.TaskNotFound);
            }

            var phones = NumberSeekerPhoneStorage.Deserialize(ownedTask.PhonesJson);
            if (phones.Count == 0 && !TerminalStatuses.Contains(ownedTask.Status))
            {
                try
                {
                    var status = await _scraperClient.GetTaskStatusAsync(taskId.Trim());
                    await SyncOwnedTaskAsync(ownedTask, status);
                    phones = status.Phones ?? new List<string>();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Export fetch failed for task {TaskId}", taskId);
                }
            }

            if (phones.Count == 0)
            {
                return ApiResponse<(NumberSeekerTask, List<string>)>.BadRequest(
                    NumberSeekerUserMessages.NoPhonesForAction,
                    errorCode: ErrorCodes.InvalidInput);
            }

            return ApiResponse<(NumberSeekerTask, List<string>)>.CreateSuccess((ownedTask, phones));
        }

        private static byte[] BuildPhonesExcel(
            IReadOnlyList<string> phones,
            string sourceDisplayName,
            string city,
            string category,
            string taskId)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("شماره‌ها");
            worksheet.RightToLeft = true;

            worksheet.Cell(1, 1).Value = "ردیف";
            worksheet.Cell(1, 2).Value = "شماره موبایل";
            worksheet.Cell(1, 3).Value = "منبع";
            worksheet.Cell(1, 4).Value = "شهر";
            worksheet.Cell(1, 5).Value = "دسته";
            worksheet.Cell(1, 6).Value = "شناسه جستجو";

            var headerRange = worksheet.Range(1, 1, 1, 6);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#0D9488");
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            for (var i = 0; i < phones.Count; i++)
            {
                var row = i + 2;
                worksheet.Cell(row, 1).Value = i + 1;
                worksheet.Cell(row, 2).Value = phones[i];
                worksheet.Cell(row, 2).Style.NumberFormat.Format = "@";
                worksheet.Cell(row, 3).Value = sourceDisplayName;
                worksheet.Cell(row, 4).Value = city;
                worksheet.Cell(row, 5).Value = category;
                worksheet.Cell(row, 6).Value = taskId;
            }

            worksheet.Columns().AdjustToContents();
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private static string SanitizeFilePart(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "na";
            var cleaned = string.Concat(value.Trim().Where(ch =>
                !Path.GetInvalidFileNameChars().Contains(ch) && ch != ' ' && ch != '/'));
            return string.IsNullOrWhiteSpace(cleaned) ? "na" : cleaned;
        }

        private static NumberSeekerCancelResultDto BuildCancelResult(NumberSeekerTask ownedTask)
        {
            var phones = NumberSeekerPhoneStorage.Deserialize(ownedTask.PhonesJson);
            var count = phones.Count > 0 ? phones.Count : ownedTask.CurrentCount;
            return new NumberSeekerCancelResultDto
            {
                TaskId = ownedTask.ScraperTaskId,
                Message = NumberSeekerUserMessages.Cancelled,
                Status = "cancelled",
                StatusDisplayName = NumberSeekerUiMapper.GetStatusDisplayName("cancelled"),
                CurrentCount = count,
                ProgressPercent = NumberSeekerUiMapper.ComputeProgressPercent(count, ownedTask.TargetCount),
                CanDownload = phones.Count > 0,
                CanImport = false
            };
        }

        private async Task SyncOwnedTaskAsync(NumberSeekerTask ownedTask, NumberSeekerTaskStatusDto status)
        {
            var changed = false;

            if (!string.Equals(ownedTask.Status, status.Status, StringComparison.OrdinalIgnoreCase))
            {
                ownedTask.Status = status.Status;
                changed = true;
            }

            if (ownedTask.CurrentCount != status.CurrentCount)
            {
                ownedTask.CurrentCount = status.CurrentCount;
                changed = true;
            }

            if (!string.Equals(ownedTask.ResultCode, status.ResultCode, StringComparison.Ordinal))
            {
                ownedTask.ResultCode = status.ResultCode;
                changed = true;
            }

            if (!string.Equals(ownedTask.Message, status.Message, StringComparison.Ordinal))
            {
                ownedTask.Message = NumberSeekerUserMessages.ForTaskStatus(
                    status.Status,
                    status.ResultCode,
                    status.CurrentCount);
                changed = true;
            }

            if (status.Phones is { Count: > 0 })
            {
                PersistPhones(ownedTask, status.Phones);
                changed = true;
            }

            if (TerminalStatuses.Contains(status.Status) && ownedTask.CompletedAt == null)
            {
                ownedTask.CompletedAt = DateTime.UtcNow;
                changed = true;
            }

            if (changed)
            {
                ownedTask.UpdatedAt = DateTime.UtcNow;
                await _taskRepository.UpdateAsync(ownedTask);
            }
        }

        private static void PersistPhones(NumberSeekerTask ownedTask, IReadOnlyList<string> phones)
        {
            var json = NumberSeekerPhoneStorage.Serialize(phones);
            if (string.IsNullOrEmpty(json))
                return;

            ownedTask.PhonesJson = json;
            ownedTask.PhonesPersistedAt = DateTime.UtcNow;
            if (ownedTask.CurrentCount < phones.Count)
            {
                ownedTask.CurrentCount = phones.Count;
            }
        }

        private static NumberSeekerTaskSummaryDto MapSummary(NumberSeekerTask t)
        {
            var hasPhones = !string.IsNullOrWhiteSpace(t.PhonesJson);
            var progress = NumberSeekerUiMapper.ComputeProgressPercent(t.CurrentCount, t.TargetCount);

            return new NumberSeekerTaskSummaryDto
            {
                TaskId = t.ScraperTaskId,
                Source = t.Source,
                SourceDisplayName = NumberSeekerUiMapper.GetSourceDisplayName(t.Source),
                City = t.City,
                Category = t.Category,
                Subtitle = NumberSeekerUiMapper.BuildSubtitle(t.City, t.Category),
                Status = t.Status,
                StatusDisplayName = NumberSeekerUiMapper.GetStatusDisplayName(t.Status),
                StatusTone = NumberSeekerUiMapper.GetStatusTone(t.Status),
                CurrentCount = t.CurrentCount,
                TargetCount = t.TargetCount,
                ProgressPercent = progress,
                CountLabel = $"{t.CurrentCount}/{t.TargetCount}",
                CreatedAt = t.CreatedAt.ToString("O"),
                CreatedAtPersian = NumberSeekerUiMapper.ToPersianDate(t.CreatedAt),
                CompletedAt = t.CompletedAt?.ToString("O"),
                CompletedAtPersian = t.CompletedAt.HasValue
                    ? NumberSeekerUiMapper.ToPersianDate(t.CompletedAt.Value)
                    : null,
                ImportedAt = t.ImportedAt?.ToString("O"),
                ImportedCount = t.ImportedCount,
                CanDownload = hasPhones && NumberSeekerUiMapper.IsDownloadable(t.Status),
                CanImport = hasPhones && NumberSeekerUiMapper.IsImportable(t.Status),
                IsTerminal = NumberSeekerUiMapper.IsTerminal(t.Status)
            };
        }

        private static void EnrichStatusForUi(NumberSeekerTaskStatusDto status, DateTime createdAt)
        {
            var allPhones = status.Phones ?? new List<string>();
            var terminal = NumberSeekerUiMapper.IsTerminal(status.Status);
            var running = string.Equals(status.Status, "running", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status.Status, "pending", StringComparison.OrdinalIgnoreCase);

            // همیشه progress را از شمارنده‌ها به‌صورت double با یک رقم اعشار محاسبه کن
            status.ProgressPercent = NumberSeekerUiMapper.ComputeProgressPercent(
                status.CurrentCount,
                status.TargetCount);

            status.SourceDisplayName = NumberSeekerUiMapper.GetSourceDisplayName(status.Source);
            status.StatusDisplayName = NumberSeekerUiMapper.GetStatusDisplayName(status.Status);
            status.StatusTone = NumberSeekerUiMapper.GetStatusTone(status.Status);
            status.Subtitle = NumberSeekerUiMapper.BuildSubtitle(status.City, status.Category);
            status.IsTerminal = terminal;
            status.IsRunning = running;
            status.CanCancel = running;
            status.CanImport = NumberSeekerUiMapper.IsImportable(status.Status) && allPhones.Count > 0;
            status.CanDownload = NumberSeekerUiMapper.IsDownloadable(status.Status) && allPhones.Count > 0;
            status.ProgressLabel = NumberSeekerUiMapper.BuildProgressLabel(status.CurrentCount, status.TargetCount);
            status.PhonesPreviewLimit = NumberSeekerUiMapper.PhonesPreviewLimit;
            status.PhonesPreview = NumberSeekerUiMapper.TakePreview(allPhones);
            status.CreatedAtPersian = NumberSeekerUiMapper.ToPersianDate(createdAt);
            status.ResultTitle = NumberSeekerUiMapper.BuildResultTitle(status.Status);
            status.ResultCountLabel = NumberSeekerUiMapper.BuildResultCountLabel(status.CurrentCount);

            // هرگز Error/Message فنی اسکرپر را به موبایل نده
            status.Error = null;
            status.Message = NumberSeekerUserMessages.ForTaskStatus(
                status.Status,
                status.ResultCode,
                status.CurrentCount);

            // صفحه در حال جستجو: فقط preview؛ نتایج: لیست کامل
            if (!terminal)
            {
                status.Phones = status.PhonesPreview;
            }

            if (status.ElapsedSeconds is null or <= 0 && !terminal)
            {
                status.ElapsedSeconds = Math.Max(0, (DateTime.UtcNow - createdAt).TotalSeconds);
            }

            var (etaSeconds, etaText) = NumberSeekerUiMapper.EstimateRemaining(
                status.Status,
                status.CurrentCount,
                status.TargetCount,
                status.ElapsedSeconds,
                status.QueuePosition);
            status.EstimatedSecondsRemaining = etaSeconds;
            status.EstimatedRemainingText = etaText;
        }

        private async Task ApplyPhoneVisibilityAsync(int userId, NumberSeekerTaskStatusDto status)
        {
            var canView = await _phoneAccess.CanViewPhonesAsync(userId);
            status.CanViewPhones = canView;
            status.IsPhonesMasked = !canView;
            if (canView)
                return;

            status.Phones = PhoneNumberMasker.ForClient(status.Phones, canViewPhones: false);
            status.PhonesPreview = PhoneNumberMasker.ForClient(status.PhonesPreview, canViewPhones: false);
        }

        private static NumberSeekerTaskStatusDto BuildStatusFromOwnedTask(
            NumberSeekerTask ownedTask,
            List<string> phones)
        {
            var count = phones.Count > 0 ? phones.Count : ownedTask.CurrentCount;
            return new NumberSeekerTaskStatusDto
            {
                TaskId = ownedTask.ScraperTaskId,
                Source = ownedTask.Source,
                City = ownedTask.City,
                Category = ownedTask.Category,
                Status = ownedTask.Status,
                TargetCount = ownedTask.TargetCount,
                CurrentCount = count,
                ProgressPercent = NumberSeekerUiMapper.ComputeProgressPercent(count, ownedTask.TargetCount),
                Phones = phones,
                Message = ownedTask.Message,
                ResultCode = ownedTask.ResultCode,
                StartedAt = ownedTask.CreatedAt.ToString("O"),
                CompletedAt = ownedTask.CompletedAt?.ToString("O")
            };
        }
    }
}
