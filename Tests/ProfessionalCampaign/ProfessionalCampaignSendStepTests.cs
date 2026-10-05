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

/// <summary>
/// اطمینان از اینکه ProcessDueSteps بدون تراکنش محیطی دور SendDirect کار می‌کند
/// و مرحله بعد از ارسال موفق Sent می‌شود.
/// </summary>
public class ProfessionalCampaignSendStepTests
{
    private static readonly SemaphoreSlim MigrationLock = new(1, 1);
    private static bool _migrationsApplied;

    [Fact]
    public async Task ProcessDueSteps_SendsPendingStep_WithoutAmbientTransactionAroundSms()
    {
        await using var context = await CreateContextAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var user = new User
        {
            PhoneNumber = $"09{suffix}02",
            PasswordHash = "x",
            FullName = "pc-send-step",
            WalletBalance = 50_000m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var campaign = new Models.ProfessionalCampaign
        {
            UserId = user.Id,
            Title = "کمپین تست ارسال",
            TargetType = ProfessionalCampaignTargetTypes.Notebooks,
            TargetIdsJson = "[1]",
            Status = ProfessionalCampaignStatuses.Active,
            StartAtUtc = DateTime.UtcNow.AddMinutes(-1),
            RecipientsCount = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.ProfessionalCampaigns.Add(campaign);
        await context.SaveChangesAsync();

        context.ProfessionalCampaignRecipients.Add(new ProfessionalCampaignRecipient
        {
            ProfessionalCampaignId = campaign.Id,
            MobileNumber = "09121234567",
            FullName = "گیرنده تست",
            CreatedAt = DateTime.UtcNow
        });

        var step = new ProfessionalCampaignStep
        {
            ProfessionalCampaignId = campaign.Id,
            StepOrder = 1,
            Content = "سلام تست",
            DelayAfterPreviousMinutes = 0,
            Status = ProfessionalCampaignStepStatuses.Pending,
            ApprovalStatus = AdminApprovalStatuses.Approved,
            ScheduledAtUtc = DateTime.UtcNow.AddMinutes(-1),
            CreatedAt = DateTime.UtcNow
        };
        context.ProfessionalCampaignSteps.Add(step);
        await context.SaveChangesAsync();

        var messageService = new RecordingMessageService();
        var service = new ProfessionalCampaignService(
            context,
            new ProfessionalCampaignRepository(context),
            messageService,
            NullLogger<ProfessionalCampaignService>.Instance);

        await service.ProcessDueStepsAsync(CancellationToken.None);

        // DB اشتراکی ممکن است مراحل due دیگر هم داشته باشد؛ فقط نتیجهٔ همین مرحله مهم است.
        Assert.True(messageService.CallCount >= 1);
        Assert.True(messageService.LastBypassAdminApproval);
        Assert.NotNull(messageService.LastSession);
        Assert.Null(context.Database.CurrentTransaction);

        var updated = await context.ProfessionalCampaignSteps.AsNoTracking()
            .FirstAsync(s => s.Id == step.Id);
        Assert.Equal(ProfessionalCampaignStepStatuses.Sent, updated.Status);
        Assert.Equal(1, updated.SentCount);
        Assert.NotNull(updated.SentAtUtc);

        var updatedCampaign = await context.ProfessionalCampaigns.AsNoTracking()
            .FirstAsync(c => c.Id == campaign.Id);
        Assert.Equal(ProfessionalCampaignStatuses.Completed, updatedCampaign.Status);
        Assert.False(updatedCampaign.IsActive);
    }

    [Fact]
    public async Task ProcessDueSteps_RecoversStaleProcessingStep_AsFailed()
    {
        await using var context = await CreateContextAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var user = new User
        {
            PhoneNumber = $"09{suffix}03",
            PasswordHash = "x",
            FullName = "pc-stale-step",
            WalletBalance = 50_000m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var campaign = new Models.ProfessionalCampaign
        {
            UserId = user.Id,
            Title = "کمپین تست stale",
            TargetType = ProfessionalCampaignTargetTypes.Notebooks,
            TargetIdsJson = "[1]",
            Status = ProfessionalCampaignStatuses.Active,
            StartAtUtc = DateTime.UtcNow.AddMinutes(-10),
            RecipientsCount = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.ProfessionalCampaigns.Add(campaign);
        await context.SaveChangesAsync();

        context.ProfessionalCampaignRecipients.Add(new ProfessionalCampaignRecipient
        {
            ProfessionalCampaignId = campaign.Id,
            MobileNumber = "09129876543",
            FullName = "گیرنده",
            CreatedAt = DateTime.UtcNow
        });

        var step = new ProfessionalCampaignStep
        {
            ProfessionalCampaignId = campaign.Id,
            StepOrder = 1,
            Content = "گیر کرده",
            DelayAfterPreviousMinutes = 0,
            Status = ProfessionalCampaignStepStatuses.Processing,
            ApprovalStatus = AdminApprovalStatuses.Approved,
            ScheduledAtUtc = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-5),
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        context.ProfessionalCampaignSteps.Add(step);
        await context.SaveChangesAsync();

        var messageService = new RecordingMessageService();
        var service = new ProfessionalCampaignService(
            context,
            new ProfessionalCampaignRepository(context),
            messageService,
            NullLogger<ProfessionalCampaignService>.Instance);

        await service.ProcessDueStepsAsync(CancellationToken.None);

        // در DB اشتراکی ممکن است مراحل due دیگر هم ارسال شوند؛ این تست فقط بازیابی stale را می‌سنجد.
        var updated = await context.ProfessionalCampaignSteps.AsNoTracking()
            .FirstAsync(s => s.Id == step.Id);
        Assert.Equal(ProfessionalCampaignStepStatuses.Failed, updated.Status);
        Assert.Contains("ناتمام", updated.LastError);

        var updatedCampaign = await context.ProfessionalCampaigns.AsNoTracking()
            .FirstAsync(c => c.Id == campaign.Id);
        Assert.Equal(ProfessionalCampaignStatuses.Paused, updatedCampaign.Status);
        Assert.False(updatedCampaign.IsActive);
    }

    private sealed class RecordingMessageService : IMessageService
    {
        public int CallCount { get; private set; }
        public bool LastBypassAdminApproval { get; private set; }
        public MessageSession? LastSession { get; private set; }

        public Task<ApiResponse<DirectSendResultDto>> SendDirectMessageAsync(
            int userId,
            int messageId,
            SendDirectMessageDto sendDto,
            MessageSession? session = null,
            bool bypassAdminApproval = false)
        {
            CallCount++;
            LastBypassAdminApproval = bypassAdminApproval;
            LastSession = session;
            return Task.FromResult(ApiResponse<DirectSendResultDto>.CreateSuccess(new DirectSendResultDto
            {
                SentCount = 1,
                FailedCount = 0,
                TotalCost = 160
            }));
        }

        private static Task<ApiResponse<T>> NotUsed<T>() =>
            Task.FromResult(ApiResponse<T>.BadRequest("not used in professional campaign send tests"));

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
}
