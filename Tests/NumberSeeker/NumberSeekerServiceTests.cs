using Api_Vapp.Configuration;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Contact;
using Api_Vapp.DTOs.NumberSeeker;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services;
using Api_Vapp.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api_Vapp.Tests.NumberSeeker;

public class NumberSeekerRateLimiterTests
{
    [Fact]
    public async Task ScrapeLimit_BlocksAfterMax()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var limiter = new NumberSeekerRateLimiter(
            cache,
            Options.Create(new NumberSeekerOptions { MaxScrapesPerHour = 2 }));

        await limiter.RecordScrapeAsync(1);
        await limiter.RecordScrapeAsync(1);

        var (allowed, retryAfter) = await limiter.CheckScrapeAsync(1);
        Assert.False(allowed);
        Assert.True(retryAfter > 0);
    }
}

public class NumberSeekerServiceTests
{
    [Fact]
    public async Task StartScrape_PersistsOwnedTask()
    {
        var client = new FakeScraperClient();
        var repo = new InMemoryTaskRepository();
        var service = BuildService(client, repo);

        var result = await service.StartScrapeAsync(10, new StartNumberSeekerScrapeDto
        {
            Source = "divar",
            City = "تهران",
            Category = "رستوران",
            MaxPhones = 5
        });

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.Single(repo.Tasks);
        Assert.Equal(10, repo.Tasks[0].UserId);
    }

    [Fact]
    public async Task GetTaskStatus_RejectsForeignTask()
    {
        var client = new FakeScraperClient();
        var repo = new InMemoryTaskRepository();
        repo.Tasks.Add(new NumberSeekerTask
        {
            UserId = 99,
            ScraperTaskId = "task-1",
            Source = "divar",
            City = "تهران",
            Category = "x",
            TargetCount = 5,
            Status = "pending"
        });

        var service = BuildService(client, repo);
        var result = await service.GetTaskStatusAsync(10, "task-1");

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task GetTaskStatus_ProgressPercentIsFractionalDouble()
    {
        var client = new FakeScraperClient();
        var repo = new InMemoryTaskRepository();
        repo.Tasks.Add(new NumberSeekerTask
        {
            UserId = 10,
            ScraperTaskId = "task-progress",
            Source = "divar",
            City = "تهران",
            Category = "x",
            TargetCount = 3,
            CurrentCount = 1,
            Status = "completed",
            PhonesJson = "[\"09121111111\"]",
            PhonesPersistedAt = DateTime.UtcNow
        });

        var service = BuildService(client, repo);
        var result = await service.GetTaskStatusAsync(10, "task-progress");

        Assert.True(result.Success);
        Assert.Equal(33.3, result.Data!.ProgressPercent, 1);
    }

    [Fact]
    public async Task CancelTask_MarksCancelledAndIsIdempotent()
    {
        var client = new FakeScraperClient();
        var repo = new InMemoryTaskRepository();
        repo.Tasks.Add(new NumberSeekerTask
        {
            UserId = 10,
            ScraperTaskId = "task-cancel",
            Source = "divar",
            City = "تهران",
            Category = "x",
            TargetCount = 10,
            CurrentCount = 2,
            Status = "running",
            PhonesJson = "[\"09121111111\",\"09122222222\"]"
        });

        var service = BuildService(client, repo);
        var first = await service.CancelTaskAsync(10, "task-cancel");
        Assert.True(first.Success);
        Assert.Equal("cancelled", repo.Tasks[0].Status);
        Assert.Equal("cancelled", first.Data!.Status);
        Assert.True(first.Data.CanDownload);

        var second = await service.CancelTaskAsync(10, "task-cancel");
        Assert.True(second.Success);
        Assert.Equal("cancelled", second.Data!.Status);
    }

    [Fact]
    public async Task CancelTask_RejectsCompletedTask()
    {
        var client = new FakeScraperClient();
        var repo = new InMemoryTaskRepository();
        repo.Tasks.Add(new NumberSeekerTask
        {
            UserId = 10,
            ScraperTaskId = "task-done",
            Source = "divar",
            City = "تهران",
            Category = "x",
            TargetCount = 1,
            Status = "completed",
            PhonesJson = "[\"09121111111\"]"
        });

        var service = BuildService(client, repo);
        var result = await service.CancelTaskAsync(10, "task-done");
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task ExportPhonesToExcel_ReturnsXlsxBytes()
    {
        var client = new FakeScraperClient();
        var repo = new InMemoryTaskRepository();
        repo.Tasks.Add(new NumberSeekerTask
        {
            UserId = 10,
            ScraperTaskId = "task-xlsx",
            Source = "divar",
            City = "تهران",
            Category = "رستوران",
            TargetCount = 2,
            Status = "completed",
            PhonesJson = "[\"09121111111\",\"09122222222\"]",
            PhonesPersistedAt = DateTime.UtcNow
        });

        var service = BuildService(client, repo, canViewPhones: true);
        var result = await service.ExportPhonesToExcelAsync(10, "task-xlsx");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.FileContent.Length > 100);
        Assert.EndsWith(".xlsx", result.Data.FileName);
        Assert.Equal(2, result.Data.ExportedCount);

        using var stream = new MemoryStream(result.Data.FileContent);
        using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);
        Assert.Equal("شماره موبایل", sheet.Cell(1, 2).GetString());
        Assert.Equal("09121111111", sheet.Cell(2, 2).GetString());
    }

    [Fact]
    public async Task ExportPhonesToExcel_MasksNumbersForRegularUser()
    {
        var client = new FakeScraperClient();
        var repo = new InMemoryTaskRepository();
        repo.Tasks.Add(new NumberSeekerTask
        {
            UserId = 10,
            ScraperTaskId = "task-xlsx-mask",
            Source = "divar",
            City = "تهران",
            Category = "رستوران",
            TargetCount = 1,
            Status = "completed",
            PhonesJson = "[\"09121234567\"]",
            PhonesPersistedAt = DateTime.UtcNow
        });

        var service = BuildService(client, repo, canViewPhones: false);
        var result = await service.ExportPhonesToExcelAsync(10, "task-xlsx-mask");

        Assert.True(result.Success);
        using var stream = new MemoryStream(result.Data!.FileContent);
        using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);
        Assert.Equal("0912****567", sheet.Cell(2, 2).GetString());
        Assert.DoesNotContain("09121234567", sheet.Cell(2, 2).GetString());
    }

    [Fact]
    public async Task GetTaskStatus_MasksPhonesForRegularUser()
    {
        var repo = new InMemoryTaskRepository();
        repo.Tasks.Add(new NumberSeekerTask
        {
            UserId = 10,
            ScraperTaskId = "task-mask",
            Source = "divar",
            City = "تهران",
            Category = "x",
            TargetCount = 1,
            Status = "completed",
            PhonesJson = "[\"09121234567\"]",
            PhonesPersistedAt = DateTime.UtcNow
        });

        var service = BuildService(new FakeScraperClient { ThrowOnGetStatus = true }, repo, canViewPhones: false);
        var result = await service.GetTaskStatusAsync(10, "task-mask");

        Assert.True(result.Success);
        Assert.True(result.Data!.IsPhonesMasked);
        Assert.False(result.Data.CanViewPhones);
        Assert.Equal("0912****567", Assert.Single(result.Data.Phones));
        Assert.Equal("0912****567", Assert.Single(result.Data.PhonesPreview));
    }

    [Fact]
    public async Task GetTaskStatus_RevealsPhonesForPrivilegedUser()
    {
        var repo = new InMemoryTaskRepository();
        repo.Tasks.Add(new NumberSeekerTask
        {
            UserId = 10,
            ScraperTaskId = "task-reveal",
            Source = "divar",
            City = "تهران",
            Category = "x",
            TargetCount = 1,
            Status = "completed",
            PhonesJson = "[\"09121234567\"]",
            PhonesPersistedAt = DateTime.UtcNow
        });

        var service = BuildService(new FakeScraperClient { ThrowOnGetStatus = true }, repo, canViewPhones: true);
        var result = await service.GetTaskStatusAsync(10, "task-reveal");

        Assert.True(result.Success);
        Assert.False(result.Data!.IsPhonesMasked);
        Assert.True(result.Data.CanViewPhones);
        Assert.Equal("09121234567", Assert.Single(result.Data.Phones));
    }

    [Fact]
    public async Task ExportPhones_MasksJsonForRegularUser()
    {
        var repo = new InMemoryTaskRepository();
        repo.Tasks.Add(new NumberSeekerTask
        {
            UserId = 10,
            ScraperTaskId = "task-export-mask",
            Source = "divar",
            City = "تهران",
            Category = "x",
            TargetCount = 1,
            Status = "completed",
            PhonesJson = "[\"09121234567\"]",
            PhonesPersistedAt = DateTime.UtcNow
        });

        var service = BuildService(new FakeScraperClient(), repo, canViewPhones: false);
        var result = await service.ExportPhonesAsync(10, "task-export-mask");

        Assert.True(result.Success);
        Assert.True(result.Data!.IsPhonesMasked);
        Assert.Equal("0912****567", Assert.Single(result.Data.Phones));
        Assert.Contains("0912****567", result.Data.TextContent);
        Assert.DoesNotContain("09121234567", result.Data.TextContent);
    }

    [Fact]
    public async Task ImportPhones_MarksContactsAsHidden()
    {
        var contactService = new FakeContactService();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var rateLimiter = new NumberSeekerRateLimiter(
            cache,
            Options.Create(new NumberSeekerOptions { MaxScrapesPerHour = 100, MaxImportsPerHour = 100 }));
        var repo = new InMemoryTaskRepository();
        repo.Tasks.Add(new NumberSeekerTask
        {
            UserId = 10,
            ScraperTaskId = "task-import-hide",
            Source = "divar",
            City = "تهران",
            Category = "رستوران",
            TargetCount = 1,
            Status = "completed",
            PhonesJson = "[\"09121234567\"]",
            PhonesPersistedAt = DateTime.UtcNow
        });

        var service = new NumberSeekerService(
            new FakeScraperClient { ThrowOnGetStatus = true },
            repo,
            contactService,
            rateLimiter,
            new FakePhoneAccess(),
            Options.Create(new NumberSeekerOptions()),
            new Api_Vapp.Tests.Shared.NoOpAuditService(),
            NullLogger<NumberSeekerService>.Instance);

        var result = await service.ImportPhonesAsync(10, "task-import-hide", new ImportNumberSeekerPhonesDto
        {
            ContactNotebookId = 1,
            ContactNamePrefix = "رستوران"
        });

        Assert.True(result.Success);
        Assert.NotNull(contactService.LastImport);
        Assert.All(contactService.LastImport!.Contacts, item => Assert.True(item.HideMobileNumber));
        Assert.Equal("09121234567", contactService.LastImport.Contacts[0].MobileNumber);
    }

    [Fact]
    public void JsonAlwaysDoubleConverter_WritesFractionalToken()
    {
        var options = new System.Text.Json.JsonSerializerOptions();
        options.Converters.Add(new JsonAlwaysDoubleConverter());
        var json = System.Text.Json.JsonSerializer.Serialize(100.0, options);
        Assert.Equal("100.0", json);
        var json2 = System.Text.Json.JsonSerializer.Serialize(33.333, options);
        Assert.Equal("33.3", json2);
    }

    [Fact]
    public async Task HandleWebhook_UpdatesTrackedTask()
    {
        var client = new FakeScraperClient();
        var repo = new InMemoryTaskRepository();
        repo.Tasks.Add(new NumberSeekerTask
        {
            UserId = 10,
            ScraperTaskId = "task-wh",
            Source = "divar",
            City = "تهران",
            Category = "x",
            TargetCount = 5,
            Status = "running"
        });

        var service = BuildService(client, repo);
        var result = await service.HandleWebhookAsync(new NumberSeekerWebhookDto
        {
            TaskId = "task-wh",
            Status = "completed",
            CurrentCount = 2,
            ResultCode = "success",
            Phones = new List<string> { "09121111111", "09122222222" }
        });

        Assert.True(result.Success);
        Assert.Equal("completed", repo.Tasks[0].Status);
        Assert.NotNull(repo.Tasks[0].CompletedAt);
        Assert.False(string.IsNullOrWhiteSpace(repo.Tasks[0].PhonesJson));
        Assert.NotNull(repo.Tasks[0].PhonesPersistedAt);
    }

    [Fact]
    public async Task GetTaskStatus_UsesCachedPhonesWithoutScraperCall()
    {
        var client = new FakeScraperClient { ThrowOnGetStatus = true };
        var repo = new InMemoryTaskRepository();
        repo.Tasks.Add(new NumberSeekerTask
        {
            UserId = 10,
            ScraperTaskId = "task-cache",
            Source = "divar",
            City = "تهران",
            Category = "x",
            TargetCount = 2,
            Status = "completed",
            PhonesJson = "[\"09121111111\",\"09122222222\"]",
            PhonesPersistedAt = DateTime.UtcNow
        });

        var service = BuildService(client, repo);
        var result = await service.GetTaskStatusAsync(10, "task-cache");

        Assert.True(result.Success);
        Assert.Equal(2, result.Data?.Phones.Count);
    }

    [Fact]
    public async Task ImportPhones_UsesPersistedPhonesWhenScraperUnavailable()
    {
        var client = new FakeScraperClient { ThrowOnGetStatus = true };
        var repo = new InMemoryTaskRepository();
        repo.Tasks.Add(new NumberSeekerTask
        {
            UserId = 10,
            ScraperTaskId = "task-import-cache",
            Source = "divar",
            City = "تهران",
            Category = "رستوران",
            TargetCount = 1,
            Status = "completed",
            PhonesJson = "[\"09121234567\"]",
            PhonesPersistedAt = DateTime.UtcNow
        });

        var service = BuildService(client, repo);
        var result = await service.ImportPhonesAsync(10, "task-import-cache", new ImportNumberSeekerPhonesDto
        {
            ContactNotebookId = 1,
            ContactNamePrefix = "رستوران"
        });

        Assert.True(result.Success);
        Assert.Equal(1, result.Data?.SuccessCount);
    }

    [Fact]
    public void UserMessages_HideTechnicalDetails()
    {
        Assert.Contains("استخراج", NumberSeekerUserMessages.ForTaskStatus("failed", "platform_error", 0));
        Assert.Contains("پشتیبانی", NumberSeekerUserMessages.ForTaskStatus("failed", "token_expired", 0));
        Assert.Contains("شهر یا دسته", NumberSeekerUserMessages.ForTaskStatus("failed", "no_listings", 0));
        Assert.DoesNotContain("پشتیبانی", NumberSeekerUserMessages.ForTaskStatus("failed", "no_listings", 0));
        Assert.Contains("لغو", NumberSeekerUserMessages.ForTaskStatus("cancelled", "cancelled", 0));
        Assert.Contains("شماره دریافت", NumberSeekerUserMessages.ForTaskStatus("completed", "db_unavailable", 3));
        Assert.Equal(
            NumberSeekerUserMessages.ExtractionFailed,
            NumberSeekerUserMessages.SanitizeIncomingUserMessage("SqlException at ODBC Driver", NumberSeekerUserMessages.ExtractionFailed));
    }

    [Fact]
    public async Task GetRecentTasks_IncludesUiFields()
    {
        var repo = new InMemoryTaskRepository();
        repo.Tasks.Add(new NumberSeekerTask
        {
            UserId = 10,
            ScraperTaskId = "task-ui",
            Source = "divar",
            City = "تهران",
            Category = "رستوران",
            TargetCount = 82,
            CurrentCount = 82,
            Status = "completed",
            PhonesJson = "[\"09121111111\"]",
            CreatedAt = DateTime.UtcNow
        });

        var service = BuildService(new FakeScraperClient(), repo);
        var result = await service.GetRecentTasksAsync(10);

        Assert.True(result.Success);
        var item = Assert.Single(result.Data!.Tasks);
        Assert.Equal("دیوار", item.SourceDisplayName);
        Assert.Equal("تکمیل شد", item.StatusDisplayName);
        Assert.Equal("success", item.StatusTone);
        Assert.Equal("تهران - رستوران", item.Subtitle);
        Assert.Equal("82/82", item.CountLabel);
        Assert.False(string.IsNullOrWhiteSpace(item.CreatedAtPersian));
        Assert.True(item.CanDownload);
    }

    [Fact]
    public async Task ImportPhones_SucceedsForCompletedTask()
    {
        var client = new FakeScraperClient();
        var repo = new InMemoryTaskRepository();
        repo.Tasks.Add(new NumberSeekerTask
        {
            UserId = 10,
            ScraperTaskId = "task-import",
            Source = "divar",
            City = "تهران",
            Category = "رستوران",
            TargetCount = 1,
            Status = "completed"
        });

        var service = BuildService(client, repo);
        var result = await service.ImportPhonesAsync(10, "task-import", new ImportNumberSeekerPhonesDto
        {
            ContactNotebookId = 1,
            ContactNamePrefix = "رستوران"
        });

        Assert.True(result.Success);
        Assert.Equal(1, result.Data?.SuccessCount);
        Assert.NotNull(repo.Tasks[0].ImportedAt);
    }

    private static NumberSeekerService BuildService(
        INumberScraperClient client,
        INumberSeekerTaskRepository repo)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var rateLimiter = new NumberSeekerRateLimiter(
            cache,
            Options.Create(new NumberSeekerOptions { MaxScrapesPerHour = 100, MaxImportsPerHour = 100 }));

        return new NumberSeekerService(
            client,
            repo,
            new FakeContactService(),
            rateLimiter,
            new FakePhoneAccess(),
            Options.Create(new NumberSeekerOptions()),
            new Api_Vapp.Tests.Shared.NoOpAuditService(),
            NullLogger<NumberSeekerService>.Instance);
    }

    private static NumberSeekerService BuildService(
        INumberScraperClient client,
        INumberSeekerTaskRepository repo,
        bool canViewPhones)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var rateLimiter = new NumberSeekerRateLimiter(
            cache,
            Options.Create(new NumberSeekerOptions { MaxScrapesPerHour = 100, MaxImportsPerHour = 100 }));

        return new NumberSeekerService(
            client,
            repo,
            new FakeContactService(),
            rateLimiter,
            new FakePhoneAccess { CanView = canViewPhones },
            Options.Create(new NumberSeekerOptions()),
            new Api_Vapp.Tests.Shared.NoOpAuditService(),
            NullLogger<NumberSeekerService>.Instance);
    }

    private sealed class FakeScraperClient : INumberScraperClient
    {
        public bool IsEnabled => true;
        public bool ThrowOnGetStatus { get; set; }

        public Task<NumberSeekerTaskCreatedDto> StartScrapeAsync(
            StartNumberSeekerScrapeDto request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new NumberSeekerTaskCreatedDto
            {
                TaskId = "task-new",
                Source = request.Source,
                Status = "pending",
                Message = "ok"
            });
        }

        public Task<NumberSeekerTaskStatusDto> GetTaskStatusAsync(
            string taskId,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnGetStatus)
                throw new HttpRequestException("scraper down");

            return Task.FromResult(new NumberSeekerTaskStatusDto
            {
                TaskId = taskId,
                Status = "completed",
                Phones = new List<string> { "09121234567" },
                CurrentCount = 1,
                TargetCount = 1,
                ProgressPercent = 100
            });
        }

        public Task<NumberSeekerCancelResultDto> CancelTaskAsync(
            string taskId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new NumberSeekerCancelResultDto { TaskId = taskId, Message = "cancelled" });

        public Task<NumberSeekerHealthDto> GetHealthAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new NumberSeekerHealthDto
            {
                Status = "healthy",
                ScraperReachable = true,
                ApiKeyValid = true,
                IntegrationReady = true
            });

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<ScraperPlatformTokenListRaw> GetPlatformTokensAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ScraperPlatformTokenListRaw());

        public Task<ScraperTokenAlertsRaw> GetPlatformTokenAlertsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ScraperTokenAlertsRaw());

        public Task<ScraperTokenSavedRaw> SaveDivarTokenAsync(
            string token,
            string? refreshToken,
            string? frontToken,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ScraperTokenSavedRaw { Platform = "divar", Message = "ok" });

        public Task<ScraperTokenSavedRaw> SaveSheypoorTokenAsync(
            string accessToken,
            string? refreshToken,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ScraperTokenSavedRaw { Platform = "sheypoor", Message = "ok" });

        public Task<ScraperTokenMaintenanceRaw> RunTokenMaintenanceAsync(
            bool forceSheypoorRefresh = false,
            bool forceDivarRefresh = false,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ScraperTokenMaintenanceRaw());
    }

    private sealed class InMemoryTaskRepository : INumberSeekerTaskRepository
    {
        public List<NumberSeekerTask> Tasks { get; } = new();

        public Task<NumberSeekerTask> AddAsync(NumberSeekerTask task)
        {
            task.Id = Tasks.Count + 1;
            Tasks.Add(task);
            return Task.FromResult(task);
        }

        public Task<NumberSeekerTask?> GetByScraperTaskIdAsync(string scraperTaskId)
            => Task.FromResult(Tasks.FirstOrDefault(t => t.ScraperTaskId == scraperTaskId));

        public Task<NumberSeekerTask?> GetByScraperTaskIdTrackedAsync(string scraperTaskId)
            => GetByScraperTaskIdAsync(scraperTaskId);

        public Task<NumberSeekerTask?> GetByScraperTaskIdAndUserIdAsync(string scraperTaskId, int userId)
            => Task.FromResult(Tasks.FirstOrDefault(t => t.ScraperTaskId == scraperTaskId && t.UserId == userId));

        public Task UpdateAsync(NumberSeekerTask task) => Task.CompletedTask;

        public Task<List<NumberSeekerTask>> GetRecentByUserIdAsync(int userId, int limit = 20)
            => Task.FromResult(Tasks.Where(t => t.UserId == userId).Take(limit).ToList());
    }

    private sealed class FakePhoneAccess : INumberSeekerPhoneAccessService
    {
        public bool CanView { get; set; }

        public Task<bool> CanViewPhonesAsync(int userId, CancellationToken cancellationToken = default)
            => Task.FromResult(CanView);

        public Task<HashSet<string>> GetHiddenMobileNumbersAsync(int userId, CancellationToken cancellationToken = default)
            => Task.FromResult(new HashSet<string>(StringComparer.Ordinal));
    }

    private sealed class FakeContactService : IContactService
    {
        public ImportContactsFromListDto? LastImport { get; private set; }

        public Task<ApiResponse<ImportExcelResultDto>> ImportFromListAsync(
            int userId,
            ImportContactsFromListDto importDto)
        {
            LastImport = importDto;
            return Task.FromResult(ApiResponse<ImportExcelResultDto>.CreateSuccess(
                new ImportExcelResultDto
                {
                    TotalRows = importDto.Contacts.Count,
                    SuccessCount = importDto.Contacts.Count
                }));
        }

        public Task<ApiResponse<ContactResponseDto>> CreateContactAsync(int userId, CreateContactDto createDto)
            => NotImplemented<ContactResponseDto>();

        public Task<ApiResponse<ContactResponseDto>> GetContactByIdAsync(int id, int userId)
            => NotImplemented<ContactResponseDto>();

        public Task<ApiResponse<ContactListResponseDto>> GetContactsAsync(int notebookId, int userId, int pageNumber = 1, int pageSize = 10, string? searchTerm = null)
            => NotImplemented<ContactListResponseDto>();

        public Task<ApiResponse<ContactResponseDto>> UpdateContactAsync(int id, int userId, UpdateContactDto updateDto)
            => NotImplemented<ContactResponseDto>();

        public Task<ApiResponse<bool>> DeleteContactAsync(int id, int userId)
            => NotImplemented<bool>();

        public Task<ApiResponse<bool>> TransferContactAsync(int contactId, int fromNotebookId, int toNotebookId, int userId)
            => NotImplemented<bool>();

        public Task<ApiResponse<ImportExcelResultDto>> ImportFromExcelAsync(int userId, ImportContactsFromExcelDto importDto)
            => NotImplemented<ImportExcelResultDto>();

        public Task<ApiResponse<ExportExcelResultDto>> GetImportExcelTemplateAsync()
            => NotImplemented<ExportExcelResultDto>();

        public Task<ApiResponse<ExportExcelResultDto>> ExportToExcelAsync(int notebookId, int userId, int pageNumber = 1, int pageSize = 10)
            => NotImplemented<ExportExcelResultDto>();

        public Task<ApiResponse<string>> UploadProfileImageAsync(int contactId, int userId, IFormFile imageFile)
            => NotImplemented<string>();

        public Task<ApiResponse<string>> UploadProfileImageAsync(int contactId, IFormFile imageFile)
            => NotImplemented<string>();

        public Task<ApiResponse<bool>> DeleteProfileImageAsync(int contactId, int userId)
            => NotImplemented<bool>();

        public Task<ApiResponse<List<string>>> UploadAttachmentFilesAsync(int contactId, int userId, List<IFormFile> files)
            => NotImplemented<List<string>>();

        public Task<ApiResponse<bool>> DeleteAttachmentFileAsync(int contactId, int userId, string filePath)
            => NotImplemented<bool>();

        public Task<ApiResponse<List<string>>> GetAttachmentFilesAsync(int contactId, int userId)
            => NotImplemented<List<string>>();

        public Task<ApiResponse<ContactListResponseDto>> GetAllContactsAsync(int pageNumber = 1, int pageSize = 10, string? searchTerm = null)
            => NotImplemented<ContactListResponseDto>();

        public Task<ApiResponse<ContactListResponseDto>> GetMyContactsAsync(int userId, int pageNumber = 1, int pageSize = 10, string? searchTerm = null)
            => NotImplemented<ContactListResponseDto>();

        public Task<ApiResponse<bool>> AssignTagsToContactAsync(int contactId, int userId, AssignTagsToContactDto assignDto)
            => NotImplemented<bool>();

        public Task<ApiResponse<List<ContactNotebookResponseDto>>> GetUserNotebooksAsync(int userId)
            => NotImplemented<List<ContactNotebookResponseDto>>();

        private static Task<ApiResponse<T>> NotImplemented<T>()
            => throw new NotImplementedException();
    }
}
