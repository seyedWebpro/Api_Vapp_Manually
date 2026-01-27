using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Api_Vapp.Services.BackgroundServices
{
    /// <summary>
    /// Background Service برای ارسال خودکار پیام‌های زمان‌بندی شده (بدون کمپین)
    /// هر 1 دقیقه یکبار Session های زمان‌بندی شده را بررسی و ارسال می‌کند
    /// </summary>
    public class ScheduledMessageBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ScheduledMessageBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1); // هر 1 دقیقه یکبار

        public ScheduledMessageBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<ScheduledMessageBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Scheduled Message Background Service started");

            // تأخیر اولیه برای اطمینان از آماده بودن سیستم
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessScheduledMessagesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing scheduled messages");
                }

                // انتظار تا چک بعدی
                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("Scheduled Message Background Service stopped");
        }

        private async Task ProcessScheduledMessagesAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<Api_Context>();
            var messageService = scope.ServiceProvider.GetRequiredService<IMessageService>();
            var sessionRepository = scope.ServiceProvider.GetRequiredService<IMessageSessionRepository>();

            var now = DateTime.UtcNow;

            // دریافت تمام Session های فعال که زمان‌بندی شده‌اند (فقط خواندنی - بدون Tracking)
            var allSessions = await context.MessageSessions
                .AsNoTracking()
                .Where(s => !s.IsDeleted 
                    && !s.IsUsed
                    && (s.ExpiresAt == null || s.ExpiresAt > now))
                .Select(s => new
                {
                    s.Id,
                    s.MessageId,
                    s.UserId,
                    s.SelectionCriteria,
                    s.ExpiresAt
                })
                .ToListAsync(cancellationToken);

            var scheduledSessions = new List<(int sessionId, int messageId, int userId, DateTime scheduledAt)>();

            foreach (var s in allSessions)
            {
                try
                {
                    // خواندن اطلاعات زمان‌بندی از SelectionCriteria
                    if (string.IsNullOrEmpty(s.SelectionCriteria))
                        continue;

                    var criteria = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(s.SelectionCriteria);
                    if (criteria == null)
                        continue;

                    if (criteria.TryGetValue("SendType", out var sendTypeElement) 
                        && sendTypeElement.GetString() == "Scheduled"
                        && criteria.TryGetValue("ScheduledAt", out var scheduledAtElement))
                    {
                        var scheduledAtString = scheduledAtElement.GetString();
                        if (!string.IsNullOrEmpty(scheduledAtString))
                        {
                            DateTime scheduledAt;
                            
                            // ابتدا سعی می‌کنیم DateTimeOffset را parse کنیم (که timezone را حفظ می‌کند)
                            if (DateTimeOffset.TryParse(scheduledAtString, out var dateTimeOffset))
                            {
                                scheduledAt = dateTimeOffset.UtcDateTime;
                            }
                            // اگر DateTimeOffset parse نشد، از DateTime استفاده می‌کنیم
                            else if (DateTime.TryParse(scheduledAtString, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                            {
                                // اطمینان از UTC بودن
                                if (dt.Kind == DateTimeKind.Unspecified)
                                {
                                    // اگر Kind مشخص نیست، فرض می‌کنیم UTC است (چون در MessageService به UTC ذخیره شده)
                                    scheduledAt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                                }
                                else if (dt.Kind == DateTimeKind.Local)
                                {
                                    scheduledAt = dt.ToUniversalTime();
                                }
                                else
                                {
                                    scheduledAt = dt;
                                }
                            }
                            else
                            {
                                _logger.LogWarning("Failed to parse ScheduledAt for Session {SessionId}, Value: {Value}", 
                                    s.Id, scheduledAtString);
                                continue;
                            }
                            
                            if (scheduledAt <= now)
                            {
                                scheduledSessions.Add((s.Id, s.MessageId, s.UserId, scheduledAt));
                                _logger.LogInformation("Found scheduled session ready to send - SessionId: {SessionId}, ScheduledAt (UTC): {ScheduledAt}, Now (UTC): {Now}", 
                                    s.Id, scheduledAt, now);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Failed to parse ScheduledAt for Session {SessionId}, Value: {Value}", 
                                s.Id, scheduledAtString);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing SelectionCriteria for Session {SessionId}", s.Id);
                }
            }

            _logger.LogInformation("=== بررسی پیام‌های زمان‌بندی شده ===");
            _logger.LogInformation("زمان فعلی (UTC): {Now}, تعداد Session های بررسی شده: {TotalSessions}, تعداد آماده ارسال: {ReadyCount}", 
                now, allSessions.Count, scheduledSessions.Count);

            foreach (var (sessionId, messageId, userId, scheduledAt) in scheduledSessions)
            {
                var processingStartTime = DateTime.UtcNow;
                try
                {
                    // دریافت کامل Session برای پردازش (نیاز به Tracking دارد)
                    var session = await context.MessageSessions
                        .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
                    
                    if (session == null)
                    {
                        _logger.LogWarning("Session {SessionId} not found", sessionId);
                        continue;
                    }

                    _logger.LogInformation("=== شروع پردازش پیام زمان‌بندی شده ===");
                    _logger.LogInformation("SessionId: {SessionId}, MessageId: {MessageId}, UserId: {UserId}", 
                        session.Id, session.MessageId, session.UserId);
                    _logger.LogInformation("زمان برنامه‌ریزی شده (UTC): {ScheduledAt}, زمان فعلی (UTC): {Now}, تاخیر: {Delay} ثانیه", 
                        scheduledAt, now, (now - scheduledAt).TotalSeconds);

                    // خواندن تنظیمات از SelectionCriteria
                    var criteria = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(session.SelectionCriteria);
                    var sendDto = new DTOs.Message.SendDirectMessageDto
                    {
                        SendType = DTOs.Message.CampaignSendType.Quick, // برای ارسال استفاده می‌کنیم
                        ScheduledAt = null,
                        PreventDuplicate = criteria?.TryGetValue("PreventDuplicate", out var preventDuplicate) == true 
                            && preventDuplicate.GetBoolean(),
                        DuplicatePreventionHours = criteria?.TryGetValue("DuplicatePreventionHours", out var hours) == true 
                            ? hours.GetInt32() 
                            : 24,
                        SendToSpecificTags = criteria?.TryGetValue("SendToSpecificTags", out var sendToTags) == true 
                            && sendToTags.GetBoolean(),
                        SelectedTagIds = criteria?.TryGetValue("SelectedTagIds", out var tagIds) == true
                            ? JsonSerializer.Deserialize<List<int>>(tagIds.GetRawText())
                            : null
                    };

                    // ارسال پیام
                    var sendStartTime = DateTime.UtcNow;
                    _logger.LogInformation("در حال ارسال پیام زمان‌بندی شده - زمان شروع ارسال (UTC): {SendStartTime}", sendStartTime);
                    
                    var result = await messageService.SendDirectMessageAsync(session.UserId, session.MessageId, sendDto, session);
                    var sendEndTime = DateTime.UtcNow;
                    var sendDuration = (sendEndTime - sendStartTime).TotalSeconds;
                    
                    if (result.Success && result.Data != null)
                    {
                        // علامت‌گذاری Session به عنوان استفاده شده
                        session.IsUsed = true;
                        session.UpdatedAt = DateTime.UtcNow;
                        await sessionRepository.UpdateAsync(session);

                        var processingEndTime = DateTime.UtcNow;
                        var totalProcessingDuration = (processingEndTime - processingStartTime).TotalSeconds;
                        
                        _logger.LogInformation("=== پیام زمان‌بندی شده با موفقیت ارسال شد ===");
                        _logger.LogInformation("SessionId: {SessionId}, MessageId: {MessageId}, UserId: {UserId}", 
                            session.Id, session.MessageId, session.UserId);
                        _logger.LogInformation("زمان برنامه‌ریزی شده (UTC): {ScheduledAt}, زمان ارسال واقعی (UTC): {ActualSendTime}", 
                            scheduledAt, sendStartTime);
                        _logger.LogInformation("نتایج: ✅ ارسال موفق: {SentCount}, ❌ ارسال ناموفق: {FailedCount}, 💰 هزینه: {Cost} تومان", 
                            result.Data.SentCount, result.Data.FailedCount, result.Data.TotalCost);
                        _logger.LogInformation("مدت زمان ارسال: {SendDuration} ثانیه, مدت کل پردازش: {TotalDuration} ثانیه", 
                            sendDuration, totalProcessingDuration);
                        
                        if (result.Data.FailedNumbers != null && result.Data.FailedNumbers.Any())
                        {
                            _logger.LogWarning("شماره‌های ناموفق: {FailedNumbers}", string.Join(", ", result.Data.FailedNumbers));
                        }
                    }
                    else
                    {
                        var processingEndTime = DateTime.UtcNow;
                        var totalProcessingDuration = (processingEndTime - processingStartTime).TotalSeconds;
                        
                        _logger.LogError("=== خطا در ارسال پیام زمان‌بندی شده ===");
                        _logger.LogError("SessionId: {SessionId}, MessageId: {MessageId}, UserId: {UserId}", 
                            session.Id, session.MessageId, session.UserId);
                        _logger.LogError("زمان برنامه‌ریزی شده (UTC): {ScheduledAt}, زمان تلاش (UTC): {AttemptTime}", 
                            scheduledAt, sendStartTime);
                        _logger.LogError("خطا: {Error}, مدت زمان پردازش: {Duration} ثانیه", 
                            result.Message, totalProcessingDuration);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending scheduled message - SessionId: {SessionId}, MessageId: {MessageId}, UserId: {UserId}", 
                        sessionId, messageId, userId);
                }
            }
        }
    }
}

