using Api_Vapp.Configuration;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Contact;
using Api_Vapp.DTOs.NumberSeeker;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
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
            new() { Code = "sheypoor", DisplayName = "شیپور" },
            new() { Code = "divar", DisplayName = "دیوار" },
            new() { Code = "nshan", DisplayName = "نشان" },
            new() { Code = "balad", DisplayName = "بلد" },
            new() { Code = "googlemaps", DisplayName = "گوگل مپ" }
        };

        private readonly INumberScraperClient _scraperClient;
        private readonly INumberSeekerTaskRepository _taskRepository;
        private readonly IContactService _contactService;
        private readonly INumberSeekerRateLimiter _rateLimiter;
        private readonly NumberSeekerOptions _options;
        private readonly ILogger<NumberSeekerService> _logger;

        public NumberSeekerService(
            INumberScraperClient scraperClient,
            INumberSeekerTaskRepository taskRepository,
            IContactService contactService,
            INumberSeekerRateLimiter rateLimiter,
            IOptions<NumberSeekerOptions> options,
            ILogger<NumberSeekerService> logger)
        {
            _scraperClient = scraperClient;
            _taskRepository = taskRepository;
            _contactService = contactService;
            _rateLimiter = rateLimiter;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<ApiResponse<NumberSeekerTaskCreatedDto>> StartScrapeAsync(
            int userId,
            StartNumberSeekerScrapeDto request)
        {
            if (!_scraperClient.IsEnabled)
            {
                return ApiResponse<NumberSeekerTaskCreatedDto>.Error(
                    "سرویس شماره‌جو در حال حاضر غیرفعال است.",
                    503,
                    errorCode: "SCRAPER_DISABLED");
            }

            var (allowed, retryAfter) = await _rateLimiter.CheckScrapeAsync(userId);
            if (!allowed)
            {
                return ApiResponse<NumberSeekerTaskCreatedDto>.Error(
                    $"محدودیت تعداد درخواست — لطفاً {retryAfter} ثانیه دیگر تلاش کنید.",
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
                    Message = created.Message,
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

                created.PollUrl = $"/api/NumberSeeker/task/{created.TaskId}";

                return ApiResponse<NumberSeekerTaskCreatedDto>.CreateSuccess(
                    created,
                    created.Message,
                    StatusCodes.Status201Created);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Scraper API key rejected for user {UserId}", userId);
                return ApiResponse<NumberSeekerTaskCreatedDto>.InternalServerError(
                    "پیکربندی اتصال به سرویس شماره‌جو نادرست است.",
                    "SCRAPER_AUTH_FAILED");
            }
            catch (ArgumentException ex)
            {
                return ApiResponse<NumberSeekerTaskCreatedDto>.BadRequest(ex.Message, errorCode: ErrorCodes.InvalidInput);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("محدودیت نرخ"))
            {
                return ApiResponse<NumberSeekerTaskCreatedDto>.Error(ex.Message, 429, errorCode: "RATE_LIMITED");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start number seeker scrape for user {UserId}", userId);
                return ApiResponse<NumberSeekerTaskCreatedDto>.Error(
                    "سرویس شماره‌جو در دسترس نیست. لطفاً بعداً تلاش کنید.",
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
                return ApiResponse<NumberSeekerTaskStatusDto>.BadRequest("شناسه تسک الزامی است.");
            }

            var ownedTask = await _taskRepository.GetByScraperTaskIdAndUserIdAsync(taskId.Trim(), userId);
            if (ownedTask == null)
            {
                return ApiResponse<NumberSeekerTaskStatusDto>.NotFound("تسک یافت نشد یا متعلق به شما نیست.");
            }

            try
            {
                var status = await _scraperClient.GetTaskStatusAsync(taskId.Trim());
                await SyncOwnedTaskAsync(ownedTask, status);
                return ApiResponse<NumberSeekerTaskStatusDto>.CreateSuccess(status);
            }
            catch (KeyNotFoundException)
            {
                return ApiResponse<NumberSeekerTaskStatusDto>.NotFound("تسک در سرویس اسکرپ یافت نشد.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get task status {TaskId} for user {UserId}", taskId, userId);
                return ApiResponse<NumberSeekerTaskStatusDto>.Error(
                    "دریافت وضعیت تسک با خطا مواجه شد.",
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
                return ApiResponse<NumberSeekerCancelResultDto>.BadRequest("شناسه تسک الزامی است.");
            }

            var ownedTask = await _taskRepository.GetByScraperTaskIdAndUserIdAsync(taskId.Trim(), userId);
            if (ownedTask == null)
            {
                return ApiResponse<NumberSeekerCancelResultDto>.NotFound("تسک یافت نشد یا متعلق به شما نیست.");
            }

            try
            {
                var result = await _scraperClient.CancelTaskAsync(taskId.Trim());

                ownedTask.Status = "cancelled";
                ownedTask.CompletedAt = DateTime.UtcNow;
                ownedTask.Message = result.Message;
                await _taskRepository.UpdateAsync(ownedTask);

                return ApiResponse<NumberSeekerCancelResultDto>.CreateSuccess(result, result.Message);
            }
            catch (KeyNotFoundException)
            {
                return ApiResponse<NumberSeekerCancelResultDto>.NotFound("تسک در سرویس اسکرپ یافت نشد.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel task {TaskId} for user {UserId}", taskId, userId);
                return ApiResponse<NumberSeekerCancelResultDto>.Error(
                    "لغو تسک با خطا مواجه شد.",
                    503,
                    errorCode: "SCRAPER_UNAVAILABLE");
            }
        }

        public async Task<ApiResponse<NumberSeekerImportResultDto>> ImportPhonesAsync(
            int userId,
            string taskId,
            ImportNumberSeekerPhonesDto request)
        {
            if (string.IsNullOrWhiteSpace(taskId))
            {
                return ApiResponse<NumberSeekerImportResultDto>.BadRequest("شناسه تسک الزامی است.");
            }

            var (allowed, retryAfter) = await _rateLimiter.CheckImportAsync(userId);
            if (!allowed)
            {
                return ApiResponse<NumberSeekerImportResultDto>.Error(
                    $"محدودیت import — لطفاً {retryAfter} ثانیه دیگر تلاش کنید.",
                    429,
                    errorCode: "RATE_LIMITED");
            }

            var ownedTask = await _taskRepository.GetByScraperTaskIdAndUserIdAsync(taskId.Trim(), userId);
            if (ownedTask == null)
            {
                return ApiResponse<NumberSeekerImportResultDto>.NotFound("تسک یافت نشد یا متعلق به شما نیست.");
            }

            if (ownedTask.ImportedAt != null && !request.Force)
            {
                return ApiResponse<NumberSeekerImportResultDto>.Error(
                    "این تسک قبلاً به دفترچه import شده است. برای import مجدد Force=true بفرستید.",
                    409,
                    errorCode: "ALREADY_IMPORTED");
            }

            NumberSeekerTaskStatusDto status;
            try
            {
                status = await _scraperClient.GetTaskStatusAsync(taskId.Trim());
                await SyncOwnedTaskAsync(ownedTask, status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch phones for import task {TaskId}", taskId);
                return ApiResponse<NumberSeekerImportResultDto>.Error(
                    "دریافت شماره‌ها از سرویس اسکرپ با خطا مواجه شد.",
                    503,
                    errorCode: "SCRAPER_UNAVAILABLE");
            }

            if (!ImportableStatuses.Contains(status.Status))
            {
                return ApiResponse<NumberSeekerImportResultDto>.BadRequest(
                    "تسک هنوز تمام نشده — ابتدا تا وضعیت completed یا partial صبر کنید.",
                    errorCode: ErrorCodes.InvalidInput);
            }

            if (status.Phones == null || status.Phones.Count == 0)
            {
                return ApiResponse<NumberSeekerImportResultDto>.BadRequest(
                    "شماره‌ای برای import وجود ندارد.",
                    errorCode: ErrorCodes.InvalidInput);
            }

            var namePrefix = string.IsNullOrWhiteSpace(request.ContactNamePrefix)
                ? _options.DefaultContactNamePrefix
                : request.ContactNamePrefix.Trim();

            var importDto = new ImportContactsFromListDto
            {
                ContactNotebookId = request.ContactNotebookId,
                Contacts = status.Phones
                    .Select((phone, index) => new ImportContactItemDto
                    {
                        MobileNumber = phone,
                        Name = $"{namePrefix} {index + 1}"
                    })
                    .ToList()
            };

            var importResult = await _contactService.ImportFromListAsync(userId, importDto);
            if (!importResult.Success)
            {
                return ApiResponse<NumberSeekerImportResultDto>.Error(
                    importResult.Message,
                    importResult.StatusCode,
                    importResult.Errors,
                    importResult.ErrorCode);
            }

            await _rateLimiter.RecordImportAsync(userId);

            ownedTask.ImportedAt = DateTime.UtcNow;
            ownedTask.ImportedCount = importResult.Data?.SuccessCount ?? 0;
            ownedTask.ImportedNotebookId = request.ContactNotebookId;
            await _taskRepository.UpdateAsync(ownedTask);

            var data = importResult.Data!;
            var result = new NumberSeekerImportResultDto
            {
                TaskId = taskId.Trim(),
                ContactNotebookId = request.ContactNotebookId,
                TotalPhones = status.Phones.Count,
                SuccessCount = data.SuccessCount,
                DuplicateCount = data.DuplicateCount,
                SkippedCount = data.SkippedCount,
                ErrorCount = data.ErrorCount,
                Errors = data.Errors.Select(e => new ImportRowErrorDto
                {
                    RowNumber = e.RowNumber,
                    MobileNumber = e.MobileNumber,
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
                return ApiResponse<bool>.BadRequest("شناسه تسک الزامی است.");
            }

            var ownedTask = await _taskRepository.GetByScraperTaskIdTrackedAsync(webhook.TaskId.Trim());
            if (ownedTask == null)
            {
                _logger.LogDebug("Webhook for unknown task {TaskId} — ignored", webhook.TaskId);
                return ApiResponse<bool>.CreateSuccess(true, "تسک در Vapp ثبت نشده — نادیده گرفته شد.");
            }

            ownedTask.Status = webhook.Status;
            ownedTask.CurrentCount = webhook.CurrentCount;
            ownedTask.ResultCode = webhook.ResultCode;
            ownedTask.Message = webhook.Message;

            if (TerminalStatuses.Contains(webhook.Status) && ownedTask.CompletedAt == null)
            {
                ownedTask.CompletedAt = DateTime.UtcNow;
            }

            await _taskRepository.UpdateAsync(ownedTask);
            return ApiResponse<bool>.CreateSuccess(true, "وضعیت تسک به‌روزرسانی شد.");
        }

        public async Task<ApiResponse<NumberSeekerTaskListDto>> GetRecentTasksAsync(
            int userId,
            int limit = 20)
        {
            var tasks = await _taskRepository.GetRecentByUserIdAsync(userId, limit);

            var summaries = tasks.Select(t => new NumberSeekerTaskSummaryDto
            {
                TaskId = t.ScraperTaskId,
                Source = t.Source,
                City = t.City,
                Category = t.Category,
                Status = t.Status,
                CurrentCount = t.CurrentCount,
                TargetCount = t.TargetCount,
                ProgressPercent = t.TargetCount > 0
                    ? Math.Round(t.CurrentCount * 100.0 / t.TargetCount, 1)
                    : 0,
                CreatedAt = t.CreatedAt.ToString("O"),
                ImportedAt = t.ImportedAt?.ToString("O"),
                ImportedCount = t.ImportedCount
            }).ToList();

            return ApiResponse<NumberSeekerTaskListDto>.CreateSuccess(new NumberSeekerTaskListDto
            {
                Count = summaries.Count,
                Tasks = summaries
            });
        }

        public async Task<ApiResponse<NumberSeekerHealthDto>> GetHealthAsync()
        {
            var health = await _scraperClient.GetHealthAsync();
            return ApiResponse<NumberSeekerHealthDto>.CreateSuccess(health);
        }

        public ApiResponse<NumberSeekerSourcesDto> GetSources()
        {
            return ApiResponse<NumberSeekerSourcesDto>.CreateSuccess(new NumberSeekerSourcesDto
            {
                Sources = KnownSources.ToList()
            });
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
                ownedTask.Message = status.Message;
                changed = true;
            }

            if (TerminalStatuses.Contains(status.Status) && ownedTask.CompletedAt == null)
            {
                ownedTask.CompletedAt = DateTime.UtcNow;
                changed = true;
            }

            if (changed)
            {
                await _taskRepository.UpdateAsync(ownedTask);
            }
        }
    }
}
