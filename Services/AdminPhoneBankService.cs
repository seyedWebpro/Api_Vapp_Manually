using Api_Vapp.Constants;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.NumberSeeker;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;

namespace Api_Vapp.Services
{
    /// <summary>
    /// مدیریت بانک شماره برای ادمین — مشاهده موجودی و پر کردن از طریق اسکرپ.
    /// </summary>
    public class AdminPhoneBankService : IAdminPhoneBankService
    {
        private readonly INumberSeekerPhoneBankRepository _bankRepository;
        private readonly INumberScraperClient _scraperClient;
        private readonly INumberSeekerTaskRepository _taskRepository;
        private readonly IAuditService _audit;
        private readonly ILogger<AdminPhoneBankService> _logger;

        public AdminPhoneBankService(
            INumberSeekerPhoneBankRepository bankRepository,
            INumberScraperClient scraperClient,
            INumberSeekerTaskRepository taskRepository,
            IAuditService audit,
            ILogger<AdminPhoneBankService> logger)
        {
            _bankRepository = bankRepository;
            _scraperClient = scraperClient;
            _taskRepository = taskRepository;
            _audit = audit;
            _logger = logger;
        }

        public async Task<ApiResponse<AdminPhoneBankOverviewDto>> GetOverviewAsync()
        {
            var (total, available) = await _bankRepository.CountAsync();
            var stats = await _bankRepository.GetStatsAsync();

            var overview = new AdminPhoneBankOverviewDto
            {
                TotalPhones = total,
                AvailablePhones = available,
                ScraperEnabled = _scraperClient.IsEnabled,
                ScraperReachable = false,
                Stats = stats.Select(s => new AdminPhoneBankStatDto
                {
                    City = s.City,
                    Category = s.Category,
                    Source = s.Source,
                    SourceDisplayName = NumberSeekerUiMapper.GetSourceDisplayName(s.Source),
                    Total = s.Total,
                    Available = s.Available
                }).ToList()
            };

            if (!_scraperClient.IsEnabled)
            {
                overview.Hint = "اسکرپر غیرفعال است؛ فقط از موجودی فعلی بانک می‌توانید استفاده کنید.";
                return ApiResponse<AdminPhoneBankOverviewDto>.CreateSuccess(overview);
            }

            try
            {
                overview.ScraperReachable = await _scraperClient.IsAvailableAsync();
                if (!overview.ScraperReachable)
                    overview.Hint = "اسکرپر در دسترس نیست. اتصال و کلید API را بررسی کنید.";
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Phone bank overview: scraper health check failed");
                overview.ScraperReachable = false;
                overview.Hint = "اسکرپر در دسترس نیست. اتصال و کلید API را بررسی کنید.";
            }

            return ApiResponse<AdminPhoneBankOverviewDto>.CreateSuccess(overview);
        }

        public async Task<ApiResponse<AdminPhoneBankFillResultDto>> StartFillAsync(
            int adminUserId,
            AdminPhoneBankFillDto request)
        {
            if (!_scraperClient.IsEnabled)
            {
                return ApiResponse<AdminPhoneBankFillResultDto>.Error(
                    "سرویس اسکرپر غیرفعال است.",
                    503,
                    errorCode: "SCRAPER_DISABLED");
            }

            if (!NumberSeekerCategoryHelper.TryNormalize(request.Category, out var category, out var categoryError))
            {
                var message = categoryError ?? "دسته‌بندی نامعتبر است.";
                return ApiResponse<AdminPhoneBankFillResultDto>.BadRequest(
                    message,
                    new List<string> { message },
                    ErrorCodes.ValidationFailed);
            }

            var city = (request.City ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(city))
            {
                return ApiResponse<AdminPhoneBankFillResultDto>.BadRequest(
                    "شهر الزامی است",
                    new List<string> { "شهر الزامی است" },
                    ErrorCodes.ValidationFailed);
            }

            try
            {
                var scrapeRequest = new StartNumberSeekerScrapeDto
                {
                    Source = request.Source.Trim().ToLowerInvariant(),
                    City = city,
                    Category = category,
                    MaxPhones = request.MaxPhones
                };

                var created = await _scraperClient.StartScrapeAsync(scrapeRequest);

                var ownedTask = new NumberSeekerTask
                {
                    UserId = adminUserId,
                    ScraperTaskId = created.TaskId,
                    Source = created.Source,
                    City = city,
                    Category = category,
                    TargetCount = request.MaxPhones,
                    Status = created.Status,
                    CurrentCount = 0,
                    Message = "جمع‌آوری برای بانک شماره شروع شد.",
                    CreatedAt = DateTime.UtcNow
                };

                try
                {
                    await _taskRepository.AddAsync(ownedTask);
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "DB save failed after bank-fill task {TaskId}", created.TaskId);
                    try
                    {
                        await _scraperClient.CancelTaskAsync(created.TaskId);
                    }
                    catch (Exception cancelEx)
                    {
                        _logger.LogWarning(cancelEx, "Failed to cancel orphan bank-fill task {TaskId}", created.TaskId);
                    }

                    throw;
                }

                await _audit.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.NumberSeeker,
                    Action = AuditActions.NumberSeekerBankFillStarted,
                    EntityType = AuditEntityTypes.NumberSeekerPhoneBank,
                    EntityId = created.TaskId,
                    ActorUserId = adminUserId,
                    After = new
                    {
                        source = ownedTask.Source,
                        city = ownedTask.City,
                        category = ownedTask.Category,
                        targetCount = ownedTask.TargetCount
                    }
                });

                return ApiResponse<AdminPhoneBankFillResultDto>.CreateSuccess(
                    new AdminPhoneBankFillResultDto
                    {
                        TaskId = created.TaskId,
                        Source = ownedTask.Source,
                        City = city,
                        Category = category,
                        Status = created.Status,
                        Message = "جمع‌آوری شروع شد. پس از اتمام، شماره‌ها به بانک اضافه می‌شوند."
                    },
                    "جمع‌آوری شروع شد.",
                    StatusCodes.Status201Created);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Scraper API key rejected for bank fill by admin {UserId}", adminUserId);
                return ApiResponse<AdminPhoneBankFillResultDto>.Error(
                    "احراز هویت اسکرپر ناموفق بود.",
                    503,
                    errorCode: "SCRAPER_AUTH_FAILED");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start bank fill for admin {UserId}", adminUserId);
                return ApiResponse<AdminPhoneBankFillResultDto>.Error(
                    "شروع جمع‌آوری ناموفق بود. لطفاً دوباره تلاش کنید.",
                    503,
                    errorCode: "SCRAPER_UNAVAILABLE");
            }
        }

        public async Task<ApiResponse<AdminPhoneBankImportResultDto>> ImportPhonesAsync(
            int adminUserId,
            AdminPhoneBankImportDto request)
        {
            if (!NumberSeekerCategoryHelper.TryNormalize(request.Category, out var category, out var categoryError))
            {
                var message = categoryError ?? "دسته‌بندی نامعتبر است.";
                return ApiResponse<AdminPhoneBankImportResultDto>.BadRequest(
                    message,
                    new List<string> { message },
                    ErrorCodes.ValidationFailed);
            }

            var city = (request.City ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(city))
            {
                return ApiResponse<AdminPhoneBankImportResultDto>.BadRequest(
                    "شهر الزامی است",
                    new List<string> { "شهر الزامی است" },
                    ErrorCodes.ValidationFailed);
            }

            var phones = (request.Phones ?? new List<string>())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (phones.Count == 0)
            {
                return ApiResponse<AdminPhoneBankImportResultDto>.BadRequest(
                    "حداقل یک شماره الزامی است",
                    new List<string> { "حداقل یک شماره الزامی است" },
                    ErrorCodes.ValidationFailed);
            }

            var source = string.IsNullOrWhiteSpace(request.Source)
                ? "manual"
                : request.Source.Trim().ToLowerInvariant();

            var inserted = await _bankRepository.UpsertPhonesAsync(phones, source, city, category);

            await _audit.WriteAsync(new AuditEntry
            {
                Category = AuditCategories.NumberSeeker,
                Action = AuditActions.NumberSeekerBankImported,
                EntityType = AuditEntityTypes.NumberSeekerPhoneBank,
                EntityId = $"{city}|{category}|{source}",
                ActorUserId = adminUserId,
                After = new { inserted, totalSubmitted = phones.Count, city, category, source }
            });

            var msg = inserted > 0
                ? $"{inserted} شماره جدید به بانک اضافه شد."
                : "شماره جدیدی اضافه نشد (همه تکراری بودند).";

            return ApiResponse<AdminPhoneBankImportResultDto>.CreateSuccess(
                new AdminPhoneBankImportResultDto
                {
                    Inserted = inserted,
                    TotalSubmitted = phones.Count,
                    City = city,
                    Category = category,
                    Source = source,
                    Message = msg
                },
                msg);
        }

        public async Task<ApiResponse<AdminPhoneBankDeleteResultDto>> DeletePhonesAsync(
            int adminUserId,
            AdminPhoneBankDeletePhonesDto request)
        {
            var phones = (request.Phones ?? new List<string>())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (phones.Count == 0)
            {
                return ApiResponse<AdminPhoneBankDeleteResultDto>.BadRequest(
                    "حداقل یک شماره الزامی است",
                    new List<string> { "حداقل یک شماره الزامی است" },
                    ErrorCodes.ValidationFailed);
            }

            var deleted = await _bankRepository.SoftDeleteByPhonesAsync(phones);

            await _audit.WriteAsync(new AuditEntry
            {
                Category = AuditCategories.NumberSeeker,
                Action = AuditActions.NumberSeekerBankPhonesDeleted,
                EntityType = AuditEntityTypes.NumberSeekerPhoneBank,
                EntityId = deleted.ToString(),
                ActorUserId = adminUserId,
                After = new { deleted, requested = phones.Count }
            });

            var msg = deleted > 0
                ? $"{deleted} شماره از بانک حذف شد."
                : "شماره‌ای برای حذف پیدا نشد.";

            return ApiResponse<AdminPhoneBankDeleteResultDto>.CreateSuccess(
                new AdminPhoneBankDeleteResultDto { Deleted = deleted, Message = msg },
                msg);
        }
    }
}
