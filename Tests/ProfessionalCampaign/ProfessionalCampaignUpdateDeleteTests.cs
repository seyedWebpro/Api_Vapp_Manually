using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Message;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Repositories;
using Api_Vapp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api_Vapp.Tests.ProfessionalCampaign;

public class ProfessionalCampaignUpdateDeleteTests
{
    private static readonly SemaphoreSlim MigrationLock = new(1, 1);
    private static bool _migrationsApplied;

    [Fact]
    public async Task Update_BeforeSend_ReplacesStepsAndRecipients()
    {
        await using var context = await CreateContextAsync();
        var (userId, notebookId) = await SeedUserWithNotebookAsync(context);
        var service = CreateService(context);

        var created = await service.CreateAsync(userId, BuildCreateDto(notebookId, "کمپین اولیه", "متن الف", "متن ب"));
        Assert.True(created.Success);
        var campaignId = created.Data!.Id;

        var updated = await service.UpdateAsync(userId, campaignId, new UpdateProfessionalCampaignDto
        {
            Title = "کمپین ویرایش‌شده",
            TargetType = "Notebooks",
            TargetIds = new List<int> { notebookId },
            Steps = new List<ProfessionalCampaignStepInputDto>
            {
                new() { Content = "متن جدید ۱" },
                new() { Content = "متن جدید ۲", DelayMinutes = 5 },
                new() { Content = "متن جدید ۳", DelayHours = 1 }
            }
        });

        Assert.True(updated.Success, updated.Message);
        Assert.Equal("کمپین ویرایش‌شده", updated.Data!.Title);
        Assert.Equal(3, updated.Data.Steps.Count);
        Assert.Equal("متن جدید ۱", updated.Data.Steps[0].Content);
        Assert.Equal(ProfessionalCampaignStatuses.PendingApproval, updated.Data.Status);

        Assert.Equal(3, await context.ProfessionalCampaignSteps.AsNoTracking()
            .CountAsync(s => s.ProfessionalCampaignId == campaignId));
        Assert.Equal(1, await context.ProfessionalCampaignRecipients.AsNoTracking()
            .CountAsync(r => r.ProfessionalCampaignId == campaignId));
    }

    [Fact]
    public async Task Update_WhenActive_ReturnsBadRequest()
    {
        await using var context = await CreateContextAsync();
        var (userId, notebookId) = await SeedUserWithNotebookAsync(context);
        var service = CreateService(context);

        var created = await service.CreateAsync(userId, BuildCreateDto(notebookId, "فعال", "الف", "ب"));
        var campaignId = created.Data!.Id;
        await ApproveAllStepsAsync(context, campaignId);
        var activate = await service.ActivateAsync(userId, campaignId);
        Assert.True(activate.Success, activate.Message);

        var updated = await service.UpdateAsync(userId, campaignId, BuildUpdateDto(notebookId, "نباید", "ج", "د"));
        Assert.False(updated.Success);
        Assert.Equal(400, updated.StatusCode);
        Assert.Contains("متوقف", updated.Message);
    }

    [Fact]
    public async Task Delete_SoftDeletes_AndHidesFromGet()
    {
        await using var context = await CreateContextAsync();
        var (userId, notebookId) = await SeedUserWithNotebookAsync(context);
        var service = CreateService(context);

        var created = await service.CreateAsync(userId, BuildCreateDto(notebookId, "حذف‌شو", "الف", "ب"));
        var campaignId = created.Data!.Id;

        var deleted = await service.DeleteAsync(userId, campaignId);
        Assert.True(deleted.Success, deleted.Message);

        var get = await service.GetByIdAsync(userId, campaignId);
        Assert.False(get.Success);
        Assert.Equal(404, get.StatusCode);

        var row = await context.ProfessionalCampaigns.AsNoTracking()
            .FirstAsync(c => c.Id == campaignId);
        Assert.True(row.IsDeleted);
        Assert.Equal(ProfessionalCampaignStatuses.Cancelled, row.Status);
    }

    [Fact]
    public async Task Delete_ForeignCampaign_ReturnsNotFound()
    {
        await using var context = await CreateContextAsync();
        var (ownerId, notebookId) = await SeedUserWithNotebookAsync(context);
        var other = new User
        {
            PhoneNumber = $"09{Guid.NewGuid():N}"[..11],
            PasswordHash = "x",
            FullName = "other",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(other);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var created = await service.CreateAsync(ownerId, BuildCreateDto(notebookId, "مالک", "الف", "ب"));
        var deleted = await service.DeleteAsync(other.Id, created.Data!.Id);
        Assert.False(deleted.Success);
        Assert.Equal(404, deleted.StatusCode);
    }

    private static ProfessionalCampaignService CreateService(Api_Context context) =>
        new(
            context,
            new ProfessionalCampaignRepository(context),
            new RecordingMessageService(),
            NullLogger<ProfessionalCampaignService>.Instance);

    private static CreateProfessionalCampaignDto BuildCreateDto(
        int notebookId,
        string title,
        string step1,
        string step2) => new()
    {
        Title = title,
        TargetType = "Notebooks",
        TargetIds = new List<int> { notebookId },
        Steps = new List<ProfessionalCampaignStepInputDto>
        {
            new() { Content = step1 },
            new() { Content = step2, DelayMinutes = 1 }
        }
    };

    private static UpdateProfessionalCampaignDto BuildUpdateDto(
        int notebookId,
        string title,
        string step1,
        string step2) => new()
    {
        Title = title,
        TargetType = "Notebooks",
        TargetIds = new List<int> { notebookId },
        Steps = new List<ProfessionalCampaignStepInputDto>
        {
            new() { Content = step1 },
            new() { Content = step2, DelayMinutes = 1 }
        }
    };

    private static async Task ApproveAllStepsAsync(Api_Context context, int campaignId)
    {
        var steps = await context.ProfessionalCampaignSteps
            .Where(s => s.ProfessionalCampaignId == campaignId && !s.IsDeleted)
            .ToListAsync();
        foreach (var step in steps)
        {
            step.ApprovalStatus = AdminApprovalStatuses.Approved;
            step.Status = ProfessionalCampaignStepStatuses.Pending;
            step.UpdatedAt = DateTime.UtcNow;
        }

        var campaign = await context.ProfessionalCampaigns.FirstAsync(c => c.Id == campaignId);
        campaign.Status = ProfessionalCampaignStatuses.Ready;
        campaign.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    private static async Task<(int UserId, int NotebookId)> SeedUserWithNotebookAsync(Api_Context context)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var phone = $"09{suffix}";
        if (phone.Length > 11) phone = phone[..11];

        var user = new User
        {
            PhoneNumber = phone,
            PasswordHash = "x",
            FullName = "pc-update-delete",
            WalletBalance = 50_000m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var notebook = new ContactNotebook
        {
            UserId = user.Id,
            Name = $"nb-{suffix}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.ContactNotebooks.Add(notebook);
        await context.SaveChangesAsync();

        context.Contacts.Add(new Contact
        {
            ContactNotebookId = notebook.Id,
            MobileNumber = "09121112233",
            FullName = "مخاطب",
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        return (user.Id, notebook.Id);
    }

    private static async Task<Api_Context> CreateContextAsync()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("VAPP_TEST_CONNECTION")
            ?? "Server=localhost,1436;Database=DbVapp_UserFormTests;User Id=sa;Password=Vapp@Secure2025!;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=true";

        var options = new DbContextOptionsBuilder<Api_Context>()
            .UseSqlServer(connectionString)
            .Options;

        var context = new Api_Context(options);
        await MigrationLock.WaitAsync();
        try
        {
            if (!_migrationsApplied)
            {
                await context.Database.MigrateAsync();
                _migrationsApplied = true;
            }
        }
        finally
        {
            MigrationLock.Release();
        }

        return context;
    }

    private sealed class RecordingMessageService : IMessageService
    {
        private static Task<ApiResponse<T>> NotUsed<T>() =>
            Task.FromResult(ApiResponse<T>.BadRequest("not used"));

        public Task<ApiResponse<DirectSendResultDto>> SendDirectMessageAsync(
            int userId, int messageId, SendDirectMessageDto sendDto, MessageSession? session = null, bool bypassAdminApproval = false)
            => NotUsed<DirectSendResultDto>();

        public Task<ApiResponse<MessageResponseDto>> CreateMessageAsync(int userId, CreateMessageDto createDto) => NotUsed<MessageResponseDto>();
        public Task<ApiResponse<MessageResponseDto>> GetMessageByIdAsync(int messageId, int userId) => NotUsed<MessageResponseDto>();
        public Task<ApiResponse<MessageListResponseDto>> GetMessagesAsync(int userId, int pageNumber = 1, int pageSize = 20, string? searchTerm = null) => NotUsed<MessageListResponseDto>();
        public Task<ApiResponse<MessageResponseDto>> UpdateMessageAsync(int messageId, int userId, UpdateMessageDto updateDto) => NotUsed<MessageResponseDto>();
        public Task<ApiResponse<bool>> DeleteMessageAsync(int messageId, int userId) => NotUsed<bool>();
        public Task<ApiResponse<CampaignSummaryDto>> GetCampaignSummaryAsync(int userId, int messageId) => NotUsed<CampaignSummaryDto>();
        public Task<ApiResponse<CampaignSummaryDto>> CalculateCampaignSummaryAsync(int userId, int messageId, CreateCampaignDto campaignDto, string? idempotencyKey = null) => NotUsed<CampaignSummaryDto>();
        public Task<ApiResponse<DirectSendResultDto>> ConfirmAndSendMessageAsync(int userId, int messageId, string? idempotencyKey = null) => NotUsed<DirectSendResultDto>();
        public Task<ApiResponse<CampaignResponseDto>> CreateCampaignAsync(int userId, CreateCampaignDto createDto) => NotUsed<CampaignResponseDto>();
        public Task<ApiResponse<CampaignResponseDto>> GetCampaignByIdAsync(int campaignId, int userId) => NotUsed<CampaignResponseDto>();
        public Task<ApiResponse<CampaignListResponseDto>> GetCampaignsAsync(int userId, int pageNumber = 1, int pageSize = 20, string? status = null) => NotUsed<CampaignListResponseDto>();
        public Task<ApiResponse<bool>> ConfirmAndSendCampaignAsync(int campaignId, int userId, bool bypassAdminApproval = false) => NotUsed<bool>();
        public Task<ApiResponse<bool>> CancelCampaignAsync(int campaignId, int userId) => NotUsed<bool>();
        public Task<ApiResponse<bool>> ToggleCampaignStatusAsync(int campaignId, int userId, bool isActive) => NotUsed<bool>();
        public Task<ApiResponse<TemplateResponseDto>> CreateTemplateAsync(int userId, CreateTemplateDto createDto) => NotUsed<TemplateResponseDto>();
        public Task<ApiResponse<List<TemplateResponseDto>>> GetTemplatesAsync(int userId) => NotUsed<List<TemplateResponseDto>>();
        public Task<ApiResponse<List<CategoryGroupDto>>> GetTemplatesGroupedByCategoryAsync(int userId) => NotUsed<List<CategoryGroupDto>>();
        public Task<ApiResponse<TemplateResponseDto>> UpdateTemplateAsync(int id, int userId, UpdateTemplateDto updateDto) => NotUsed<TemplateResponseDto>();
        public Task<ApiResponse<bool>> DeleteTemplateAsync(int id, int userId) => NotUsed<bool>();
        public Task<ApiResponse<TemplateResponseDto>> SetUserDefaultTemplateAsync(int userId, int templateId, bool isSelected = true) => NotUsed<TemplateResponseDto>();
        public Task<ApiResponse<List<TemplateResponseDto>>> GetQuickSendDefaultTemplatesAsync(int userId) => NotUsed<List<TemplateResponseDto>>();
        public Task<ApiResponse<List<TemplateResponseDto>>> SetQuickSendDefaultTemplatesAsync(int userId, IReadOnlyCollection<int> templateIds) => NotUsed<List<TemplateResponseDto>>();
        public Task<ApiResponse<TemplateGroupResponseDto>> CreateTemplateGroupAsync(int userId, CreateTemplateGroupDto createDto) => NotUsed<TemplateGroupResponseDto>();
        public Task<ApiResponse<List<TemplateGroupSummaryDto>>> GetTemplateGroupsAsync(int userId) => NotUsed<List<TemplateGroupSummaryDto>>();
        public Task<ApiResponse<TemplateGroupResponseDto>> GetTemplateGroupByIdAsync(int id, int userId) => NotUsed<TemplateGroupResponseDto>();
        public Task<ApiResponse<List<TemplateResponseDto>>> GetTemplatesByGroupIdAsync(int groupId, int userId) => NotUsed<List<TemplateResponseDto>>();
        public Task<ApiResponse<TemplateGroupResponseDto>> UpdateTemplateGroupAsync(int id, int userId, UpdateTemplateGroupDto updateDto) => NotUsed<TemplateGroupResponseDto>();
        public Task<ApiResponse<bool>> DeleteTemplateGroupAsync(int id, int userId) => NotUsed<bool>();
        public Task<ApiResponse<RecipientListResponseDto>> SelectRecipientsAsync(int userId, SelectRecipientsDto selectDto) => NotUsed<RecipientListResponseDto>();
        public Task<ApiResponse<DirectSendResultDto>> QuickSendMessageAsync(int userId, QuickSendMessageDto quickSendDto) => NotUsed<DirectSendResultDto>();
        public Task<ApiResponse<TodayReportDto>> GetTodayReportAsync(int userId) => NotUsed<TodayReportDto>();
        public Task<ApiResponse<List<LatestCampaignsDto>>> GetLatestCampaignsAsync(int userId, int count = 5) => NotUsed<List<LatestCampaignsDto>>();
        public Task<ApiResponse<ComprehensiveReportDto>> GetComprehensiveReportAsync(int userId) => NotUsed<ComprehensiveReportDto>();
        public Task<ApiResponse<MessagePreviewDto>> GetMessagePreviewAsync(int messageId, int userId) => NotUsed<MessagePreviewDto>();
        public Task<ApiResponse<PersonalizedMessageResponseDto>> PersonalizeMessageAsync(int messageId, int userId, Dictionary<string, string> placeholders, bool saveToMessage = true) => NotUsed<PersonalizedMessageResponseDto>();
        public Task<ApiResponse<MessageTagResponseDto>> CreateTagAsync(int userId, CreateMessageTagDto createDto) => NotUsed<MessageTagResponseDto>();
        public Task<ApiResponse<MessageTagListResponseDto>> GetTagsAsync(int userId, int pageNumber = 1, int pageSize = 20) => NotUsed<MessageTagListResponseDto>();
        public Task<ApiResponse<MessageTagWithContactCountListResponseDto>> GetTagsWithContactCountAsync(int userId, int pageNumber = 1, int pageSize = 20) => NotUsed<MessageTagWithContactCountListResponseDto>();
    }
}
