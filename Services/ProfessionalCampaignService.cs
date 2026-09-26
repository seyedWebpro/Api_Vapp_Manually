using System.Text.Json;
using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Message;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Services
{
    public class ProfessionalCampaignService : IProfessionalCampaignService
    {
        private readonly Api_Context _context;
        private readonly IProfessionalCampaignRepository _campaignRepository;
        private readonly IMessageService _messageService;
        private readonly ILogger<ProfessionalCampaignService> _logger;

        public ProfessionalCampaignService(
            Api_Context context,
            IProfessionalCampaignRepository campaignRepository,
            IMessageService messageService,
            ILogger<ProfessionalCampaignService> logger)
        {
            _context = context;
            _campaignRepository = campaignRepository;
            _messageService = messageService;
            _logger = logger;
        }

        public async Task<ApiResponse<ProfessionalCampaignResponseDto>> CreateAsync(
            int userId,
            CreateProfessionalCampaignDto dto)
        {
            _logger.LogInformation("Creating professional campaign — UserId: {UserId}, StepCount: {StepCount}", userId, dto.Steps.Count);
            var targetType = NormalizeTargetType(dto.TargetType);
            if (targetType == null)
                return ApiResponse<ProfessionalCampaignResponseDto>.BadRequest("نوع مخاطبان فقط می‌تواند دفترچه تلفن یا تگ باشد");

            var targetIds = dto.TargetIds.Where(id => id > 0).Distinct().ToList();
            if (targetIds.Count == 0)
                return ApiResponse<ProfessionalCampaignResponseDto>.BadRequest("حداقل یک دفترچه تلفن یا تگ معتبر انتخاب کنید");

            if (dto.Steps.Count < 2 || dto.Steps.Count > 20)
                return ApiResponse<ProfessionalCampaignResponseDto>.BadRequest("کمپین حرفه‌ای باید بین ۲ تا ۲۰ پیام داشته باشد");

            if (dto.Steps.Any(s => string.IsNullOrWhiteSpace(s.Content)))
                return ApiResponse<ProfessionalCampaignResponseDto>.BadRequest("متن همه پیام‌ها الزامی است");

            if (GetDelayMinutes(dto.Steps[0]) != 0)
                return ApiResponse<ProfessionalCampaignResponseDto>.BadRequest("پیام اول باید بدون تأخیر باشد");

            var startAtUtc = NormalizeToUtc(dto.StartAt);
            if (startAtUtc.HasValue && startAtUtc.Value < DateTime.UtcNow.AddMinutes(-1))
                return ApiResponse<ProfessionalCampaignResponseDto>.BadRequest("زمان شروع کمپین نمی‌تواند در گذشته باشد");

            var recipientsResult = await ResolveRecipientsAsync(userId, targetType, targetIds);
            if (recipientsResult.Error != null)
                return ApiResponse<ProfessionalCampaignResponseDto>.BadRequest(recipientsResult.Error);
            if (recipientsResult.Recipients.Count == 0)
                return ApiResponse<ProfessionalCampaignResponseDto>.BadRequest("دفترچه‌ها یا تگ‌های انتخاب‌شده مخاطب فعالی ندارند");

            await using var transaction = await _context.Database.BeginTransactionAsync();

            var campaign = new ProfessionalCampaign
            {
                UserId = userId,
                Title = dto.Title.Trim(),
                TargetType = targetType,
                TargetIdsJson = JsonSerializer.Serialize(targetIds),
                Status = ProfessionalCampaignStatuses.PendingApproval,
                StartAtUtc = startAtUtc,
                RecipientsCount = recipientsResult.Recipients.Count,
                IsActive = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.ProfessionalCampaigns.Add(campaign);
            await _context.SaveChangesAsync();

            _context.ProfessionalCampaignRecipients.AddRange(recipientsResult.Recipients.Select(r =>
                new ProfessionalCampaignRecipient
                {
                    ProfessionalCampaignId = campaign.Id,
                    ContactId = r.ContactId,
                    MobileNumber = r.MobileNumber,
                    FullName = r.FullName,
                    CreatedAt = DateTime.UtcNow
                }));

            var normalizedContents = dto.Steps.Select(s => s.Content.Trim()).Distinct().ToList();
            var approvedContents = await _context.MessageTemplates
                .AsNoTracking()
                .Where(t => t.UserId == userId
                    && !t.IsDeleted
                    && t.IsActive
                    && t.ApprovalStatus == AdminApprovalStatuses.Approved
                    && normalizedContents.Contains(t.Content))
                .Select(t => t.Content)
                .ToListAsync();
            var approvedSet = approvedContents.ToHashSet(StringComparer.Ordinal);

            for (var index = 0; index < dto.Steps.Count; index++)
            {
                var input = dto.Steps[index];
                var content = input.Content.Trim();
                var approved = approvedSet.Contains(content);
                var step = new ProfessionalCampaignStep
                {
                    ProfessionalCampaignId = campaign.Id,
                    StepOrder = index + 1,
                    Content = content,
                    DelayAfterPreviousMinutes = GetDelayMinutes(input),
                    Status = approved
                        ? ProfessionalCampaignStepStatuses.Pending
                        : ProfessionalCampaignStepStatuses.PendingApproval,
                    ApprovalStatus = approved
                        ? AdminApprovalStatuses.Approved
                        : AdminApprovalStatuses.Pending,
                    CreatedAt = DateTime.UtcNow
                };
                _context.ProfessionalCampaignSteps.Add(step);
                await _context.SaveChangesAsync();

                if (!approved)
                {
                    _context.SmsApprovalRequests.Add(new SmsApprovalRequest
                    {
                        UserId = userId,
                        RequestType = SmsApprovalRequestTypes.ProfessionalCampaignStep,
                        ProfessionalCampaignStepId = step.Id,
                        ContentPreview = content,
                        TitlePreview = $"{campaign.Title} - پیام {step.StepOrder}",
                        RecipientsCount = campaign.RecipientsCount,
                        Status = AdminApprovalStatuses.Pending,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            var hasPendingApproval = await _context.ProfessionalCampaignSteps.AnyAsync(
                s => s.ProfessionalCampaignId == campaign.Id
                    && !s.IsDeleted
                    && s.ApprovalStatus != AdminApprovalStatuses.Approved);
            if (!hasPendingApproval)
                campaign.Status = ProfessionalCampaignStatuses.Ready;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var created = await _campaignRepository.GetOwnedAsync(userId, campaign.Id);
            _logger.LogInformation("Professional campaign created — CampaignId: {CampaignId}, UserId: {UserId}", campaign.Id, userId);
            return ApiResponse<ProfessionalCampaignResponseDto>.CreateSuccess(
                Map(created!),
                campaign.Status == ProfessionalCampaignStatuses.Ready
                    ? "کمپین ساخته شد و آماده فعال‌سازی است"
                    : "کمپین ساخته شد و متن‌های جدید برای تأیید ادمین ارسال شدند",
                201);
        }

        public async Task<ApiResponse<ProfessionalCampaignResponseDto>> GetByIdAsync(int userId, int id)
        {
            var campaign = await _campaignRepository.GetOwnedAsync(userId, id);
            return campaign == null
                ? ApiResponse<ProfessionalCampaignResponseDto>.NotFound("کمپین یافت نشد")
                : ApiResponse<ProfessionalCampaignResponseDto>.CreateSuccess(Map(campaign));
        }

        public async Task<ApiResponse<ProfessionalCampaignListResponseDto>> GetListAsync(
            int userId,
            int pageNumber,
            int pageSize)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var (campaigns, totalCount) = await _campaignRepository.GetPagedOwnedAsync(userId, pageNumber, pageSize);

            return ApiResponse<ProfessionalCampaignListResponseDto>.CreateSuccess(
                new ProfessionalCampaignListResponseDto
                {
                    Items = campaigns.Select(Map).ToList(),
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                });
        }

        public async Task<ApiResponse<ProfessionalCampaignResponseDto>> ActivateAsync(int userId, int id)
        {
            var campaign = await _campaignRepository.GetOwnedAsync(userId, id, tracking: true);
            if (campaign == null)
                return ApiResponse<ProfessionalCampaignResponseDto>.NotFound("کمپین یافت نشد");
            if (campaign.Status is ProfessionalCampaignStatuses.Completed or ProfessionalCampaignStatuses.Cancelled)
                return ApiResponse<ProfessionalCampaignResponseDto>.BadRequest("این کمپین دیگر قابل فعال‌سازی نیست");
            if (campaign.Steps.Any(s => s.ApprovalStatus != AdminApprovalStatuses.Approved))
                return ApiResponse<ProfessionalCampaignResponseDto>.BadRequest("همه متن‌های کمپین هنوز تأیید نشده‌اند");

            var startUtc = campaign.StartAtUtc.HasValue && campaign.StartAtUtc.Value > DateTime.UtcNow
                ? campaign.StartAtUtc.Value
                : DateTime.UtcNow;
            var orderedSteps = campaign.Steps.OrderBy(s => s.StepOrder).ToList();
            var projected = ProfessionalCampaignSchedule.BuildProjectedUtc(
                DateTime.SpecifyKind(startUtc, DateTimeKind.Utc),
                orderedSteps.Select(s => s.DelayAfterPreviousMinutes));
            for (var index = 0; index < orderedSteps.Count; index++)
            {
                var step = orderedSteps[index];
                step.ScheduledAtUtc = projected[index];
                step.Status = ProfessionalCampaignStepStatuses.Pending;
                step.UpdatedAt = DateTime.UtcNow;
            }

            campaign.StartAtUtc = campaign.Steps.Min(s => s.ScheduledAtUtc);
            campaign.Status = ProfessionalCampaignStatuses.Active;
            campaign.IsActive = true;
            campaign.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return ApiResponse<ProfessionalCampaignResponseDto>.CreateSuccess(Map(campaign), "کمپین فعال شد");
        }

        public Task<ApiResponse<ProfessionalCampaignResponseDto>> PauseAsync(int userId, int id) =>
            SetActiveStateAsync(userId, id, false);

        public Task<ApiResponse<ProfessionalCampaignResponseDto>> ResumeAsync(int userId, int id) =>
            SetActiveStateAsync(userId, id, true);

        public async Task<ApiResponse<bool>> CancelAsync(int userId, int id)
        {
            var campaign = await _campaignRepository.GetOwnedAsync(userId, id, tracking: true);
            if (campaign == null)
                return ApiResponse<bool>.NotFound("کمپین یافت نشد");
            if (campaign.Steps.Any(s => s.Status == ProfessionalCampaignStepStatuses.Processing))
                return ApiResponse<bool>.BadRequest("در حال حاضر یکی از پیام‌های کمپین در حال ارسال است");

            campaign.Status = ProfessionalCampaignStatuses.Cancelled;
            campaign.IsActive = false;
            campaign.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return ApiResponse<bool>.CreateSuccess(true, "کمپین لغو شد");
        }

        public async Task<ApiResponse<ProfessionalCampaignResponseDto>> RetryFailedStepAsync(
            int userId,
            int campaignId,
            int stepId)
        {
            var campaign = await _campaignRepository.GetOwnedAsync(userId, campaignId, tracking: true);
            if (campaign == null)
                return ApiResponse<ProfessionalCampaignResponseDto>.NotFound("کمپین یافت نشد");

            var step = campaign.Steps.FirstOrDefault(s => s.Id == stepId);
            if (step == null)
                return ApiResponse<ProfessionalCampaignResponseDto>.NotFound("مرحله کمپین یافت نشد");
            if (step.Status != ProfessionalCampaignStepStatuses.Failed)
                return ApiResponse<ProfessionalCampaignResponseDto>.BadRequest("فقط مرحله ناموفق قابل تلاش مجدد است");
            if (step.ApprovalStatus != AdminApprovalStatuses.Approved)
                return ApiResponse<ProfessionalCampaignResponseDto>.BadRequest("متن این مرحله تأیید نشده است");

            step.Status = ProfessionalCampaignStepStatuses.Pending;
            step.ScheduledAtUtc = DateTime.UtcNow;
            step.FailedCount = 0;
            step.LastError = null;
            step.UpdatedAt = DateTime.UtcNow;
            campaign.Status = ProfessionalCampaignStatuses.Active;
            campaign.IsActive = true;
            campaign.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return ApiResponse<ProfessionalCampaignResponseDto>.CreateSuccess(Map(campaign), "مرحله برای تلاش مجدد آماده شد");
        }

        public async Task ProcessDueStepsAsync(CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var dueStepIds = await _context.ProfessionalCampaignSteps
                .AsNoTracking()
                .Where(s => !s.IsDeleted
                    && s.Status == ProfessionalCampaignStepStatuses.Pending
                    && s.ApprovalStatus == AdminApprovalStatuses.Approved
                    && s.ScheduledAtUtc.HasValue
                    && s.ScheduledAtUtc <= now
                    && !s.ProfessionalCampaign.IsDeleted
                    && s.ProfessionalCampaign.IsActive
                    && s.ProfessionalCampaign.Status == ProfessionalCampaignStatuses.Active)
                .Where(s => !_context.ProfessionalCampaignSteps.Any(previous =>
                    previous.ProfessionalCampaignId == s.ProfessionalCampaignId
                    && !previous.IsDeleted
                    && previous.StepOrder < s.StepOrder
                    && previous.Status != ProfessionalCampaignStepStatuses.Sent))
                .OrderBy(s => s.ScheduledAtUtc)
                .Select(s => s.Id)
                .Take(20)
                .ToListAsync(cancellationToken);

            foreach (var stepId in dueStepIds)
            {
                var claimed = await _context.ProfessionalCampaignSteps
                    .Where(s => s.Id == stepId && s.Status == ProfessionalCampaignStepStatuses.Pending)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(s => s.Status, ProfessionalCampaignStepStatuses.Processing)
                        .SetProperty(s => s.UpdatedAt, DateTime.UtcNow), cancellationToken);
                if (claimed == 0)
                    continue;

                try
                {
                    await SendStepAsync(stepId, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Professional campaign step failed - StepId: {StepId}", stepId);
                    await MarkStepFailedAsync(stepId, "ارسال پیام کمپین با خطا مواجه شد", cancellationToken);
                }
            }
        }

        private async Task SendStepAsync(int stepId, CancellationToken cancellationToken)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var step = await _context.ProfessionalCampaignSteps
                    .Include(s => s.ProfessionalCampaign)
                    .ThenInclude(c => c.Recipients.Where(r => !r.IsDeleted))
                    .FirstAsync(s => s.Id == stepId, cancellationToken);

                var campaign = step.ProfessionalCampaign;
                var recipientDtos = campaign.Recipients.Select(r => new RecipientItemDto
                {
                    ContactId = r.ContactId,
                    MobileNumber = r.MobileNumber,
                    FullName = r.FullName
                }).ToList();

                var message = new Message
                {
                    UserId = campaign.UserId,
                    Title = $"{campaign.Title} - پیام {step.StepOrder}",
                    Content = step.Content,
                    IsPersonalized = step.Content.Contains('{') && step.Content.Contains('}'),
                    CreatedAt = DateTime.UtcNow
                };
                _context.Messages.Add(message);
                await _context.SaveChangesAsync(cancellationToken);

                var session = new MessageSession
                {
                    MessageId = message.Id,
                    UserId = campaign.UserId,
                    SelectionCriteria = JsonSerializer.Serialize(new
                    {
                        SelectionType = campaign.TargetType,
                        ProfessionalCampaignId = campaign.Id,
                        ProfessionalCampaignStepId = step.Id
                    }),
                    RecipientsJson = JsonSerializer.Serialize(recipientDtos),
                    IsUsed = false,
                    ExpiresAt = DateTime.UtcNow.AddHours(24),
                    CreatedAt = DateTime.UtcNow
                };
                _context.MessageSessions.Add(session);
                await _context.SaveChangesAsync(cancellationToken);

                var result = await _messageService.SendDirectMessageAsync(
                    campaign.UserId,
                    message.Id,
                    new SendDirectMessageDto
                    {
                        SendType = CampaignSendType.Quick,
                        PreventDuplicate = false,
                        SendToSpecificTags = false
                    },
                    session,
                    bypassAdminApproval: true);

                if (!result.Success || result.Data == null || result.Data.SentCount == 0)
                {
                    await MarkStepFailedAsync(
                        step.Id,
                        string.IsNullOrWhiteSpace(result.Message) ? "هیچ پیامکی ارسال نشد" : result.Message,
                        cancellationToken,
                        result.Data?.FailedCount ?? campaign.RecipientsCount);
                    await transaction.CommitAsync(cancellationToken);
                    return;
                }

                step.Status = ProfessionalCampaignStepStatuses.Sent;
                step.SentCount = result.Data.SentCount;
                step.FailedCount = result.Data.FailedCount;
                step.SentAtUtc = DateTime.UtcNow;
                step.LastError = null;
                step.UpdatedAt = DateTime.UtcNow;

                var nextStep = await _context.ProfessionalCampaignSteps
                    .Where(s => s.ProfessionalCampaignId == campaign.Id
                        && !s.IsDeleted
                        && s.StepOrder > step.StepOrder
                        && s.Status == ProfessionalCampaignStepStatuses.Pending)
                    .OrderBy(s => s.StepOrder)
                    .FirstOrDefaultAsync(cancellationToken);
                if (nextStep == null)
                {
                    campaign.Status = ProfessionalCampaignStatuses.Completed;
                    campaign.IsActive = false;
                    campaign.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    // فاصله هر مرحله از زمان ارسال واقعی مرحله قبلی محاسبه می‌شود.
                    nextStep.ScheduledAtUtc = ProfessionalCampaignSchedule.GetNextUtc(
                        DateTime.SpecifyKind(step.SentAtUtc.Value, DateTimeKind.Utc),
                        nextStep.DelayAfterPreviousMinutes);
                    nextStep.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        private async Task MarkStepFailedAsync(
            int stepId,
            string message,
            CancellationToken cancellationToken,
            int failedCount = 0)
        {
            var step = await _context.ProfessionalCampaignSteps
                .Include(s => s.ProfessionalCampaign)
                .FirstOrDefaultAsync(s => s.Id == stepId, cancellationToken);
            if (step == null)
                return;
            step.Status = ProfessionalCampaignStepStatuses.Failed;
            step.FailedCount = failedCount;
            step.LastError = message.Length > 1000 ? message[..1000] : message;
            step.UpdatedAt = DateTime.UtcNow;
            step.ProfessionalCampaign.Status = ProfessionalCampaignStatuses.Paused;
            step.ProfessionalCampaign.IsActive = false;
            step.ProfessionalCampaign.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<ApiResponse<ProfessionalCampaignResponseDto>> SetActiveStateAsync(
            int userId,
            int id,
            bool resume)
        {
            var campaign = await _campaignRepository.GetOwnedAsync(userId, id, tracking: true);
            if (campaign == null)
                return ApiResponse<ProfessionalCampaignResponseDto>.NotFound("کمپین یافت نشد");

            if (resume)
            {
                if (campaign.Status != ProfessionalCampaignStatuses.Paused)
                    return ApiResponse<ProfessionalCampaignResponseDto>.BadRequest("فقط کمپین متوقف‌شده قابل ادامه است");
                if (campaign.Steps.Any(s => s.Status == ProfessionalCampaignStepStatuses.Failed))
                    return ApiResponse<ProfessionalCampaignResponseDto>.BadRequest("ابتدا خطای مرحله ناموفق باید بررسی شود");
                campaign.Status = ProfessionalCampaignStatuses.Active;
                campaign.IsActive = true;
            }
            else
            {
                if (campaign.Status != ProfessionalCampaignStatuses.Active)
                    return ApiResponse<ProfessionalCampaignResponseDto>.BadRequest("فقط کمپین فعال قابل توقف است");
                if (campaign.Steps.Any(s => s.Status == ProfessionalCampaignStepStatuses.Processing))
                    return ApiResponse<ProfessionalCampaignResponseDto>.BadRequest("در حال حاضر یکی از پیام‌ها در حال ارسال است");
                campaign.Status = ProfessionalCampaignStatuses.Paused;
                campaign.IsActive = false;
            }

            campaign.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return ApiResponse<ProfessionalCampaignResponseDto>.CreateSuccess(
                Map(campaign),
                resume ? "کمپین ادامه یافت" : "کمپین متوقف شد");
        }

        private async Task<(List<RecipientItemDto> Recipients, string? Error)> ResolveRecipientsAsync(
            int userId,
            string targetType,
            IReadOnlyCollection<int> targetIds)
        {
            if (targetType == ProfessionalCampaignTargetTypes.Notebooks)
            {
                var validIds = await _context.ContactNotebooks.AsNoTracking()
                    .Where(n => n.UserId == userId && !n.IsDeleted && targetIds.Contains(n.Id))
                    .Select(n => n.Id)
                    .ToListAsync();
                if (validIds.Count != targetIds.Count)
                    return (new(), "یک یا چند دفترچه تلفن یافت نشد یا متعلق به شما نیست");

                var recipients = await _context.Contacts.AsNoTracking()
                    .Where(c => validIds.Contains(c.ContactNotebookId) && !c.IsDeleted)
                    .Select(c => new RecipientItemDto
                    {
                        ContactId = c.Id,
                        MobileNumber = c.MobileNumber,
                        FullName = c.FullName
                    })
                    .ToListAsync();
                return (DistinctRecipients(recipients), null);
            }

            var validTagIds = await _context.MessageTags.AsNoTracking()
                .Where(t => t.UserId == userId && !t.IsDeleted && t.IsActive && targetIds.Contains(t.Id))
                .Select(t => t.Id)
                .ToListAsync();
            if (validTagIds.Count != targetIds.Count)
                return (new(), "یک یا چند تگ یافت نشد یا متعلق به شما نیست");

            var taggedRecipients = await _context.ContactTags.AsNoTracking()
                .Where(ct => validTagIds.Contains(ct.TagId)
                    && !ct.Contact.IsDeleted
                    && ct.Contact.ContactNotebook.UserId == userId
                    && !ct.Contact.ContactNotebook.IsDeleted)
                .Select(ct => new RecipientItemDto
                {
                    ContactId = ct.ContactId,
                    MobileNumber = ct.Contact.MobileNumber,
                    FullName = ct.Contact.FullName
                })
                .ToListAsync();
            return (DistinctRecipients(taggedRecipients), null);
        }

        private static ProfessionalCampaignResponseDto Map(ProfessionalCampaign campaign) => new()
        {
            Id = campaign.Id,
            Title = campaign.Title,
            TargetType = campaign.TargetType,
            TargetIds = JsonSerializer.Deserialize<List<int>>(campaign.TargetIdsJson) ?? new(),
            Status = campaign.Status,
            StartAtUtc = campaign.StartAtUtc,
            RecipientsCount = campaign.RecipientsCount,
            IsActive = campaign.IsActive,
            CreatedAt = campaign.CreatedAt,
            Steps = campaign.Steps.OrderBy(s => s.StepOrder).Select(s => new ProfessionalCampaignStepResponseDto
            {
                Id = s.Id,
                StepOrder = s.StepOrder,
                Content = s.Content,
                DelayAfterPreviousMinutes = s.DelayAfterPreviousMinutes,
                ScheduledAtUtc = s.ScheduledAtUtc,
                Status = s.Status,
                ApprovalStatus = s.ApprovalStatus,
                RejectionReason = s.RejectionReason,
                SentCount = s.SentCount,
                FailedCount = s.FailedCount,
                SentAtUtc = s.SentAtUtc
            }).ToList()
        };

        private static List<RecipientItemDto> DistinctRecipients(IEnumerable<RecipientItemDto> recipients) =>
            recipients
                .Where(r => !string.IsNullOrWhiteSpace(r.MobileNumber))
                .GroupBy(r => r.MobileNumber.Trim(), StringComparer.Ordinal)
                .Select(g =>
                {
                    var first = g.First();
                    first.MobileNumber = g.Key;
                    return first;
                })
                .ToList();

        private static int GetDelayMinutes(ProfessionalCampaignStepInputDto step) =>
            checked((step.DelayDays * 24 * 60) + (step.DelayHours * 60) + step.DelayMinutes);

        private static string? NormalizeTargetType(string value) => value.Trim().ToLowerInvariant() switch
        {
            "notebook" or "notebooks" => ProfessionalCampaignTargetTypes.Notebooks,
            "tag" or "tags" => ProfessionalCampaignTargetTypes.Tags,
            _ => null
        };

        private static DateTime? NormalizeToUtc(DateTimeOffset? value) =>
            value.HasValue ? ProfessionalCampaignSchedule.ToUtc(value.Value) : null;
    }
}
