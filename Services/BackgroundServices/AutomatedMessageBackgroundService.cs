using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;

namespace Api_Vapp.Services.BackgroundServices
{
    /// <summary>
    /// Background Service برای اجرای خودکار پیام‌های اتوماسیون
    /// هر 1 دقیقه یکبار پیام‌های خودکار را بررسی و اجرا می‌کند
    /// </summary>
    public class AutomatedMessageBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AutomatedMessageBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1); // هر 1 دقیقه یکبار

        public AutomatedMessageBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<AutomatedMessageBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Automated Message Background Service started");

            // تأخیر اولیه برای اطمینان از آماده بودن سیستم
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAutomatedMessagesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing automated messages");
                }

                // انتظار تا چک بعدی
                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("Automated Message Background Service stopped");
        }

        private async Task ProcessAutomatedMessagesAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<Api_Context>();
            var automatedMessageRepository = scope.ServiceProvider.GetRequiredService<IAutomatedMessageRepository>();
            var contactRepository = scope.ServiceProvider.GetRequiredService<IContactRepository>();
            var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();

            // دریافت تمام پیام‌های خودکار فعال (فقط خواندنی - بدون Tracking)
            var allAutomatedMessages = await context.AutomatedMessages
                .AsNoTracking()
                .Where(am => am.IsActive && !am.IsDeleted)
                .Select(am => new
                {
                    am.Id,
                    am.UserId,
                    am.AutomationType,
                    am.ScheduledTime,
                    am.DaysBeforeEvent,
                    am.SpecialOccasionId,
                    am.MessageId,
                    am.MessageContent,
                    am.LastExecutedAt,
                    am.ActivationConditions
                })
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Found {Count} active automated messages to process", allAutomatedMessages.Count);

            foreach (var am in allAutomatedMessages)
            {
                try
                {
                    // دریافت کامل AutomatedMessage برای پردازش (نیاز به Tracking دارد)
                    var automatedMessage = await context.AutomatedMessages
                        .FirstOrDefaultAsync(a => a.Id == am.Id, cancellationToken);
                    
                    if (automatedMessage == null)
                        continue;

                    await ProcessSingleAutomatedMessageAsync(context, automatedMessage, contactRepository, auditService, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing automated message {Id} for user {UserId}", 
                        am.Id, am.UserId);
                }
            }
        }

        private async Task ProcessSingleAutomatedMessageAsync(
            Api_Context context,
            AutomatedMessage automatedMessage,
            IContactRepository contactRepository,
            IAuditService auditService,
            CancellationToken cancellationToken)
        {
            var today = DateTime.UtcNow.Date;

            switch (automatedMessage.AutomationType)
            {
                case "Birthday":
                    await ProcessBirthdayAutomationAsync(context, automatedMessage, contactRepository, auditService, today, cancellationToken);
                    break;

                case "CashbackExpiry":
                    await ProcessCashbackExpiryAutomationAsync(context, automatedMessage, contactRepository, today, cancellationToken);
                    break;

                case "Welcome":
                    // Welcome messages are handled when contact is created, not in background
                    break;

                case "PurchaseReminder":
                    await ProcessPurchaseReminderAutomationAsync(context, automatedMessage, contactRepository, today, cancellationToken);
                    break;

                case "SpecialOccasion":
                    await ProcessSpecialOccasionAutomationAsync(context, automatedMessage, contactRepository, auditService, today, cancellationToken);
                    break;

                case "Custom":
                    await ProcessCustomAutomationAsync(context, automatedMessage, contactRepository, today, cancellationToken);
                    break;
            }
        }

        private async Task ProcessBirthdayAutomationAsync(
            Api_Context context,
            AutomatedMessage automatedMessage,
            IContactRepository contactRepository,
            IAuditService auditService,
            DateTime today,
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;

            // زمان‌بندی: قبل از ScheduledTime اجرا نشود؛ بعد از آن catch-up تا پایان روز (با dedupe)
            if (!automatedMessage.ScheduledTime.HasReachedScheduledTime(today, now))
            {
                var scheduledTimeUtc = today.CombineWithTime(automatedMessage.ScheduledTime!.Value);
                _logger.LogInformation(
                    "Birthday automation {Id} scheduled for {ScheduledTime} UTC, current time {CurrentTime} UTC — too early, skipping",
                    automatedMessage.Id, scheduledTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"), now.ToString("yyyy-MM-dd HH:mm:ss"));
                return;
            }

            // RepeatYearly=false → فقط یک‌بار در کل عمر (نه هر سال)
            var repeatYearly = ReadRepeatYearly(automatedMessage.ActivationConditions, defaultValue: true);

            _logger.LogInformation("Birthday automation {Id} - processing birthday messages for {Date} (UTC), RepeatYearly={RepeatYearly}",
                automatedMessage.Id, today.ToString("yyyy-MM-dd"), repeatYearly);

            var selectedScope = await ResolveSelectedContactScopeAsync(context, automatedMessage, cancellationToken);

            var contactsWithBirthdayToday = await context.Contacts
                .AsNoTracking()
                .Include(c => c.ContactNotebook)
                .Include(c => c.AdditionalInfo)
                .Where(c => !c.IsDeleted
                    && c.ContactNotebook.UserId == automatedMessage.UserId
                    && c.AdditionalInfo != null
                    && c.AdditionalInfo.DateOfBirth.HasValue)
                .ToListAsync(cancellationToken);

            var eligibleContacts = contactsWithBirthdayToday
                .Where(c => c.AdditionalInfo!.DateOfBirth!.EnsureUtc().IsBirthdayToday(today))
                .Where(c => IsContactInSelectedScope(c, selectedScope))
                .ToList();

            _logger.LogInformation(
                "Found {TotalCount} contacts with birth dates, {EligibleCount} have birthday today and match selection for automation {Id}",
                contactsWithBirthdayToday.Count, eligibleContacts.Count, automatedMessage.Id);

            foreach (var contact in eligibleContacts)
            {
                _logger.LogInformation(
                    "Contact {ContactId} ({Name}) - {MobileNumber} has birthday today (BirthDate: {BirthDate}) for automation {Id}",
                    contact.Id, contact.FullName, contact.MobileNumber,
                    contact.AdditionalInfo?.DateOfBirth?.ToString("yyyy-MM-dd"), automatedMessage.Id);
            }

            int queuedCount = 0;
            int failedCount = 0;
            int skippedCount = 0;

            var todayStart = today.Date;
            var todayEnd = todayStart.AddDays(1);

            // تکراری امروز
            var handledTodayIds = await context.AutomationExecutions
                .AsNoTracking()
                .Where(ae => ae.AutomatedMessageId == automatedMessage.Id
                    && ae.ContactId.HasValue
                    && ae.ExecutedAt >= todayStart
                    && ae.ExecutedAt < todayEnd)
                .Select(ae => ae.ContactId!.Value)
                .ToHashSetAsync(cancellationToken);

            // اگر RepeatYearly=false: هر مخاطبی که قبلاً (هر زمان) اجرا شده را حذف کن
            HashSet<int> everHandledIds = new();
            if (!repeatYearly)
            {
                everHandledIds = await context.AutomationExecutions
                    .AsNoTracking()
                    .Where(ae => ae.AutomatedMessageId == automatedMessage.Id
                        && ae.ContactId.HasValue
                        && (ae.Status == "Success" || ae.Status == "PendingApproval" || ae.Status == "Sent"))
                    .Select(ae => ae.ContactId!.Value)
                    .ToHashSetAsync(cancellationToken);
            }

            var contactsToQueue = eligibleContacts
                .Where(c => !handledTodayIds.Contains(c.Id))
                .Where(c => repeatYearly || !everHandledIds.Contains(c.Id))
                .ToList();

            skippedCount = eligibleContacts.Count - contactsToQueue.Count;

            if (contactsToQueue.Count > 0)
            {
                try
                {
                    queuedCount = await EnqueueAutomatedBatchForAdminApprovalAsync(
                        context, automatedMessage, contactsToQueue, auditService, cancellationToken);
                    _logger.LogInformation(
                        "Birthday automation {Id} queued {Count} recipients for admin approval",
                        automatedMessage.Id, queuedCount);
                }
                catch (Exception ex)
                {
                    failedCount = contactsToQueue.Count;
                    _logger.LogError(ex, "Failed to queue birthday automation {Id} for admin approval", automatedMessage.Id);
                }
            }

            _logger.LogInformation(
                "Birthday automation {Id} completed: {QueuedCount} queued, {FailedCount} failed, {SkippedCount} skipped, {TotalEligible} eligible for {Date} (UTC)",
                automatedMessage.Id, queuedCount, failedCount, skippedCount, eligibleContacts.Count, today.ToString("yyyy-MM-dd"));
        }

        private Task ProcessCashbackExpiryAutomationAsync(
            Api_Context context,
            AutomatedMessage automatedMessage,
            IContactRepository contactRepository,
            DateTime today,
            CancellationToken cancellationToken)
        {
            if (!automatedMessage.DaysBeforeEvent.HasValue)
                return Task.CompletedTask;

            var expiryDate = today.AddDays(automatedMessage.DaysBeforeEvent.Value);

            // TODO: نیاز به جدول Cashback یا WalletTransaction برای بررسی انقضا
            _logger.LogInformation("Processing CashbackExpiry automation {Id} for date {ExpiryDate}",
                automatedMessage.Id, expiryDate);
            return Task.CompletedTask;
        }

        private Task ProcessPurchaseReminderAutomationAsync(
            Api_Context context,
            AutomatedMessage automatedMessage,
            IContactRepository contactRepository,
            DateTime today,
            CancellationToken cancellationToken)
        {
            if (!automatedMessage.DaysBeforeEvent.HasValue)
                return Task.CompletedTask;

            var reminderDate = today.AddDays(-automatedMessage.DaysBeforeEvent.Value);

            // TODO: نیاز به جدول Purchase/Order برای بررسی آخرین خرید
            _logger.LogInformation("Processing PurchaseReminder automation {Id} for date {ReminderDate}",
                automatedMessage.Id, reminderDate);
            return Task.CompletedTask;
        }

        private async Task ProcessSpecialOccasionAutomationAsync(
            Api_Context context,
            AutomatedMessage automatedMessage,
            IContactRepository contactRepository,
            IAuditService auditService,
            DateTime today,
            CancellationToken cancellationToken)
        {
            if (!automatedMessage.SpecialOccasionId.HasValue)
                return;

            var now = DateTime.UtcNow;

            if (!automatedMessage.ScheduledTime.HasReachedScheduledTime(today, now))
            {
                var scheduledTimeUtc = today.CombineWithTime(automatedMessage.ScheduledTime!.Value);
                _logger.LogInformation(
                    "SpecialOccasion automation {Id} scheduled for {ScheduledTime} UTC, current {CurrentTime} UTC — too early, skipping",
                    automatedMessage.Id, scheduledTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"), now.ToString("yyyy-MM-dd HH:mm:ss"));
                return;
            }

            var specialOccasion = await context.SpecialOccasions
                .AsNoTracking()
                .FirstOrDefaultAsync(so => so.Id == automatedMessage.SpecialOccasionId.Value
                    && !so.IsDeleted
                    && so.IsActive, cancellationToken);

            if (specialOccasion == null)
                return;

            // مقایسه ماه/روز (مناسبت سالانه) — تاریخ با EnsureDateOnlyUtc ذخیره می‌شود
            var occasionDateUtc = specialOccasion.OccasionDate.EnsureDateOnlyUtc();
            if (!occasionDateUtc.IsSameMonthDay(today))
            {
                _logger.LogInformation(
                    "SpecialOccasion automation {Id} occasion date {OccasionDate} is not today {Today} (month/day) — skipping",
                    automatedMessage.Id, occasionDateUtc.ToString("yyyy-MM-dd"), today.ToString("yyyy-MM-dd"));
                return;
            }

            var selectedScope = await ResolveSelectedContactScopeAsync(context, automatedMessage, cancellationToken);

            var contacts = await context.Contacts
                .AsNoTracking()
                .Include(c => c.ContactNotebook)
                .Where(c => !c.IsDeleted && c.ContactNotebook.UserId == automatedMessage.UserId)
                .ToListAsync(cancellationToken);

            contacts = contacts.Where(c => IsContactInSelectedScope(c, selectedScope)).ToList();

            _logger.LogInformation("Processing SpecialOccasion automation {Id} for {Count} contacts (scoped)",
                automatedMessage.Id, contacts.Count);

            var todayStart = today.Date;
            var todayEnd = todayStart.AddDays(1);

            var handledContactIds = await context.AutomationExecutions
                .AsNoTracking()
                .Where(ae => ae.AutomatedMessageId == automatedMessage.Id
                    && ae.ContactId.HasValue
                    && ae.ExecutedAt >= todayStart
                    && ae.ExecutedAt < todayEnd)
                .Select(ae => ae.ContactId!.Value)
                .ToHashSetAsync(cancellationToken);

            var contactsToQueue = contacts
                .Where(c => !handledContactIds.Contains(c.Id))
                .ToList();

            var skippedCount = contacts.Count - contactsToQueue.Count;
            var queuedCount = 0;

            if (contactsToQueue.Count > 0)
            {
                queuedCount = await EnqueueAutomatedBatchForAdminApprovalAsync(
                    context, automatedMessage, contactsToQueue, auditService, cancellationToken);
            }

            _logger.LogInformation(
                "SpecialOccasion automation {Id} completed: {QueuedCount} queued for approval, {SkippedCount} skipped",
                automatedMessage.Id, queuedCount, skippedCount);
        }

        private Task ProcessCustomAutomationAsync(
            Api_Context context,
            AutomatedMessage automatedMessage,
            IContactRepository contactRepository,
            DateTime today,
            CancellationToken cancellationToken)
        {
            // TODO: پردازش شرایط سفارشی از ActivationConditions (JSON)
            _logger.LogInformation("Processing Custom automation {Id}", automatedMessage.Id);
            return Task.CompletedTask;
        }

        /// <summary>
        /// محدوده گیرندگان انتخاب‌شده از MessageSession (SelectionCriteria / RecipientsJson)
        /// </summary>
        private sealed class SelectedContactScope
        {
            public bool HasSelection { get; set; }
            public bool ApplyToAllContacts { get; set; } = true;
            public HashSet<int> ContactNotebookIds { get; set; } = new();
            public HashSet<int> ExcludedContactIds { get; set; } = new();
            public HashSet<int> ExplicitContactIds { get; set; } = new();
        }

        private static bool ReadRepeatYearly(string? activationConditionsJson, bool defaultValue)
        {
            if (string.IsNullOrWhiteSpace(activationConditionsJson))
                return defaultValue;

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(activationConditionsJson);
                if (doc.RootElement.TryGetProperty("repeatYearly", out var prop))
                {
                    if (prop.ValueKind == System.Text.Json.JsonValueKind.True) return true;
                    if (prop.ValueKind == System.Text.Json.JsonValueKind.False) return false;
                    if (prop.ValueKind == System.Text.Json.JsonValueKind.String
                        && bool.TryParse(prop.GetString(), out var b))
                        return b;
                }
            }
            catch
            {
                // ignore malformed JSON — use default
            }

            return defaultValue;
        }

        private async Task<SelectedContactScope> ResolveSelectedContactScopeAsync(
            Api_Context context,
            AutomatedMessage automatedMessage,
            CancellationToken cancellationToken)
        {
            if (!automatedMessage.MessageId.HasValue)
            {
                return new SelectedContactScope { HasSelection = false, ApplyToAllContacts = true };
            }

            var session = await context.MessageSessions
                .AsNoTracking()
                .Where(s => s.MessageId == automatedMessage.MessageId.Value
                    && s.UserId == automatedMessage.UserId
                    && !s.IsDeleted)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (session == null)
            {
                return new SelectedContactScope { HasSelection = false, ApplyToAllContacts = true };
            }

            var scope = new SelectedContactScope { HasSelection = true, ApplyToAllContacts = true };

            if (!string.IsNullOrWhiteSpace(session.SelectionCriteria))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(session.SelectionCriteria);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("ApplyToAllContacts", out var allProp)
                        || root.TryGetProperty("applyToAllContacts", out allProp))
                    {
                        scope.ApplyToAllContacts = allProp.ValueKind == System.Text.Json.JsonValueKind.True
                            || (allProp.ValueKind == System.Text.Json.JsonValueKind.String
                                && bool.TryParse(allProp.GetString(), out var b) && b);
                    }

                    scope.ContactNotebookIds = NotebookIdSelectionHelper.ReadFromJsonElement(root);

                    if (root.TryGetProperty("ExcludedContactIds", out var exProp)
                        || root.TryGetProperty("excludedContactIds", out exProp))
                    {
                        if (exProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var item in exProp.EnumerateArray())
                            {
                                if (item.TryGetInt32(out var id))
                                    scope.ExcludedContactIds.Add(id);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse SelectionCriteria for automation {Id}", automatedMessage.Id);
                }
            }

            // اگر دفترچه مشخص نیست ولی RecipientsJson داریم — از ContactId های eligible استفاده کن
            if (!scope.ApplyToAllContacts && scope.ContactNotebookIds.Count == 0
                && !string.IsNullOrWhiteSpace(session.RecipientsJson))
            {
                try
                {
                    var recipients = System.Text.Json.JsonSerializer.Deserialize<List<DTOs.Automation.RecipientItemForAutomatedMessageDto>>(
                        session.RecipientsJson);
                    scope.ExplicitContactIds = recipients?
                        .Where(r => r.ContactId.HasValue && r.IsEligible)
                        .Select(r => r.ContactId!.Value)
                        .ToHashSet() ?? new HashSet<int>();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse RecipientsJson for automation {Id}", automatedMessage.Id);
                }
            }

            return scope;
        }

        private static bool IsContactInSelectedScope(Contact contact, SelectedContactScope scope)
        {
            if (!scope.HasSelection || scope.ApplyToAllContacts)
            {
                return !scope.ExcludedContactIds.Contains(contact.Id);
            }

            if (scope.ExplicitContactIds.Count > 0)
                return scope.ExplicitContactIds.Contains(contact.Id);

            if (scope.ContactNotebookIds.Count > 0
                && !scope.ContactNotebookIds.Contains(contact.ContactNotebookId))
                return false;

            return !scope.ExcludedContactIds.Contains(contact.Id);
        }

        /// <summary>
        /// به‌جای ارسال مستقیم SMS، کمپین PendingApproval می‌سازد/به‌روز می‌کند تا از صف تأیید ادمین رد شود.
        /// شخصی‌سازی در ConfirmAndSend انجام می‌شود (IsPersonalized=true) تا از N+1 جلوگیری شود.
        /// </summary>
        /// <returns>تعداد گیرندگانی که به صف اضافه شدند</returns>
        private async Task<int> EnqueueAutomatedBatchForAdminApprovalAsync(
            Api_Context context,
            AutomatedMessage automatedMessage,
            List<Contact> contacts,
            IAuditService auditService,
            CancellationToken cancellationToken)
        {
            if (contacts.Count == 0)
                return 0;

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var todayStart = DateTime.UtcNow.Date;

                // کمپین بازِ امروز برای همین اتوماسیون (در صورت وجود، گیرندگان جدید را به همان اضافه کن)
                var existingCampaign = await context.MessageCampaigns
                    .Include(c => c.Recipients)
                    .FirstOrDefaultAsync(c => c.AutomatedMessageId == automatedMessage.Id
                        && !c.IsDeleted
                        && c.CreatedAt >= todayStart
                        && c.Status == "PendingApproval"
                        && c.AdminApprovalStatus == AdminApprovalStatuses.Pending,
                        cancellationToken);

                string messageContent = automatedMessage.MessageContent ?? string.Empty;
                Message? message = null;

                if (automatedMessage.MessageId.HasValue)
                {
                    message = await context.Messages
                        .FirstOrDefaultAsync(m => m.Id == automatedMessage.MessageId.Value && !m.IsDeleted, cancellationToken);

                    if (message != null)
                        messageContent = message.Content;
                }

                if (string.IsNullOrWhiteSpace(messageContent))
                {
                    _logger.LogWarning("No message content found for automated message {Id}", automatedMessage.Id);
                    await transaction.RollbackAsync(cancellationToken);
                    return 0;
                }

                if (message == null)
                {
                    await using var pricingScope = _serviceProvider.CreateAsyncScope();
                    var pricing = await pricingScope.ServiceProvider
                        .GetRequiredService<ISmsPricingService>()
                        .GetRuntimeAsync(cancellationToken);

                    int partsCount;
                    try
                    {
                        partsCount = SmsPartsCalculator.CalculateParts(messageContent, pricing.Rules);
                    }
                    catch (ArgumentException ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Automated message content exceeds max pages — AutomatedMessageId: {Id}",
                            automatedMessage.Id);
                        await transaction.RollbackAsync(cancellationToken);
                        return 0;
                    }

                    message = new Message
                    {
                        UserId = automatedMessage.UserId,
                        Title = automatedMessage.Title ?? $"پیام خودکار #{automatedMessage.Id}",
                        Content = messageContent,
                        CharacterCount = SmsPartsCalculator.CountMessageCharacters(messageContent, pricing.Rules),
                        PartsCount = partsCount,
                        IsPersonalized = true,
                        Status = "Ready",
                        CreatedAt = DateTime.UtcNow
                    };
                    await context.Messages.AddAsync(message, cancellationToken);
                    await context.SaveChangesAsync(cancellationToken);
                    automatedMessage.MessageId = message.Id;
                }
                else if (!message.IsPersonalized)
                {
                    message.IsPersonalized = true;
                    message.UpdatedAt = DateTime.UtcNow;
                }

                var now = DateTime.UtcNow;
                var previewContent = messageContent.Length > 4000 ? messageContent[..4000] : messageContent;

                MessageCampaign campaign;
                var isNewCampaign = existingCampaign == null;
                if (existingCampaign != null)
                {
                    campaign = existingCampaign;
                    var alreadyQueued = campaign.Recipients
                        .Where(r => r.ContactId.HasValue)
                        .Select(r => r.ContactId!.Value)
                        .ToHashSet();
                    contacts = contacts.Where(c => !alreadyQueued.Contains(c.Id)).ToList();
                    if (contacts.Count == 0)
                    {
                        await transaction.CommitAsync(cancellationToken);
                        return 0;
                    }
                }
                else
                {
                    campaign = new MessageCampaign
                    {
                        MessageId = message.Id,
                        UserId = automatedMessage.UserId,
                        Title = automatedMessage.Title ?? $"خودکار — {automatedMessage.AutomationType} #{automatedMessage.Id}",
                        SendType = "Automated",
                        AutomatedMessageId = automatedMessage.Id,
                        RecipientsCount = 0,
                        PartsCount = message.PartsCount,
                        Status = "PendingApproval",
                        AdminApprovalStatus = AdminApprovalStatuses.Pending,
                        IsActive = true,
                        CreatedAt = now
                    };
                    await context.MessageCampaigns.AddAsync(campaign, cancellationToken);
                }

                foreach (var contact in contacts)
                {
                    campaign.Recipients.Add(new MessageRecipient
                    {
                        ContactId = contact.Id,
                        MobileNumber = contact.MobileNumber,
                        FullName = contact.FullName,
                        Status = "Pending",
                        CreatedAt = now
                    });

                    await context.AutomationExecutions.AddAsync(new AutomationExecution
                    {
                        AutomatedMessageId = automatedMessage.Id,
                        ContactId = contact.Id,
                        ExecutedAt = now,
                        Status = "PendingApproval",
                        MessageContent = previewContent,
                        SentCount = 0
                    }, cancellationToken);
                }

                campaign.RecipientsCount = campaign.Recipients.Count;
                campaign.UpdatedAt = now;
                automatedMessage.LastExecutedAt = now;
                await context.SaveChangesAsync(cancellationToken);

                var approvalRequest = await context.SmsApprovalRequests
                    .FirstOrDefaultAsync(r => r.MessageCampaignId == campaign.Id
                        && r.Status == AdminApprovalStatuses.Pending
                        && !r.IsDeleted,
                        cancellationToken);

                if (approvalRequest != null)
                {
                    approvalRequest.ContentPreview = previewContent;
                    approvalRequest.TitlePreview = campaign.Title;
                    approvalRequest.RecipientsCount = campaign.RecipientsCount;
                    approvalRequest.UpdatedAt = now;
                }
                else
                {
                    await context.SmsApprovalRequests.AddAsync(new SmsApprovalRequest
                    {
                        UserId = automatedMessage.UserId,
                        RequestType = SmsApprovalRequestTypes.Campaign,
                        MessageCampaignId = campaign.Id,
                        MessageId = message.Id,
                        ContentPreview = previewContent,
                        TitlePreview = campaign.Title,
                        RecipientsCount = campaign.RecipientsCount,
                        Status = AdminApprovalStatuses.Pending,
                        CreatedAt = now
                    }, cancellationToken);
                }

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                await auditService.WriteAsync(new AuditEntry
                {
                    Category = AuditCategories.Message,
                    Action = isNewCampaign ? AuditActions.CampaignAutoSent : AuditActions.AutomatedMessageQueued,
                    EntityType = AuditEntityTypes.MessageCampaign,
                    EntityId = campaign.Id.ToString(),
                    ActorUserId = automatedMessage.UserId,
                    Source = AuditSources.Background,
                    After = new { automatedMessageId = automatedMessage.Id, addedRecipients = contacts.Count, totalRecipients = campaign.RecipientsCount }
                }, cancellationToken);

                _logger.LogInformation(
                    "Queued automated message {AutomationId} as campaign {CampaignId} (+{Added} recipients, total {Total}) for admin approval",
                    automatedMessage.Id, campaign.Id, contacts.Count, campaign.RecipientsCount);

                return contacts.Count;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }
}

