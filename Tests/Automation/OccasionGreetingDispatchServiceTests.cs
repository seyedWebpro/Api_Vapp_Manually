using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Message;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services;
using Api_Vapp.Tests.Shared;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api_Vapp.Tests.Automation
{
    /// <summary>
    /// مسیر ارسال مناسبتی بدون AutomatedMessage — با InMemory و fake پیام‌رسان.
    /// </summary>
    public class OccasionGreetingDispatchServiceTests
    {
        [Fact]
        public async Task Dispatch_SendsEnabledCustomOccasion_WithoutAutomatedMessage_AndDedupesSameDay()
        {
            await using var db = OccasionDispatchTestHost.Create();
            var today = OccasionCalendarHelper.GetTodayParts();

            var user = new User
            {
                FullName = "کاربر تست",
                PhoneNumber = "09120000001",
                CreatedAt = DateTime.UtcNow
            };
            db.Context.Users.Add(user);
            await db.Context.SaveChangesAsync();

            var notebook = new ContactNotebook
            {
                UserId = user.Id,
                Name = "دفترچه",
                CreatedAt = DateTime.UtcNow
            };
            db.Context.ContactNotebooks.Add(notebook);
            await db.Context.SaveChangesAsync();

            var contacts = Enumerable.Range(1, 2).Select(i => new Contact
            {
                ContactNotebookId = notebook.Id,
                FullName = $"مخاطب {i}",
                MobileNumber = $"0912111000{i}",
                CreatedAt = DateTime.UtcNow
            }).ToList();
            db.Context.Contacts.AddRange(contacts);

            var occasion = new SpecialOccasion
            {
                UserId = user.Id,
                Name = "مناسبت تست امروز",
                Type = "Custom",
                Category = OccasionCategories.Congratulation,
                CalendarType = OccasionCalendarTypes.Jalali,
                Month = (byte)today.JalaliMonth,
                Day = (byte)today.JalaliDay,
                OccasionDate = DateTime.UtcNow.Date,
                IsSystem = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Context.SpecialOccasions.Add(occasion);
            await db.Context.SaveChangesAsync();

            db.Context.UserOccasionPreferences.Add(new UserOccasionPreference
            {
                UserId = user.Id,
                SpecialOccasionId = occasion.Id,
                IsEnabled = true,
                ApplyToAllContacts = true,
                CustomMessage = "سلام {{نام}} — {{مناسبت}}",
                TemplateApprovalStatus = AdminApprovalStatuses.Approved,
                CreatedAt = DateTime.UtcNow
            });
            db.Context.UserOccasionProfiles.Add(new UserOccasionProfile
            {
                UserId = user.Id,
                BusinessName = "برند تست",
                CongratulationsEnabled = true,
                CondolencesEnabled = true,
                ScheduledTimeTehran = TimeSpan.Zero,
                CreatedAt = DateTime.UtcNow
            });
            await db.Context.SaveChangesAsync();

            var first = await db.Service.DispatchDueGreetingsAsync();
            Assert.Equal(2, first);

            var executions = await db.Context.AutomationExecutions
                .Where(e => e.SpecialOccasionId == occasion.Id)
                .ToListAsync();
            Assert.Equal(2, executions.Count);
            Assert.All(executions, e =>
            {
                Assert.Null(e.AutomatedMessageId);
                Assert.Equal("Success", e.Status);
                Assert.Equal(1, e.SentCount);
            });

            var campaign = Assert.Single(await db.Context.MessageCampaigns.ToListAsync());
            Assert.Equal(2, campaign.RecipientsCount);
            Assert.Equal("Sent", campaign.Status);
            Assert.Null(campaign.AutomatedMessageId);

            var second = await db.Service.DispatchDueGreetingsAsync();
            Assert.Equal(0, second);
            Assert.Equal(2, await db.Context.AutomationExecutions.CountAsync(e => e.SpecialOccasionId == occasion.Id));
            Assert.Equal(1, await db.Context.MessageCampaigns.CountAsync());
        }

        [Fact]
        public async Task Dispatch_SkipsDisabledPreference_EvenIfOccasionIsToday()
        {
            await using var db = OccasionDispatchTestHost.Create();
            var today = OccasionCalendarHelper.GetTodayParts();

            var user = new User { FullName = "u", PhoneNumber = "09120000002", CreatedAt = DateTime.UtcNow };
            db.Context.Users.Add(user);
            await db.Context.SaveChangesAsync();
            var notebook = new ContactNotebook { UserId = user.Id, Name = "n", CreatedAt = DateTime.UtcNow };
            db.Context.ContactNotebooks.Add(notebook);
            await db.Context.SaveChangesAsync();
            db.Context.Contacts.Add(new Contact
            {
                ContactNotebookId = notebook.Id,
                FullName = "c",
                MobileNumber = "09123334444",
                CreatedAt = DateTime.UtcNow
            });

            var occasion = new SpecialOccasion
            {
                UserId = user.Id,
                Name = "خاموش",
                Type = "Custom",
                Category = OccasionCategories.Congratulation,
                CalendarType = OccasionCalendarTypes.Jalali,
                Month = (byte)today.JalaliMonth,
                Day = (byte)today.JalaliDay,
                OccasionDate = DateTime.UtcNow.Date,
                IsSystem = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Context.SpecialOccasions.Add(occasion);
            await db.Context.SaveChangesAsync();
            db.Context.UserOccasionPreferences.Add(new UserOccasionPreference
            {
                UserId = user.Id,
                SpecialOccasionId = occasion.Id,
                IsEnabled = false,
                ApplyToAllContacts = true,
                CustomMessage = "متن",
                TemplateApprovalStatus = AdminApprovalStatuses.Approved,
                CreatedAt = DateTime.UtcNow
            });
            db.Context.UserOccasionProfiles.Add(new UserOccasionProfile
            {
                UserId = user.Id,
                CongratulationsEnabled = true,
                ScheduledTimeTehran = TimeSpan.Zero,
                CreatedAt = DateTime.UtcNow
            });
            await db.Context.SaveChangesAsync();

            Assert.Equal(0, await db.Service.DispatchDueGreetingsAsync());
            Assert.Empty(await db.Context.AutomationExecutions.ToListAsync());
        }
    }

    file sealed class OccasionDispatchTestHost : IAsyncDisposable
    {
        public Api_Context Context { get; }
        public OccasionGreetingDispatchService Service { get; }

        private OccasionDispatchTestHost(Api_Context context, OccasionGreetingDispatchService service)
        {
            Context = context;
            Service = service;
        }

        public static OccasionDispatchTestHost Create()
        {
            var options = new DbContextOptionsBuilder<Api_Context>()
                .UseInMemoryDatabase($"occasion-dispatch-{Guid.NewGuid():N}")
                .Options;
            var context = new Api_Context(options);
            var messages = new CapturingMessageService(context);
            var service = new OccasionGreetingDispatchService(
                context,
                messages,
                new FixedSmsPricingService(),
                new NoOpAuditService(),
                NullLogger<OccasionGreetingDispatchService>.Instance);

            return new OccasionDispatchTestHost(context, service);
        }

        public ValueTask DisposeAsync()
        {
            Context.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    file sealed class FixedSmsPricingService : ISmsPricingService
    {
        public Task<SmsPricingRuntime> GetRuntimeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(SmsPricingRuntime.Defaults);

        public Task<ApiResponse<SmsPricingSettingResponseDto>> GetAdminSettingsAsync() =>
            Task.FromResult(ApiResponse<SmsPricingSettingResponseDto>.CreateSuccess(new SmsPricingSettingResponseDto()));

        public Task<ApiResponse<SmsPricingSettingResponseDto>> UpdateAdminSettingsAsync(UpdateSmsPricingSettingDto dto) =>
            Task.FromResult(ApiResponse<SmsPricingSettingResponseDto>.CreateSuccess(new SmsPricingSettingResponseDto()));

        public Task<ApiResponse<SmsPricingPreviewResponseDto>> PreviewAsync(SmsPricingPreviewRequestDto dto) =>
            Task.FromResult(ApiResponse<SmsPricingPreviewResponseDto>.CreateSuccess(new SmsPricingPreviewResponseDto()));

        public Task<ApiResponse<SmsPartsEstimateResponseDto>> EstimatePartsForUserAsync(SmsPartsEstimateRequestDto dto) =>
            Task.FromResult(ApiResponse<SmsPartsEstimateResponseDto>.CreateSuccess(new SmsPartsEstimateResponseDto()));
    }

    file sealed class CapturingMessageService : IMessageService
    {
        private readonly Api_Context _context;
        public CapturingMessageService(Api_Context context) => _context = context;

        public async Task<ApiResponse<bool>> ConfirmAndSendCampaignAsync(
            int campaignId, int userId, bool bypassAdminApproval = false)
        {
            var campaign = await _context.MessageCampaigns
                .Include(c => c.Recipients)
                .FirstAsync(c => c.Id == campaignId && c.UserId == userId);

            foreach (var recipient in campaign.Recipients.Where(r => r.Status == "Pending"))
            {
                recipient.Status = "Sent";
                recipient.SentAt = DateTime.UtcNow;
            }

            campaign.Status = "Sent";
            campaign.SentAt = DateTime.UtcNow;
            campaign.SentCount = campaign.Recipients.Count(r => r.Status == "Sent");
            campaign.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return ApiResponse<bool>.CreateSuccess(true, "ok");
        }

        private static Task<ApiResponse<T>> NotUsed<T>() =>
            Task.FromResult(ApiResponse<T>.BadRequest("not used in occasion dispatch tests"));

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
        public Task<ApiResponse<DirectSendResultDto>> SendDirectMessageAsync(int userId, int messageId, SendDirectMessageDto sendDto, MessageSession? session = null, bool bypassAdminApproval = false) => NotUsed<DirectSendResultDto>();
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
