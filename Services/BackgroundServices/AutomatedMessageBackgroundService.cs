using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Sms;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;

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

            int sentCount = 0;
            int failedCount = 0;
            int skippedCount = 0;

            foreach (var contact in eligibleContacts)
            {
                try
                {
                    // چک کردن جلوگیری از ارسال تکراری - آیا امروز برای این مخاطب و این پیام خودکار ارسال شده؟
                    // چک می‌کنیم برای همین AutomatedMessageId و ContactId (نه فقط Success) تا از ارسال تکراری جلوگیری کنیم
                    var todayStart = today.Date; // شروع روز جاری در UTC
                    var todayEnd = todayStart.AddDays(1); // پایان روز جاری در UTC

                    var alreadySentToday = await context.AutomationExecutions
                        .AnyAsync(ae => ae.AutomatedMessageId == automatedMessage.Id
                            && ae.ContactId == contact.Id
                            && ae.ExecutedAt >= todayStart
                            && ae.ExecutedAt < todayEnd,
                            cancellationToken);

                    if (alreadySentToday)
                    {
                        skippedCount++;
                        _logger.LogInformation("Birthday message skipped for contact {ContactId} ({Name}) - {MobileNumber} (already sent today {Date}) for automation {Id}",
                            contact.Id, contact.FullName, contact.MobileNumber, today.ToString("yyyy-MM-dd"), automatedMessage.Id);
                        continue;
                    }

                    // ارسال پیام تولد
                    await SendAutomatedMessageAsync(context, automatedMessage, contact, cancellationToken);

                    sentCount++;
                    _logger.LogInformation("Birthday message sent successfully to contact {ContactId} ({Name}) - {MobileNumber} for automation {Id}",
                        contact.Id, contact.FullName, contact.MobileNumber, automatedMessage.Id);
                }
                catch (Exception ex)
                {
                    failedCount++;
                    _logger.LogError(ex, "Failed to send birthday message to contact {ContactId} ({Name}) - {MobileNumber} for automation {Id}: {Error}",
                        contact.Id, contact.FullName, contact.MobileNumber, automatedMessage.Id, ex.Message);
                }
            }

            _logger.LogInformation("Birthday automation {Id} completed: {SentCount} sent, {FailedCount} failed, {SkippedCount} skipped (already sent today), {TotalEligible} eligible contacts processed for {Date} (UTC)",
                automatedMessage.Id, sentCount, failedCount, skippedCount, eligibleContacts.Count, today.ToString("yyyy-MM-dd"));
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

            int sentCount = 0;
            int skippedCount = 0;

            foreach (var contact in contacts)
            {
                // چک تکراری: آیا امروز برای این مخاطب و این پیام خودکار ارسال شده؟
                var alreadySentToday = await context.AutomationExecutions
                    .AnyAsync(ae => ae.AutomatedMessageId == automatedMessage.Id
                        && ae.ContactId == contact.Id
                        && ae.ExecutedAt >= todayStart
                        && ae.ExecutedAt < todayEnd,
                        cancellationToken);

                if (alreadySentToday)
                {
                    skippedCount++;
                    continue;
                }

                await SendAutomatedMessageAsync(context, automatedMessage, contact, cancellationToken);
                sentCount++;
            }

            _logger.LogInformation("SpecialOccasion automation {Id} completed: {SentCount} sent, {SkippedCount} skipped", 
                automatedMessage.Id, sentCount, skippedCount);
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

        private async Task SendAutomatedMessageAsync(
            Api_Context context,
            AutomatedMessage automatedMessage,
            Contact contact,
            CancellationToken cancellationToken)
        {
            try
            {
                // دریافت محتوای پیام
                string messageContent = automatedMessage.MessageContent ?? string.Empty;

                if (automatedMessage.MessageId.HasValue)
                {
                    var message = await context.Messages
                        .AsNoTracking()
                        .Where(m => m.Id == automatedMessage.MessageId.Value)
                        .Select(m => new { m.Id, m.Content })
                        .FirstOrDefaultAsync(cancellationToken);
                    
                    if (message != null)
                    {
                        messageContent = message.Content;
                        
                        // شخصی‌سازی پیام با اطلاعات مخاطب
                        messageContent = await PersonalizeMessageAsync(messageContent, contact, context);
                    }
                }
                else if (string.IsNullOrEmpty(messageContent))
                {
                    _logger.LogWarning("No message content found for automated message {Id}", automatedMessage.Id);
                    return;
                }

                // اضافه کردن 'لغو11' در انتهای پیامک (الزام API)
                if (!messageContent.TrimEnd().EndsWith("لغو11"))
                {
                    messageContent = $"{messageContent.TrimEnd()}\nلغو11";
                    _logger.LogDebug("متن 'لغو11' به پیام خودکار اضافه شد برای {Mobile}", contact.MobileNumber);
                }

                // ارسال پیام از طریق SMS Service (مشابه سیستم ارسال پیام زمان‌بندی شده)
                try
                {
                    var smsRequest = new DTOs.Sms.SendSmsRequestDto
                    {
                        Mobile = contact.MobileNumber,
                        Message = messageContent
                    };

                    var smsResult = await SendSmsWithRetryAsync(smsRequest, cancellationToken);

                    // بررسی موفقیت: Sid > 0 یعنی پیام ارسال شده (حتی اگر Status = 0 باشد)
                    // Status > 0 هم یعنی موفقیت
                    // Status < 0 یعنی خطا
                    bool isSuccess = smsResult.Success && smsResult.Data != null && 
                        (smsResult.Data.Sid > 0 || smsResult.Data.Status > 0);
                    
                    if (isSuccess)
                    {
                        using var trackScope = _serviceProvider.CreateScope();
                        var deliveryTracking = trackScope.ServiceProvider.GetRequiredService<ISmsDeliveryTrackingService>();
                        await deliveryTracking.TrackSuccessfulSendAsync(new SmsDeliveryTrackRequestDto
                        {
                            UserId = automatedMessage.UserId,
                            SourceModule = SmsSourceModules.AutomatedMessage,
                            SourceEntityId = automatedMessage.Id,
                            SourceEntityLabel = automatedMessage.Title ?? $"پیام خودکار #{automatedMessage.Id}",
                            Mobile = contact.MobileNumber,
                            Sid = smsResult.Data!.Sid
                        });
                    }

                    string status = isSuccess ? "Success" : "Failed";
                    string? errorMessage = isSuccess ? null : ControlledErrorHelper.SendFailed;

                    _logger.LogInformation(
                        "Sending automated message {AutomationId} to contact {ContactId} ({MobileNumber}). Content: {Content}, Status: {Status}",
                        automatedMessage.Id, contact.Id, contact.MobileNumber, messageContent, status);

                    // ثبت اجرا
                    var now = DateTime.UtcNow;
                    var execution = new AutomationExecution
                    {
                        AutomatedMessageId = automatedMessage.Id,
                        ContactId = contact.Id,
                        ExecutedAt = now,
                        Status = status,
                        MessageContent = messageContent,
                        SentCount = isSuccess ? 1 : 0,
                        ErrorMessage = errorMessage
                    };

                    await context.AutomationExecutions.AddAsync(execution, cancellationToken);

                    // به‌روزرسانی LastExecutedAt
                    automatedMessage.LastExecutedAt = now;

                    await context.SaveChangesAsync(cancellationToken);
                }
                catch (Exception smsEx)
                {
                    _logger.LogError(smsEx, "Error sending SMS for automated message {AutomationId} to contact {ContactId}", automatedMessage.Id, contact.Id);

                    // ثبت اجرا با خطا
                    var now = DateTime.UtcNow;
                    var execution = new AutomationExecution
                    {
                        AutomatedMessageId = automatedMessage.Id,
                        ContactId = contact.Id,
                        ExecutedAt = now,
                        Status = "Failed",
                        MessageContent = messageContent,
                        SentCount = 0,
                        ErrorMessage = ControlledErrorHelper.SendFailed
                    };

                    await context.AutomationExecutions.AddAsync(execution, cancellationToken);
                    await context.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending automated message {AutomationId} to contact {ContactId}", 
                    automatedMessage.Id, contact.Id);
            }
        }

        private async Task<string> PersonalizeMessageAsync(string messageContent, Contact contact, Api_Context context)
        {
            string result = messageContent;

            // دریافت نام کامل (FullName)
            var fullName = contact.FullName ?? "";

            // دریافت مبلغ کش بک (مجموع مبلغ‌های واریز شده) - فقط خواندنی
            var totalCashback = await context.CashbackTransactions
                .AsNoTracking()
                .Where(ct => ct.ContactId == contact.Id && ct.Status == "Deposited")
                .SumAsync(ct => (decimal?)ct.Amount) ?? 0;

            // دریافت آخرین تاریخ خرید (آخرین تراکنش کش بک) - فقط خواندنی
            var lastPurchaseDate = await context.CashbackTransactions
                .AsNoTracking()
                .Where(ct => ct.ContactId == contact.Id)
                .OrderByDescending(ct => ct.CreatedAt)
                .Select(ct => (DateTime?)ct.CreatedAt)
                .FirstOrDefaultAsync();

            // دریافت نام برند
            var brandName = contact.Brand ?? "";

            // تاریخ عضویت (تاریخ ایجاد مخاطب)
            var membershipDate = contact.CreatedAt;

            // جایگزینی placeholder ها با استفاده از Regex
            
            // {{نام}} — نام کامل مخاطب
            result = ReplacePlaceholder(result, "{{نام}}", fullName);
            result = ReplacePlaceholder(result, "{{name}}", fullName, StringComparison.OrdinalIgnoreCase);
            
            // {{مبلغ کش بک}} — مجموع مبلغ‌های کش‌بک
            result = ReplacePlaceholder(result, "{{مبلغ کش بک}}", FormatAmount(totalCashback));
            result = ReplacePlaceholder(result, "{{cashback amount}}", FormatAmount(totalCashback), StringComparison.OrdinalIgnoreCase);
            result = ReplacePlaceholder(result, "{{cashbackamount}}", FormatAmount(totalCashback), StringComparison.OrdinalIgnoreCase);
            
            // {{نام برند}} — نام برند
            result = ReplacePlaceholder(result, "{{نام برند}}", brandName);
            result = ReplacePlaceholder(result, "{{brand name}}", brandName, StringComparison.OrdinalIgnoreCase);
            result = ReplacePlaceholder(result, "{{brandname}}", brandName, StringComparison.OrdinalIgnoreCase);
            
            // {{تاریخ عضویت}} — تاریخ عضویت مخاطب
            result = ReplacePlaceholder(result, "{{تاریخ عضویت}}", FormatPersianDate(membershipDate));
            result = ReplacePlaceholder(result, "{{membership date}}", FormatPersianDate(membershipDate), StringComparison.OrdinalIgnoreCase);
            result = ReplacePlaceholder(result, "{{membershipdate}}", FormatPersianDate(membershipDate), StringComparison.OrdinalIgnoreCase);
            
            // {{تاریخ خرید}} — تاریخ آخرین خرید
            result = ReplacePlaceholder(result, "{{تاریخ خرید}}", lastPurchaseDate.HasValue ? FormatPersianDate(lastPurchaseDate.Value) : "");
            result = ReplacePlaceholder(result, "{{purchase date}}", lastPurchaseDate.HasValue ? FormatPersianDate(lastPurchaseDate.Value) : "", StringComparison.OrdinalIgnoreCase);
            result = ReplacePlaceholder(result, "{{purchasedate}}", lastPurchaseDate.HasValue ? FormatPersianDate(lastPurchaseDate.Value) : "", StringComparison.OrdinalIgnoreCase);

            // پشتیبانی از فرمت قدیمی با یک آکولاد
            result = ReplacePlaceholder(result, "{نام}", fullName);
            result = ReplacePlaceholder(result, "{مبلغ کش بک}", FormatAmount(totalCashback));
            result = ReplacePlaceholder(result, "{نام برند}", brandName);
            result = ReplacePlaceholder(result, "{تاریخ عضویت}", FormatPersianDate(membershipDate));
            result = ReplacePlaceholder(result, "{تاریخ خرید}", lastPurchaseDate.HasValue ? FormatPersianDate(lastPurchaseDate.Value) : "");

            // پشتیبانی از فرمت‌های قدیمی (برای سازگاری با کد قبلی)
            result = result.Replace("{{FirstName}}", fullName);
            result = result.Replace("{{LastName}}", "");
            result = result.Replace("{{FullName}}", fullName);
            result = result.Replace("{{Brand}}", brandName);
            result = result.Replace("{{MobileNumber}}", contact.MobileNumber);

            return result;
        }

        private string ReplacePlaceholder(string text, string placeholder, string value, StringComparison comparison = StringComparison.Ordinal)
        {
            // Escape کردن کاراکترهای خاص در placeholder برای استفاده در Regex
            var escapedPlaceholder = System.Text.RegularExpressions.Regex.Escape(placeholder);
            var escapedValue = value;
            
            // جایگزینی با Regex برای جایگزینی همه موارد
            var pattern = escapedPlaceholder;
            return System.Text.RegularExpressions.Regex.Replace(text, pattern, escapedValue, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        }

        private string FormatAmount(decimal amount)
        {
            return $"{amount:N0} تومان";
        }

        private string FormatPersianDate(DateTime date)
        {
            try
            {
                var persianCalendar = new System.Globalization.PersianCalendar();
                var year = persianCalendar.GetYear(date);
                var month = persianCalendar.GetMonth(date);
                var day = persianCalendar.GetDayOfMonth(date);

                var monthNames = new[] { "", "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند" };

                return $"{day} {monthNames[month]} {year}";
            }
            catch
            {
                // در صورت خطا، تاریخ میلادی را برمی‌گرداند
                return date.ToString("yyyy/MM/dd");
            }
        }

        /// <summary>
        /// ارسال SMS با Retry Mechanism و Exponential Backoff
        /// </summary>
        private async Task<ApiResponse<DTOs.Sms.SendSmsResponseDto>> SendSmsWithRetryAsync(
            DTOs.Sms.SendSmsRequestDto request,
            CancellationToken cancellationToken,
            int maxRetries = 3,
            int initialDelayMs = 1000)
        {
            var lastException = (Exception?)null;
            var lastResult = (ApiResponse<DTOs.Sms.SendSmsResponseDto>?)null;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    if (attempt > 0)
                    {
                        // Exponential Backoff: delay = initialDelay * 2^(attempt-1)
                        var delayMs = initialDelayMs * (int)Math.Pow(2, attempt - 1);
                        _logger.LogInformation("Retrying SMS send - Attempt: {Attempt}/{MaxRetries}, Delay: {Delay}ms, Mobile: {Mobile}",
                            attempt + 1, maxRetries + 1, delayMs, request.Mobile);
                        await Task.Delay(delayMs, cancellationToken);
                    }

                    // Resolve SMS service from scoped container
                    using var scope = _serviceProvider.CreateScope();
                    var smsService = scope.ServiceProvider.GetRequiredService<ISmsService>();

                    var result = await smsService.SendSmsAsync(request);

                    // اگر ارسال موفق بود، نتیجه را برمی‌گردانیم
                    // Sid > 0 یعنی پیام ارسال شده (حتی اگر Status = 0 باشد)
                    // Status > 0 هم یعنی موفقیت
                    bool isSuccess = result.Success && result.Data != null && 
                        (result.Data.Sid > 0 || result.Data.Status > 0);
                    
                    if (isSuccess)
                    {
                        if (attempt > 0)
                        {
                            _logger.LogInformation("SMS sent successfully after {Attempt} retries - Mobile: {Mobile}",
                                attempt + 1, request.Mobile);
                        }
                        return result;
                    }

                    // بررسی خطاهای غیرقابل Retry
                    if (result.Data != null)
                    {
                        var status = result.Data.Status;
                        var message = result.Data.Message ?? "";

                        // خطاهای غیرقابل Retry:
                        // 1. Status < 0 (خطاهای API)
                        // 2. Status = 0 با پیام‌های خاص (مثل "پیام تکراری")
                        // 3. Status = 0 با پیام‌های خطای مشخص
                        bool isNonRetryable = false;

                        if (status < 0)
                        {
                            isNonRetryable = true;
                        }
                        else if (status == 0)
                        {
                            // بررسی پیام‌های خطای غیرقابل Retry
                            var lowerMessage = message.ToLower();
                            if (lowerMessage.Contains("تکراری") ||
                                lowerMessage.Contains("duplicate") ||
                                lowerMessage.Contains("مجاز به ارسال پیام تکراری") ||
                                lowerMessage.Contains("شماره نامعتبر") ||
                                lowerMessage.Contains("invalid") ||
                                lowerMessage.Contains("blacklist") ||
                                lowerMessage.Contains("مشترک در لیست سیاه"))
                            {
                                isNonRetryable = true;
                            }
                        }

                        if (isNonRetryable)
                        {
                            _logger.LogWarning("SMS send failed with non-retryable error - Mobile: {Mobile}, Status: {Status}, Message: {Message}",
                                request.Mobile, status, message);
                            return result;
                        }
                    }

                    lastResult = result;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogWarning(ex, "SMS send attempt {Attempt} failed - Mobile: {Mobile}", attempt + 1, request.Mobile);

                    // Check if cancellation was requested
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            // اگر همه تلاش‌ها ناموفق بود
            if (lastResult != null)
            {
                return lastResult;
            }

            // اگر Exception داشتیم
            if (lastException != null)
            {
                return ApiResponse<DTOs.Sms.SendSmsResponseDto>.InternalServerError(ControlledErrorHelper.SmsFailed);
            }

            return ApiResponse<DTOs.Sms.SendSmsResponseDto>.InternalServerError("خطای ناشناخته در ارسال SMS");
        }
    }
}

