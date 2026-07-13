using Api_Vapp.Configuration;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Contact;
using Api_Vapp.DTOs.NumberSeeker;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services;
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
            CurrentCount = 5,
            ResultCode = "success"
        });

        Assert.True(result.Success);
        Assert.Equal("completed", repo.Tasks[0].Status);
        Assert.NotNull(repo.Tasks[0].CompletedAt);
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
            Options.Create(new NumberSeekerOptions()),
            NullLogger<NumberSeekerService>.Instance);
    }

    private sealed class FakeScraperClient : INumberScraperClient
    {
        public bool IsEnabled => true;

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
            => Task.FromResult(new NumberSeekerHealthDto { Status = "healthy", ScraperReachable = true });

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);
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

    private sealed class FakeContactService : IContactService
    {
        public Task<ApiResponse<ImportExcelResultDto>> ImportFromListAsync(
            int userId,
            ImportContactsFromListDto importDto)
        {
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
