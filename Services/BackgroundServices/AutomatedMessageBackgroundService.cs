using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
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
                    am.LastExecutedAt
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

                    await ProcessSingleAutomatedMessageAsync(context, automatedMessage, contactRepository, cancellationToken);
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
            CancellationToken cancellationToken)
        {
            var today = DateTime.UtcNow.Date;

            switch (automatedMessage.AutomationType)
            {
                case "Birthday":
                    await ProcessBirthdayAutomationAsync(context, automatedMessage, contactRepository, today, cancellationToken);
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
                    await ProcessSpecialOccasionAutomationAsync(context, automatedMessage, contactRepository, today, cancellationToken);
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
            DateTime today,
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;

            // چک کردن زمان‌بندی ارسال (ScheduledTime)
            if (automatedMessage.ScheduledTime.HasValue)
            {
                // ScheduledTime به صورت TimeSpan ذخیره می‌شود (ساعت و دقیقه در روز)
                var scheduledTimeSpan = automatedMessage.ScheduledTime.Value;

                // ایجاد زمان برنامه‌ریزی شده برای امروز به UTC
                // today قبلاً UTC است، پس فقط TimeSpan را اضافه می‌کنیم
                var scheduledTimeUtc = today.CombineWithTime(scheduledTimeSpan);

                // بررسی اینکه آیا زمان فعلی در بازه زمانی مناسب برای ارسال است
                // استفاده از toleranceMinutes: 2 برای جلوگیری از ارسال تکراری
                if (!scheduledTimeUtc.IsWithinScheduleWindow(now, toleranceMinutes: 2))
                {
                    _logger.LogInformation("Birthday automation {Id} scheduled for {ScheduledTime} UTC, current time {CurrentTime} UTC, skipping (not within 2 minute window)",
                        automatedMessage.Id, scheduledTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"), now.ToString("yyyy-MM-dd HH:mm:ss"));
                    return;
                }

                _logger.LogInformation("Birthday automation {Id} is within scheduled time window (scheduled: {ScheduledTime} UTC, current: {CurrentTime} UTC)",
                    automatedMessage.Id, scheduledTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"), now.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            else
            {
                _logger.LogInformation("Birthday automation {Id} has no scheduled time, processing immediately at {CurrentTime} UTC",
                    automatedMessage.Id, now.ToString("yyyy-MM-dd HH:mm:ss"));
            }

            _logger.LogInformation("Birthday automation {Id} - processing birthday messages for {Date} (UTC)",
                automatedMessage.Id, today.ToString("yyyy-MM-dd"));

            // دریافت تمام مخاطبینی که امروز تولد دارند (با چک دقیق تاریخ تولد به UTC)
            // استفاده از AsNoTracking برای Query فقط خواندنی
            var contactsWithBirthdayToday = await context.Contacts
                .AsNoTracking()
                .Include(c => c.ContactNotebook)
                .Include(c => c.AdditionalInfo)
                .Where(c => !c.IsDeleted
                    && c.ContactNotebook.UserId == automatedMessage.UserId
                    && c.AdditionalInfo != null
                    && c.AdditionalInfo.DateOfBirth.HasValue)
                .ToListAsync(cancellationToken);

            // فیلتر کردن مخاطبینی که دقیقاً امروز تولد دارند (بر اساس UTC)
            // از متد IsBirthdayToday استفاده می‌کنیم که فقط ماه و روز را مقایسه می‌کند
            // توجه: در Where قبلی چک شده که DateOfBirth.HasValue است، پس null نیست
            var eligibleContacts = contactsWithBirthdayToday
                .Where(c => c.AdditionalInfo!.DateOfBirth!.EnsureUtc().IsBirthdayToday(today))
                .ToList();

            _logger.LogInformation("Found {TotalCount} contacts with birth dates, {EligibleCount} have birthday today ({Month}/{Day}) for automation {Id}",
                contactsWithBirthdayToday.Count, eligibleContacts.Count, today.Month, today.Day, automatedMessage.Id);

            // لاگ کردن جزئیات مخاطبین واجد شرایط
            foreach (var contact in eligibleContacts)
            {
                _logger.LogInformation("Contact {ContactId} ({Name}) - {MobileNumber} has birthday today (BirthDate: {BirthDate}) for automation {Id}",
                    contact.Id, contact.FullName, contact.MobileNumber,
                    contact.AdditionalInfo?.DateOfBirth?.ToString("yyyy-MM-dd"), automatedMessage.Id);
            }

            int queuedCount = 0;
            int failedCount = 0;
            int skippedCount = 0;

            var todayStart = today.Date;
            var todayEnd = todayStart.AddDays(1);

            // یک کوئری به‌جای N+1 برای چک تکراری امروز
            var handledContactIds = await context.AutomationExecutions
                .AsNoTracking()
                .Where(ae => ae.AutomatedMessageId == automatedMessage.Id
                    && ae.ContactId.HasValue
                    && ae.ExecutedAt >= todayStart
                    && ae.ExecutedAt < todayEnd)
                .Select(ae => ae.ContactId!.Value)
                .ToHashSetAsync(cancellationToken);

            var contactsToQueue = eligibleContacts
                .Where(c => !handledContactIds.Contains(c.Id))
                .ToList();

            skippedCount = eligibleContacts.Count - contactsToQueue.Count;

            if (contactsToQueue.Count > 0)
            {
                try
                {
                    queuedCount = await EnqueueAutomatedBatchForAdminApprovalAsync(
                        context, automatedMessage, contactsToQueue, cancellationToken);
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

            _logger.LogInformation("Birthday automation {Id} completed: {QueuedCount} queued for approval, {FailedCount} failed, {SkippedCount} skipped (already handled today), {TotalEligible} eligible contacts processed for {Date} (UTC)",
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
            // فعلاً فقط ساختار کلی را می‌گذاریم
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
            // فعلاً فقط ساختار کلی را می‌گذاریم
            _logger.LogInformation("Processing PurchaseReminder automation {Id} for date {ReminderDate}", 
                automatedMessage.Id, reminderDate);
            return Task.CompletedTask;
        }

        private async Task ProcessSpecialOccasionAutomationAsync(
            Api_Context context,
            AutomatedMessage automatedMessage,
            IContactRepository contactRepository,
            DateTime today,
            CancellationToken cancellationToken)
        {
            if (!automatedMessage.SpecialOccasionId.HasValue)
                return;

            var now = DateTime.UtcNow;

            // چک کردن زمان‌بندی ارسال (ScheduledTime)
            if (automatedMessage.ScheduledTime.HasValue)
            {
                var scheduledTimeSpan = automatedMessage.ScheduledTime.Value;
                var scheduledTimeUtc = today.CombineWithTime(scheduledTimeSpan);

                if (!scheduledTimeUtc.IsWithinScheduleWindow(now, toleranceMinutes: 2))
                {
                    _logger.LogInformation("SpecialOccasion automation {Id} scheduled for {ScheduledTime} UTC, current time {CurrentTime} UTC, skipping",
                        automatedMessage.Id, scheduledTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"), now.ToString("yyyy-MM-dd HH:mm:ss"));
                    return;
                }
            }

            var specialOccasion = await context.SpecialOccasions
                .AsNoTracking()
                .FirstOrDefaultAsync(so => so.Id == automatedMessage.SpecialOccasionId.Value, cancellationToken);

            if (specialOccasion == null)
                return;

            // مقایسه تاریخ مناسبت با تاریخ امروز (UTC)
            var occasionDateUtc = specialOccasion.OccasionDate.EnsureUtc();

            if (occasionDateUtc.Date != today)
                return;

            // دریافت تمام مخاطبین کاربر (فقط خواندنی - بدون Tracking)
            var contacts = await context.Contacts
                .AsNoTracking()
                .Include(c => c.ContactNotebook)
                .Where(c => !c.IsDeleted && c.ContactNotebook.UserId == automatedMessage.UserId)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Processing SpecialOccasion automation {Id} for {Count} contacts", 
                automatedMessage.Id, contacts.Count);

            var todayStart = today.Date;
            var todayEnd = todayStart.AddDays(1);

            // یک کوئری به‌جای N+1
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
                    context, automatedMessage, contactsToQueue, cancellationToken);
            }

            _logger.LogInformation("SpecialOccasion automation {Id} completed: {QueuedCount} queued for approval, {SkippedCount} skipped", 
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
        /// به‌جای ارسال مستقیم SMS، کمپین PendingApproval می‌سازد/به‌روز می‌کند تا از صف تأیید ادمین رد شود.
        /// شخصی‌سازی در ConfirmAndSend انجام می‌شود (IsPersonalized=true) تا از N+1 جلوگیری شود.
        /// </summary>
        /// <returns>تعداد گیرندگانی که به صف اضافه شدند</returns>
        private async Task<int> EnqueueAutomatedBatchForAdminApprovalAsync(
            Api_Context context,
            AutomatedMessage automatedMessage,
            List<Contact> contacts,
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
                    int partsCount;
                    try
                    {
                        partsCount = SmsPartsCalculator.CalculateParts(messageContent);
                    }
                    catch (ArgumentException)
                    {
                        partsCount = 10;
                    }

                    message = new Message
                    {
                        UserId = automatedMessage.UserId,
                        Title = automatedMessage.Title ?? $"پیام خودکار #{automatedMessage.Id}",
                        Content = messageContent,
                        CharacterCount = messageContent.Length,
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

