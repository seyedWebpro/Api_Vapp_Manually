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

public class ProfessionalCampaignQuickSendTests
{
    private static readonly SemaphoreSlim MigrationLock = new(1, 1);
    private static bool _migrationsApplied;

    [Fact]
    public async Task GetQuickSendOptions_ReturnsOnlyReadyOrActiveApprovedTemplates()
    {
        await using var context = await CreateContextAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = await SeedUserAsync(context, suffix);

        var ready = await SeedTemplateCampaignAsync(
            context,
            user.Id,
            "آماده",
            ProfessionalCampaignStatuses.Ready,
            approved: true);
        await SeedTemplateCampaignAsync(
            context,
            user.Id,
            "در انتظار",
            ProfessionalCampaignStatuses.PendingApproval,
            approved: false);
        await SeedEnrollmentCampaignAsync(context, user.Id, "اجرای سریع");

        var service = CreateService(context, new RecordingMessageService());
        var result = await service.GetQuickSendOptionsAsync(user.Id, 1, 20);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal(ready.Id, result.Data.Items[0].Id);
        Assert.DoesNotContain(
            result.Data.Items,
            item => item.TargetType == ProfessionalCampaignTargetTypes.QuickSend);
    }

    [Fact]
    public async Task QuickSend_CreatesEnrollment_SendsFirstStep_AndSchedulesNextFromNow()
    {
        await using var context = await CreateContextAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = await SeedUserAsync(context, suffix);
        var contact = await SeedContactAsync(context, user.Id, suffix);

        var template = await SeedTemplateCampaignAsync(
            context,
            user.Id,
            "کمپین فروش",
            ProfessionalCampaignStatuses.Active,
            approved: true,
            secondDelayMinutes: 24 * 60);

        var messageService = new RecordingMessageService();
        var service = CreateService(context, messageService);

        var before = DateTime.UtcNow;
        var result = await service.QuickSendAsync(
            user.Id,
            template.Id,
            new QuickSendProfessionalCampaignDto { ContactId = contact.Id });
        var after = DateTime.UtcNow;

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.SentCount);
        Assert.Equal(1, messageService.CallCount);
        Assert.True(messageService.LastBypassAdminApproval);

        var enrollment = await context.ProfessionalCampaigns.AsNoTracking()
            .Include(c => c.Steps.Where(s => !s.IsDeleted))
            .Include(c => c.Recipients.Where(r => !r.IsDeleted))
            .SingleAsync(c =>
                c.UserId == user.Id
                && c.TargetType == ProfessionalCampaignTargetTypes.QuickSend
                && !c.IsDeleted);

        Assert.Equal(template.Title, enrollment.Title);
        Assert.Equal(ProfessionalCampaignStatuses.Active, enrollment.Status);
        Assert.True(enrollment.IsActive);
        Assert.Single(enrollment.Recipients);
        Assert.Equal(contact.Id, enrollment.Recipients.First().ContactId);
        Assert.Equal(contact.MobileNumber, enrollment.Recipients.First().MobileNumber);

        var steps = enrollment.Steps.OrderBy(s => s.StepOrder).ToList();
        Assert.Equal(2, steps.Count);
        Assert.Equal(ProfessionalCampaignStepStatuses.Sent, steps[0].Status);
        Assert.Equal(ProfessionalCampaignStepStatuses.Pending, steps[1].Status);
        Assert.NotNull(steps[0].SentAtUtc);
        Assert.NotNull(steps[1].ScheduledAtUtc);
        Assert.True(steps[1].ScheduledAtUtc >= before.AddDays(1).AddMinutes(-1));
        Assert.True(steps[1].ScheduledAtUtc <= after.AddDays(1).AddMinutes(1));

        // قالب اصلی دست‌نخورده می‌ماند
        var original = await context.ProfessionalCampaigns.AsNoTracking()
            .FirstAsync(c => c.Id == template.Id);
        Assert.Equal(ProfessionalCampaignStatuses.Active, original.Status);
    }

    [Fact]
    public async Task QuickSend_RejectsUnapprovedTemplate()
    {
        await using var context = await CreateContextAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = await SeedUserAsync(context, suffix);
        var contact = await SeedContactAsync(context, user.Id, suffix);

        var template = await SeedTemplateCampaignAsync(
            context,
            user.Id,
            "تأییدنشده",
            ProfessionalCampaignStatuses.PendingApproval,
            approved: false);

        var service = CreateService(context, new RecordingMessageService());
        var result = await service.QuickSendAsync(
            user.Id,
            template.Id,
            new QuickSendProfessionalCampaignDto { ContactId = contact.Id });

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(0, context.ProfessionalCampaigns.Count(c =>
            c.UserId == user.Id
            && c.TargetType == ProfessionalCampaignTargetTypes.QuickSend));
    }

    [Fact]
    public async Task QuickSend_RejectsForeignContact()
    {
        await using var context = await CreateContextAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var owner = await SeedUserAsync(context, suffix + "a");
        var other = await SeedUserAsync(context, suffix + "b");
        var foreignContact = await SeedContactAsync(context, other.Id, suffix + "c");

        var template = await SeedTemplateCampaignAsync(
            context,
            owner.Id,
            "مالک",
            ProfessionalCampaignStatuses.Ready,
            approved: true);

        var service = CreateService(context, new RecordingMessageService());
        var result = await service.QuickSendAsync(
            owner.Id,
            template.Id,
            new QuickSendProfessionalCampaignDto { ContactId = foreignContact.Id });

        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
    }

    private static ProfessionalCampaignService CreateService(
        Api_Context context,
        IMessageService messageService) =>
        new(
            context,
            new ProfessionalCampaignRepository(context),
            messageService,
            NullLogger<ProfessionalCampaignService>.Instance);

    private static async Task<User> SeedUserAsync(Api_Context context, string suffix)
    {
        var user = new User
        {
            PhoneNumber = $"09{suffix}01".Length <= 11
                ? $"09{suffix}01"[..11]
                : $"09{Guid.NewGuid().ToString("N")[..9]}",
            PasswordHash = "x",
            FullName = "pc-quick-send",
            WalletBalance = 50_000m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<Contact> SeedContactAsync(Api_Context context, int userId, string suffix)
    {
        var notebook = new ContactNotebook
        {
            UserId = userId,
            Name = $"دفترچه-{suffix}",
            CreatedAt = DateTime.UtcNow
        };
        context.ContactNotebooks.Add(notebook);
        await context.SaveChangesAsync();

        var contact = new Contact
        {
            ContactNotebookId = notebook.Id,
            MobileNumber = $"0912{suffix.PadRight(7, '0')[..7]}",
            FullName = "مخاطب تست",
            CreatedAt = DateTime.UtcNow
        };
        context.Contacts.Add(contact);
        await context.SaveChangesAsync();
        return contact;
    }

    private static async Task<Models.ProfessionalCampaign> SeedTemplateCampaignAsync(
        Api_Context context,
        int userId,
        string title,
        string status,
        bool approved,
        int secondDelayMinutes = 60)
    {
        var campaign = new Models.ProfessionalCampaign
        {
            UserId = userId,
            Title = title,
            TargetType = ProfessionalCampaignTargetTypes.Notebooks,
            TargetIdsJson = "[1]",
            Status = status,
            StartAtUtc = DateTime.UtcNow,
            RecipientsCount = 1,
            IsActive = status == ProfessionalCampaignStatuses.Active,
            CreatedAt = DateTime.UtcNow
        };
        context.ProfessionalCampaigns.Add(campaign);
        await context.SaveChangesAsync();

        context.ProfessionalCampaignRecipients.Add(new ProfessionalCampaignRecipient
        {
            ProfessionalCampaignId = campaign.Id,
            MobileNumber = "09120000000",
            FullName = "گیرنده قالب",
            CreatedAt = DateTime.UtcNow
        });

        var approval = approved
            ? AdminApprovalStatuses.Approved
            : AdminApprovalStatuses.Pending;
        var stepStatus = approved
            ? ProfessionalCampaignStepStatuses.Pending
            : ProfessionalCampaignStepStatuses.PendingApproval;

        context.ProfessionalCampaignSteps.AddRange(
            new ProfessionalCampaignStep
            {
                ProfessionalCampaignId = campaign.Id,
                StepOrder = 1,
                Content = $"{title}-پیام1",
                DelayAfterPreviousMinutes = 0,
                Status = stepStatus,
                ApprovalStatus = approval,
                CreatedAt = DateTime.UtcNow
            },
            new ProfessionalCampaignStep
            {
                ProfessionalCampaignId = campaign.Id,
                StepOrder = 2,
                Content = $"{title}-پیام2",
                DelayAfterPreviousMinutes = secondDelayMinutes,
                Status = stepStatus,
                ApprovalStatus = approval,
                CreatedAt = DateTime.UtcNow
            });
        await context.SaveChangesAsync();
        return campaign;
    }

    private static async Task SeedEnrollmentCampaignAsync(Api_Context context, int userId, string title)
    {
        var campaign = new Models.ProfessionalCampaign
        {
            UserId = userId,
            Title = title,
            TargetType = ProfessionalCampaignTargetTypes.QuickSend,
            TargetIdsJson = "[99]",
            Status = ProfessionalCampaignStatuses.Active,
            StartAtUtc = DateTime.UtcNow,
            RecipientsCount = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.ProfessionalCampaigns.Add(campaign);
        await context.SaveChangesAsync();

        context.ProfessionalCampaignSteps.AddRange(
            new ProfessionalCampaignStep
            {
                ProfessionalCampaignId = campaign.Id,
                StepOrder = 1,
                Content = "qs1",
                DelayAfterPreviousMinutes = 0,
                Status = ProfessionalCampaignStepStatuses.Pending,
                ApprovalStatus = AdminApprovalStatuses.Approved,
                CreatedAt = DateTime.UtcNow
            },
            new ProfessionalCampaignStep
            {
                ProfessionalCampaignId = campaign.Id,
                StepOrder = 2,
                Content = "qs2",
                DelayAfterPreviousMinutes = 60,
                Status = ProfessionalCampaignStepStatuses.Pending,
                ApprovalStatus = AdminApprovalStatuses.Approved,
                CreatedAt = DateTime.UtcNow
            });
        await context.SaveChangesAsync();
    }

    private sealed class RecordingMessageService : IMessageService
    {
        public int CallCount { get; private set; }
        public bool LastBypassAdminApproval { get; private set; }

        public Task<ApiResponse<DirectSendResultDto>> SendDirectMessageAsync(
            int userId,
            int messageId,
            SendDirectMessageDto sendDto,
            MessageSession? session = null,
            bool bypassAdminApproval = false)
        {
            CallCount++;
            LastBypassAdminApproval = bypassAdminApproval;
            return Task.FromResult(ApiResponse<DirectSendResultDto>.CreateSuccess(new DirectSendResultDto
            {
                SentCount = 1,
                FailedCount = 0,
                TotalCost = 160
            }));
        }

        private static Task<ApiResponse<T>> NotUsed<T>() =>
            Task.FromResult(ApiResponse<T>.BadRequest("not used in professional campaign quick-send tests"));

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
