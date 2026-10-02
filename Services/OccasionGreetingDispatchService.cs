using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Services
{
    /// <summary>
    /// ارسال تبریک/تسلیت مناسبتی مستقیم از جدول مناسبت‌های کاربر.
    /// متن‌ها قبلاً تأیید ادمین گرفته‌اند (ResolveEffectiveTemplate)، پس کمپین Approved ساخته و بلافاصله ارسال می‌شود.
    /// تکرار با کلید (مناسبت، مخاطب، روز تهران) کنترل می‌شود؛ اجرای امروز با هر وضعیتی دوباره ارسال نمی‌شود.
    /// </summary>
    public class OccasionGreetingDispatchService : IOccasionGreetingDispatchService
    {
        private const string CampaignTitlePrefix = "مناسبت‌های خاص";
        private const string ExecutionStatusQueued = "Queued";
        private const string ExecutionStatusSuccess = "Success";
        private const string ExecutionStatusFailed = "Failed";
        private const string MessageTooLongError = "متن پیامک بیش از حداکثر صفحات مجاز است.";

        private readonly Api_Context _context;
        private readonly IMessageService _messageService;
        private readonly ISmsPricingService _smsPricing;
        private readonly IAuditService _audit;
        private readonly ILogger<OccasionGreetingDispatchService> _logger;

        public OccasionGreetingDispatchService(
            Api_Context context,
            IMessageService messageService,
            ISmsPricingService smsPricing,
            IAuditService audit,
            ILogger<OccasionGreetingDispatchService> logger)
        {
            _context = context;
            _messageService = messageService;
            _smsPricing = smsPricing;
            _audit = audit;
            _logger = logger;
        }

        public async Task<int> DispatchDueGreetingsAsync(CancellationToken cancellationToken = default)
        {
            var nowUtc = DateTime.UtcNow;
            var todayParts = OccasionCalendarHelper.GetTodayParts(nowUtc);

            var occasionsToday = await LoadOccasionsTodayAsync(todayParts, cancellationToken);
            if (occasionsToday.Count == 0)
            {
                _logger.LogDebug(
                    "Occasion greeting — no occasions for Tehran {TehranDate} (J{J}/{Jd} G{G}/{Gd})",
                    todayParts.TehranDate, todayParts.JalaliMonth, todayParts.JalaliDay,
                    todayParts.GregorianMonth, todayParts.GregorianDay);
                return 0;
            }

            var occasionIds = occasionsToday.Select(o => o.Id).ToList();
            var preferences = await _context.UserOccasionPreferences
                .AsNoTracking()
                .Where(p => !p.IsDeleted && occasionIds.Contains(p.SpecialOccasionId))
                .ToListAsync(cancellationToken);

            var enabled = OccasionGreetingPlanner.ResolveEnabledOccasions(occasionsToday, preferences);
            if (enabled.Count == 0)
            {
                _logger.LogInformation(
                    "Occasion greeting — {OccasionCount} occasions today but none enabled for any user — Tehran {TehranDate}",
                    occasionsToday.Count, todayParts.TehranDate);
                return 0;
            }

            var candidateUserIds = enabled.Select(e => e.UserId).Distinct().ToList();
            var activeUserIds = await _context.Users
                .AsNoTracking()
                .Where(u => candidateUserIds.Contains(u.Id) && !u.IsDeleted)
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            var profiles = (await _context.UserOccasionProfiles
                    .AsNoTracking()
                    .Where(p => !p.IsDeleted && activeUserIds.Contains(p.UserId))
                    .ToListAsync(cancellationToken))
                .GroupBy(p => p.UserId)
                .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Id).First());

            var dueUserIds = activeUserIds
                .Where(userId => OccasionCalendarHelper.HasReachedScheduledTimeTehran(
                    OccasionGreetingPlanner.ResolveSendTimeTehran(profiles.GetValueOrDefault(userId)),
                    nowUtc))
                .ToList();

            _logger.LogInformation(
                "Occasion greeting scan — Tehran {TehranDate}, occasions={OccasionCount}, enabled={EnabledCount}, dueUsers={DueUserCount}",
                todayParts.TehranDate, occasionsToday.Count, enabled.Count, dueUserIds.Count);

            if (dueUserIds.Count == 0)
                return 0;

            var handledKeys = await LoadHandledKeysAsync(occasionIds, todayParts.TehranDate, cancellationToken);

            var totalQueued = 0;
            foreach (var userId in dueUserIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    totalQueued += await DispatchForUserAsync(
                        userId,
                        enabled,
                        profiles.GetValueOrDefault(userId),
                        handledKeys,
                        cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Occasion greeting dispatch failed — UserId: {UserId}", userId);
                }
                finally
                {
                    _context.ChangeTracker.Clear();
                }
            }

            if (totalQueued > 0)
            {
                _logger.LogInformation(
                    "Occasion greeting dispatch completed for Tehran {TehranDate} — {QueuedCount} recipients queued",
                    todayParts.TehranDate, totalQueued);
            }

            return totalQueued;
        }

        private async Task<List<SpecialOccasion>> LoadOccasionsTodayAsync(
            OccasionCalendarHelper.CalendarDayParts todayParts,
            CancellationToken cancellationToken)
        {
            var monthCandidates = new[]
                {
                    (byte)todayParts.JalaliMonth,
                    (byte)todayParts.GregorianMonth,
                    (byte)todayParts.HijriMonth
                }
                .Distinct()
                .ToList();

            var candidates = await _context.SpecialOccasions
                .AsNoTracking()
                .Where(so => !so.IsDeleted && so.IsActive && monthCandidates.Contains(so.Month))
                .ToListAsync(cancellationToken);

            return candidates
                .Where(so => OccasionCalendarHelper.IsOccasionToday(so.CalendarType, so.Month, so.Day, todayParts))
                .ToList();
        }

        private async Task<HashSet<(int OccasionId, int ContactId)>> LoadHandledKeysAsync(
            List<int> occasionIds,
            DateOnly tehranDate,
            CancellationToken cancellationToken)
        {
            var (dayStartUtc, dayEndUtc) = OccasionCalendarHelper.GetTehranDayUtcRange(tehranDate);

            var handled = await _context.AutomationExecutions
                .AsNoTracking()
                .Where(ae => ae.SpecialOccasionId.HasValue
                    && occasionIds.Contains(ae.SpecialOccasionId.Value)
                    && ae.ContactId.HasValue
                    && ae.ExecutedAt >= dayStartUtc
                    && ae.ExecutedAt < dayEndUtc)
                .Select(ae => new { OccasionId = ae.SpecialOccasionId!.Value, ContactId = ae.ContactId!.Value })
                .ToListAsync(cancellationToken);

            return handled.Select(h => (h.OccasionId, h.ContactId)).ToHashSet();
        }

        private async Task<int> DispatchForUserAsync(
            int userId,
            IReadOnlyCollection<OccasionGreetingPlanner.EnabledOccasion> enabled,
            UserOccasionProfile? profile,
            HashSet<(int OccasionId, int ContactId)> handledKeys,
            CancellationToken cancellationToken)
        {
            var contacts = await _context.Contacts
                .AsNoTracking()
                .Where(c => !c.IsDeleted
                    && !c.ContactNotebook.IsDeleted
                    && c.ContactNotebook.UserId == userId)
                .Select(c => new Contact
                {
                    Id = c.Id,
                    ContactNotebookId = c.ContactNotebookId,
                    MobileNumber = c.MobileNumber,
                    FullName = c.FullName
                })
                .ToListAsync(cancellationToken);

            var (batches, skipped) = OccasionGreetingPlanner.PlanUserBatches(
                userId, enabled, profile, contacts, handledKeys);

            foreach (var skip in skipped.Where(s => s.Reason != OccasionGreetingPlanner.SkipReasonNoRecipients))
            {
                _logger.LogInformation(
                    "Occasion greeting skipped — UserId: {UserId}, OccasionId: {OccasionId}, Reason: {Reason}",
                    skip.UserId, skip.OccasionId, skip.Reason);
            }

            var queued = 0;
            foreach (var batch in batches)
            {
                queued += await EnqueueAndSendAsync(batch, cancellationToken);
                foreach (var contact in batch.Contacts)
                    handledKeys.Add((batch.Occasion.Id, contact.Id));
            }

            return queued;
        }

        private async Task<int> EnqueueAndSendAsync(
            OccasionGreetingPlanner.PlannedBatch batch,
            CancellationToken cancellationToken)
        {
            var pricing = await _smsPricing.GetRuntimeAsync(cancellationToken);
            var now = DateTime.UtcNow;
            var preview = batch.Content.Length > 2000 ? batch.Content[..2000] : batch.Content;

            if (!SmsPartsCalculator.TryCalculateParts(batch.Content, pricing.Rules, out var partsCount, out _))
            {
                _logger.LogWarning(
                    "Occasion greeting exceeds max SMS pages — UserId: {UserId}, OccasionId: {OccasionId}",
                    batch.UserId, batch.Occasion.Id);

                await AddExecutionsAsync(batch, now, preview, ExecutionStatusFailed, MessageTooLongError, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return 0;
            }

            var title = $"{CampaignTitlePrefix} — {batch.Occasion.Name}";
            MessageCampaign campaign;
            List<AutomationExecution> executions;

            var useTransaction = _context.Database.IsRelational();
            await using var transaction = useTransaction
                ? await _context.Database.BeginTransactionAsync(cancellationToken)
                : null;
            try
            {
                var message = new Message
                {
                    UserId = batch.UserId,
                    Title = title,
                    Content = batch.Content,
                    CharacterCount = SmsPartsCalculator.CountMessageCharacters(batch.Content, pricing.Rules),
                    PartsCount = partsCount,
                    IsPersonalized = true,
                    Status = "Ready",
                    CreatedAt = now
                };
                await _context.Messages.AddAsync(message, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                campaign = new MessageCampaign
                {
                    MessageId = message.Id,
                    UserId = batch.UserId,
                    Title = title,
                    SendType = "Automated",
                    RecipientsCount = batch.Contacts.Count,
                    PartsCount = partsCount,
                    Status = "Pending",
                    AdminApprovalStatus = AdminApprovalStatuses.Approved,
                    IsActive = true,
                    CreatedAt = now
                };

                foreach (var contact in batch.Contacts)
                {
                    campaign.Recipients.Add(new MessageRecipient
                    {
                        ContactId = contact.Id,
                        MobileNumber = contact.MobileNumber,
                        FullName = contact.FullName,
                        Status = "Pending",
                        CreatedAt = now
                    });
                }

                await _context.MessageCampaigns.AddAsync(campaign, cancellationToken);
                executions = await AddExecutionsAsync(
                    batch, now, preview, ExecutionStatusQueued, null, cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);
                if (transaction != null)
                    await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                if (transaction != null)
                    await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }

            var campaignId = campaign.Id;
            var executionIds = executions.Select(e => e.Id).ToList();
            _context.ChangeTracker.Clear();

            await _audit.WriteAsync(new AuditEntry
            {
                Category = AuditCategories.Message,
                Action = AuditActions.CampaignAutoSent,
                EntityType = AuditEntityTypes.MessageCampaign,
                EntityId = campaignId.ToString(),
                ActorUserId = batch.UserId,
                Source = AuditSources.Background,
                After = new
                {
                    specialOccasionId = batch.Occasion.Id,
                    recipients = batch.Contacts.Count
                }
            }, cancellationToken);

            var sendResult = await _messageService.ConfirmAndSendCampaignAsync(
                campaignId,
                batch.UserId,
                bypassAdminApproval: true);

            await SyncAfterSendAsync(campaignId, executionIds, sendResult.Success, sendResult.Message, cancellationToken);

            _logger.LogInformation(
                "Occasion greeting campaign {CampaignId} — UserId: {UserId}, OccasionId: {OccasionId}, Recipients: {Recipients}, SendSuccess: {SendSuccess}, ErrorCode: {ErrorCode}",
                campaignId,
                batch.UserId,
                batch.Occasion.Id,
                batch.Contacts.Count,
                sendResult.Success,
                sendResult.ErrorCode);

            return batch.Contacts.Count;
        }

        private async Task<List<AutomationExecution>> AddExecutionsAsync(
            OccasionGreetingPlanner.PlannedBatch batch,
            DateTime executedAt,
            string preview,
            string status,
            string? errorMessage,
            CancellationToken cancellationToken)
        {
            var executions = batch.Contacts
                .Select(contact => new AutomationExecution
                {
                    AutomatedMessageId = null,
                    ContactId = contact.Id,
                    SpecialOccasionId = batch.Occasion.Id,
                    ExecutedAt = executedAt,
                    Status = status,
                    ErrorMessage = errorMessage,
                    MessageContent = preview,
                    SentCount = 0
                })
                .ToList();

            await _context.AutomationExecutions.AddRangeAsync(executions, cancellationToken);
            return executions;
        }

        /// <summary>
        /// وضعیت اجراها را با نتیجه واقعی گیرندگان هم‌تراز می‌کند؛
        /// اگر ارسال اصلاً شروع نشد (مثلاً موجودی ناکافی) کمپین Failed با پیام کنترل‌شده می‌شود.
        /// </summary>
        private async Task SyncAfterSendAsync(
            int campaignId,
            List<int> executionIds,
            bool sendSucceeded,
            string? sendMessage,
            CancellationToken cancellationToken)
        {
            var recipientStatus = (await _context.MessageRecipients
                    .AsNoTracking()
                    .Where(r => r.CampaignId == campaignId && r.ContactId.HasValue)
                    .Select(r => new { ContactId = r.ContactId!.Value, r.Status })
                    .ToListAsync(cancellationToken))
                .GroupBy(r => r.ContactId)
                .ToDictionary(g => g.Key, g => g.First().Status);

            var campaign = await _context.MessageCampaigns
                .FirstOrDefaultAsync(c => c.Id == campaignId, cancellationToken);

            var notStarted = campaign != null && campaign.Status == "Pending";
            var failureMessage = !sendSucceeded && notStarted && !string.IsNullOrWhiteSpace(sendMessage)
                ? sendMessage
                : ControlledErrorHelper.SendFailed;

            if (campaign != null && notStarted)
            {
                campaign.Status = "Failed";
                campaign.ErrorMessage = failureMessage;
                campaign.UpdatedAt = DateTime.UtcNow;
            }

            var executions = await _context.AutomationExecutions
                .Where(ae => executionIds.Contains(ae.Id))
                .ToListAsync(cancellationToken);

            foreach (var execution in executions)
            {
                var sent = execution.ContactId.HasValue
                    && recipientStatus.TryGetValue(execution.ContactId.Value, out var status)
                    && status == "Sent";

                execution.Status = sent ? ExecutionStatusSuccess : ExecutionStatusFailed;
                execution.SentCount = sent ? 1 : 0;
                execution.FailedCount = sent ? 0 : 1;
                execution.ErrorMessage = sent ? null : failureMessage;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
