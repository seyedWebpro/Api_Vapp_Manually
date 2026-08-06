using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Text.Json;

namespace Api_Vapp.Services.BackgroundServices
{
    /// <summary>
    /// Background Service برای پردازش کش‌بک‌های زمان‌بندی شده
    /// هر 1 دقیقه یکبار کش‌بک‌های زمان‌بندی شده را بررسی و ارسال می‌کند
    /// تمام زمان‌ها به UTC محاسبه می‌شوند
    /// </summary>
    public class ScheduledCashbackBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ScheduledCashbackBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);
        public ScheduledCashbackBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<ScheduledCashbackBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("=== Scheduled Cashback Background Service Started at {Time} (UTC) ===", DateTime.UtcNow);

            // تأخیر اولیه برای اطمینان از آماده بودن سیستم
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var processingStartTime = DateTime.UtcNow;
                
                try
                {
                    _logger.LogDebug("=== شروع بررسی کش‌بک‌های زمان‌بندی شده - {Time} (UTC) ===", processingStartTime);
                    
                    await ProcessScheduledCashbacksAsync(stoppingToken);
                    await ProcessScheduledCashbackTransactionsAsync(stoppingToken);
                    
                    var processingDuration = (DateTime.UtcNow - processingStartTime).TotalSeconds;
                    _logger.LogDebug("=== پایان بررسی کش‌بک‌ها - مدت زمان: {Duration} ثانیه ===", processingDuration);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Scheduled Cashback Background Service is shutting down...");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطا در پردازش کش‌بک‌های زمان‌بندی شده - {Error}", ex.Message);
                }

                // انتظار تا چک بعدی
                try
                {
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            _logger.LogInformation("=== Scheduled Cashback Background Service Stopped at {Time} (UTC) ===", DateTime.UtcNow);
        }

        /// <summary>
        /// پردازش کش‌بک‌های زمان‌بندی شده (ارسال گروهی)
        /// </summary>
        private async Task ProcessScheduledCashbacksAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<Api_Context>();
            var userSmsBilling = scope.ServiceProvider.GetRequiredService<IUserSmsBillingService>();
            var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
            var smsPricing = scope.ServiceProvider.GetRequiredService<ISmsPricingService>();

            var now = DateTime.UtcNow;

            // دریافت کش‌بک‌های زمان‌بندی شده که زمانشان رسیده (فقط خواندنی - بدون Tracking)
            var scheduledCashbacks = await context.Cashbacks
                .AsNoTracking()
                .Where(c => !c.IsDeleted
                    && c.IsActive
                    && c.DepositTiming == CashbackDepositTiming.Scheduled
                    && c.ScheduleStatus == CashbackScheduleStatus.Pending
                    && c.ScheduledDepositDateTime.HasValue
                    && c.ScheduledDepositDateTime.Value <= now)
                .Select(c => new
                {
                    c.Id,
                    c.UserId,
                    c.Title,
                    c.ScheduledDepositDateTime,
                    c.TargetAudience,
                    c.TargetNotebookIds,
                    c.SendToSpecificTags,
                    c.TargetTagIds,
                    c.CashbackType,
                    c.FixedAmount,
                    c.Percentage,
                    c.MaxCashbackAmount,
                    c.ValidityDays
                })
                .ToListAsync(cancellationToken);

            if (!scheduledCashbacks.Any())
            {
                return;
            }

            _logger.LogInformation("=== یافت شد: {Count} کش‌بک زمان‌بندی شده آماده پردازش ===", scheduledCashbacks.Count);

            foreach (var c in scheduledCashbacks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var cashbackStartTime = DateTime.UtcNow;
                
                try
                {
                    // دریافت کامل Cashback برای پردازش (نیاز به Tracking دارد)
                    var cashback = await context.Cashbacks
                        .FirstOrDefaultAsync(cb => cb.Id == c.Id, cancellationToken);
                    
                    if (cashback == null)
                    {
                        _logger.LogWarning("Cashback {CashbackId} not found", c.Id);
                        continue;
                    }

                    var scheduledTime = cashback.ScheduledDepositDateTime!.Value;
                    var delaySeconds = (now - scheduledTime).TotalSeconds;
                    
                    _logger.LogInformation("=== شروع پردازش کش‌بک زمان‌بندی شده ===");
                    _logger.LogInformation("CashbackId: {CashbackId}, Title: {Title}, UserId: {UserId}",
                        cashback.Id, cashback.Title, cashback.UserId);
                    _logger.LogInformation("زمان برنامه‌ریزی شده (UTC): {ScheduledAt:yyyy-MM-dd HH:mm:ss}, زمان فعلی (UTC): {Now:yyyy-MM-dd HH:mm:ss}, تأخیر: {Delay:F2} ثانیه",
                        scheduledTime, now, delaySeconds);

                    // علامت‌گذاری به عنوان در حال پردازش
                    cashback.ScheduleStatus = CashbackScheduleStatus.Processing;
                    cashback.UpdatedAt = DateTime.UtcNow;
                    await context.SaveChangesAsync(cancellationToken);

                    // پردازش کش‌بک
                    var result = await ProcessSingleScheduledCashbackAsync(
                        context, userSmsBilling, smsPricing, cashback, cancellationToken);

                    // به‌روزرسانی وضعیت
                    cashback.ScheduleStatus = result.Success 
                        ? CashbackScheduleStatus.Completed 
                        : CashbackScheduleStatus.Failed;
                    cashback.LastScheduledProcessedAt = DateTime.UtcNow;
                    cashback.UpdatedAt = DateTime.UtcNow;

                    await context.SaveChangesAsync(cancellationToken);

                    var duration = (DateTime.UtcNow - cashbackStartTime).TotalSeconds;

                    if (result.Success)
                    {
                        await auditService.WriteAsync(new AuditEntry
                        {
                            Category = AuditCategories.Cashback,
                            Action = AuditActions.CashbackApplied,
                            EntityType = AuditEntityTypes.Cashback,
                            EntityId = cashback.Id.ToString(),
                            ActorUserId = cashback.UserId,
                            Source = AuditSources.Background,
                            After = new
                            {
                                totalContacts = result.TotalContacts,
                                successCount = result.SuccessCount,
                                failedCount = result.FailedCount,
                                totalAmount = result.TotalCashbackAmount
                            }
                        }, cancellationToken);

                        _logger.LogInformation("=== کش‌بک زمان‌بندی شده با موفقیت پردازش شد ===");
                        _logger.LogInformation("CashbackId: {CashbackId}, کل مخاطبین: {Total}, موفق: {Success}, ناموفق: {Failed}, مبلغ کل: {Amount:N0} تومان, مدت زمان: {Duration:F2} ثانیه",
                            cashback.Id, result.TotalContacts, result.SuccessCount, result.FailedCount, result.TotalCashbackAmount, duration);
                    }
                    else
                    {
                        _logger.LogWarning("=== خطا در پردازش کش‌بک زمان‌بندی شده ===");
                        _logger.LogWarning("CashbackId: {CashbackId}, خطا: {Error}, مدت زمان: {Duration:F2} ثانیه",
                            cashback.Id, result.ErrorMessage ?? "خطای نامشخص", duration);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطا در پردازش کش‌بک زمان‌بندی شده {CashbackId} برای کاربر {UserId}",
                        c.Id, c.UserId);

                    // علامت‌گذاری به عنوان ناموفق
                    try
                    {
                        var cashback = await context.Cashbacks
                            .FirstOrDefaultAsync(cb => cb.Id == c.Id, cancellationToken);
                        
                        if (cashback != null)
                    {
                        cashback.ScheduleStatus = CashbackScheduleStatus.Failed;
                        cashback.LastScheduledProcessedAt = DateTime.UtcNow;
                        cashback.UpdatedAt = DateTime.UtcNow;
                        await context.SaveChangesAsync(cancellationToken);
                        }
                    }
                    catch (Exception saveEx)
                    {
                        _logger.LogError(saveEx, "خطا در ذخیره وضعیت ناموفق کش‌بک {CashbackId}", c.Id);
                    }
                }
            }
        }

        /// <summary>
        /// پردازش یک کش‌بک زمان‌بندی شده
        /// </summary>
        private async Task<CashbackProcessResult> ProcessSingleScheduledCashbackAsync(
            Api_Context context,
            IUserSmsBillingService userSmsBilling,
            ISmsPricingService smsPricing,
            Cashback cashback,
            CancellationToken cancellationToken)
        {
            var result = new CashbackProcessResult();

            // دریافت مخاطبین هدف
            var contacts = await GetTargetContactsAsync(context, cashback.UserId, cashback, cancellationToken);

            if (!contacts.Any())
            {
                result.ErrorMessage = "هیچ مخاطبی برای ارسال کش‌بک یافت نشد";
                return result;
            }

            result.TotalContacts = contacts.Count;
            _logger.LogInformation("تعداد مخاطبین هدف: {Count} برای کش‌بک {CashbackId}", contacts.Count, cashback.Id);

            var pricing = await smsPricing.GetRuntimeAsync(cancellationToken);
            // کمبود موجودی نباید کل پردازش را fail کند — فقط پیامک‌ها soft-skip می‌شوند

            var now = DateTime.UtcNow;
            var successCount = 0;
            var failedCount = 0;
            var totalCashbackAmount = 0m;
            var smsSentCount = 0;
            decimal totalSmsCost = 0m;

            using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // مرحله 1: ایجاد تمام تراکنش‌های کش‌بک (بدون SaveChanges در foreach)
                var transactionsToProcess = new List<(CashbackTransaction transaction, Contact contact, decimal amount, string normalizedMobile)>();
                
                foreach (var contact in contacts)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        // محاسبه مبلغ کش‌بک
                        decimal cashbackAmount = CalculateCashbackAmount(cashback, null);

                        // نرمال‌سازی شماره موبایل
                        var normalizedMobile = NormalizePhoneNumber(contact.MobileNumber);
                        if (string.IsNullOrWhiteSpace(normalizedMobile))
                        {
                            _logger.LogWarning("شماره موبایل نامعتبر برای مخاطب {ContactId}: {Mobile}", 
                                contact.Id, contact.MobileNumber);
                            
                            // ایجاد تراکنش با وضعیت Failed
                            var failedTransaction = new CashbackTransaction
                            {
                                CashbackId = cashback.Id,
                                ContactId = contact.Id,
                                Amount = cashbackAmount,
                                Status = CashbackTransactionStatuses.Failed,
                                CreatedAt = now,
                                Description = "شماره موبایل نامعتبر"
                            };
                            await context.CashbackTransactions.AddAsync(failedTransaction, cancellationToken);
                            failedCount++;
                            continue;
                        }

                        // ایجاد تراکنش کش‌بک
                        var cashbackTransaction = new CashbackTransaction
                        {
                            CashbackId = cashback.Id,
                            ContactId = contact.Id,
                            Amount = cashbackAmount,
                            Status = CashbackTransactionStatuses.Pending,
                            CreatedAt = now,
                            Description = "کش‌بک زمان‌بندی شده"
                        };

                        await context.CashbackTransactions.AddAsync(cashbackTransaction, cancellationToken);
                        
                        // اضافه کردن به لیست برای پردازش بعدی
                        transactionsToProcess.Add((cashbackTransaction, contact, cashbackAmount, normalizedMobile));
                    }
                    catch (Exception ex)
                        {
                        _logger.LogError(ex, "خطا در ایجاد تراکنش کش‌بک برای مخاطب {ContactId}", contact.Id);
                            failedCount++;
                    }
                }

                // یکبار SaveChanges برای ذخیره تمام تراکنش‌ها
                            await context.SaveChangesAsync(cancellationToken);

                // مرحله 2: ارسال SMS و به‌روزرسانی وضعیت تراکنش‌ها
                foreach (var (cashbackTransaction, contact, cashbackAmount, normalizedMobile) in transactionsToProcess)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var message = GenerateCashbackMessage(cashback, cashbackAmount, cashbackTransaction.PurchaseAmount);
                        var sendResult = await userSmsBilling.TrySendAsync(
                            cashback.UserId,
                            normalizedMobile,
                            message,
                            SmsSourceModules.CashbackScheduled,
                            "ارسال کش‌بک زمان‌بندی شده",
                            $"هزینه پیامک کش‌بک زمان‌بندی‌شده «{cashback.Title}»",
                            cashback.Id,
                            cashback.Title,
                            cancellationToken);

                        // کش‌بک همیشه ثبت می‌شود؛ پیامک best-effort است
                        cashbackTransaction.Status = CashbackTransactionStatuses.Deposited;
                        cashbackTransaction.DepositedAt = DateTime.UtcNow;
                        if (sendResult.Sent)
                        {
                            cashbackTransaction.Description = "کش‌بک زمان‌بندی شده با موفقیت ارسال شد";
                            smsSentCount++;
                            totalSmsCost += sendResult.ChargedAmount > 0
                                ? sendResult.ChargedAmount
                                : sendResult.Cost;
                        }
                        else if (sendResult.SkippedInsufficientBalance)
                        {
                            cashbackTransaction.Description = "کش‌بک ثبت شد؛ پیامک به‌خاطر کمبود موجودی ارسال نشد";
                        }
                        else
                        {
                            cashbackTransaction.Description = "کش‌بک ثبت شد؛ ارسال پیامک ناموفق بود";
                        }

                        successCount++;
                        totalCashbackAmount += cashbackAmount;

                        _logger.LogDebug(
                            "کش‌بک پردازش شد - ContactId: {ContactId}, Mobile: {Mobile}, Amount: {Amount}, SmsSent={SmsSent}",
                            contact.Id, normalizedMobile, cashbackAmount, sendResult.Sent);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "خطا در ارسال SMS برای تراکنش کش‌بک {TransactionId}", cashbackTransaction.Id);
                        cashbackTransaction.Status = CashbackTransactionStatuses.Failed;
                        cashbackTransaction.Description = ControlledErrorHelper.SystemError;
                        failedCount++;
                    }
                }

                // یکبار SaveChanges برای به‌روزرسانی تمام وضعیت‌ها
                await context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                result.Success = successCount > 0;
                result.SuccessCount = successCount;
                result.FailedCount = failedCount;
                result.TotalCashbackAmount = totalCashbackAmount;
                result.TotalSmsCost = totalSmsCost;

                return result;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "خطا در تراکنش کش‌بک {CashbackId}", cashback.Id);
                result.ErrorMessage = ControlledErrorHelper.SystemError;
                return result;
            }
        }

        /// <summary>
        /// پردازش تراکنش‌های کش‌بک زمان‌بندی شده (تراکنش‌های منفرد)
        /// </summary>
        private async Task ProcessScheduledCashbackTransactionsAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<Api_Context>();
            var userSmsBilling = scope.ServiceProvider.GetRequiredService<IUserSmsBillingService>();
            var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();

            var now = DateTime.UtcNow;

            // دریافت تراکنش‌های زمان‌بندی شده که زمانشان رسیده (فقط خواندنی - بدون Tracking)
            var scheduledTransactions = await context.CashbackTransactions
                .AsNoTracking()
                .Where(ct => ct.Status == CashbackTransactionStatuses.Scheduled
                    && ct.ScheduledAt.HasValue
                    && ct.ScheduledAt.Value <= now)
                .Include(ct => ct.Cashback)
                    .ThenInclude(c => c.User)
                .Include(ct => ct.Contact)
                .Select(ct => new
                {
                    ct.Id,
                    ct.ContactId,
                    ct.CashbackId,
                    ct.Amount,
                    ct.ScheduledAt,
                    Contact = new { ct.Contact.Id, ct.Contact.MobileNumber },
                    Cashback = new { ct.Cashback.Id, ct.Cashback.UserId, ct.Cashback.CashbackType, ct.Cashback.Percentage, ct.Cashback.ValidityDays }
                })
                .ToListAsync(cancellationToken);

            if (!scheduledTransactions.Any())
            {
                return;
            }

            _logger.LogInformation("=== یافت شد: {Count} تراکنش کش‌بک زمان‌بندی شده آماده پردازش ===", scheduledTransactions.Count);

            foreach (var t in scheduledTransactions)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Declare transaction outside try block to make it accessible in catch block
                CashbackTransaction? transaction = null;

                try
                {
                    // دریافت کامل Transaction برای پردازش (نیاز به Tracking دارد)
                    transaction = await context.CashbackTransactions
                        .Include(ct => ct.Cashback)
                        .Include(ct => ct.Contact)
                        .FirstOrDefaultAsync(ct => ct.Id == t.Id, cancellationToken);
                    
                    if (transaction == null)
                    {
                        _logger.LogWarning("Transaction {TransactionId} not found", t.Id);
                        continue;
                    }

                    var transactionScheduledTime = transaction.ScheduledAt!.Value;
                    var transactionDelaySeconds = (now - transactionScheduledTime).TotalSeconds;
                    
                    _logger.LogInformation("پردازش تراکنش کش‌بک زمان‌بندی شده - TransactionId: {TransactionId}, ContactId: {ContactId}",
                        transaction.Id, transaction.ContactId);
                    _logger.LogDebug("زمان برنامه‌ریزی شده (UTC): {ScheduledAt:yyyy-MM-dd HH:mm:ss}, زمان فعلی (UTC): {Now:yyyy-MM-dd HH:mm:ss}, تأخیر: {Delay:F2} ثانیه",
                        transactionScheduledTime, now, transactionDelaySeconds);

                    // کمبود موجودی نباید تراکنش را fail کند — فقط پیامک soft-skip می‌شود

                    // نرمال‌سازی شماره موبایل
                    var normalizedMobile = NormalizePhoneNumber(transaction.Contact.MobileNumber);
                    if (string.IsNullOrWhiteSpace(normalizedMobile))
                    {
                        _logger.LogWarning("شماره موبایل نامعتبر برای تراکنش {TransactionId}, ContactId: {ContactId}, Mobile: {Mobile}",
                            transaction.Id, transaction.ContactId, transaction.Contact.MobileNumber);
                        transaction.Status = CashbackTransactionStatuses.Failed;
                        transaction.Description = "شماره موبایل نامعتبر";
                        await context.SaveChangesAsync(cancellationToken);
                        continue;
                    }

                    var message = GenerateCashbackMessage(transaction.Cashback, transaction.Amount, transaction.PurchaseAmount);
                    var sendResult = await userSmsBilling.TrySendAsync(
                        transaction.Cashback.UserId,
                        normalizedMobile,
                        message,
                        SmsSourceModules.CashbackScheduled,
                        "ارسال کش‌بک زمان‌بندی شده",
                        $"هزینه ارسال پیامک کش‌بک تراکنش #{transaction.Id}",
                        transaction.CashbackId,
                        transaction.Cashback.Title,
                        cancellationToken);

                    transaction.Status = CashbackTransactionStatuses.Deposited;
                    transaction.DepositedAt = DateTime.UtcNow;
                    transaction.Description = sendResult.Sent
                        ? "کش‌بک زمان‌بندی شده با موفقیت ارسال شد"
                        : sendResult.SkippedInsufficientBalance
                            ? "کش‌بک ثبت شد؛ پیامک به‌خاطر کمبود موجودی ارسال نشد"
                            : "کش‌بک ثبت شد؛ ارسال پیامک ناموفق بود";

                    await context.SaveChangesAsync(cancellationToken);

                    await auditService.WriteAsync(new AuditEntry
                    {
                        Category = AuditCategories.Cashback,
                        Action = AuditActions.CashbackApplied,
                        EntityType = AuditEntityTypes.Cashback,
                        EntityId = transaction.CashbackId.ToString(),
                        ActorUserId = transaction.Cashback.UserId,
                        Source = AuditSources.Background,
                        After = new
                        {
                            transactionId = transaction.Id,
                            amount = transaction.Amount,
                            contactId = transaction.ContactId,
                            smsSent = sendResult.Sent
                        }
                    }, cancellationToken);

                    _logger.LogInformation(
                        "تراکنش کش‌بک {TransactionId} پردازش شد - Mobile: {Mobile}, SmsSent={SmsSent}",
                        transaction.Id, normalizedMobile, sendResult.Sent);
                }
                catch (Exception ex)
                {
                    if (transaction != null)
                    {
                        _logger.LogError(ex, "خطا در پردازش تراکنش کش‌بک {TransactionId}", transaction.Id);

                        try
                        {
                            transaction.Status = CashbackTransactionStatuses.Failed;
                            transaction.Description = ControlledErrorHelper.SystemError;
                            await context.SaveChangesAsync(cancellationToken);
                        }
                        catch { }
                    }
                    else
                    {
                        _logger.LogError(ex, "خطا در پردازش تراکنش کش‌بک {TransactionId} (transaction is null)", t.Id);
                    }
                }
            }
        }

        /// <summary>
        /// دریافت مخاطبین هدف کش‌بک
        /// </summary>
        private async Task<List<Contact>> GetTargetContactsAsync(
            Api_Context context,
            int userId,
            Cashback cashback,
            CancellationToken cancellationToken)
        {
            var contacts = new List<Contact>();

            if (cashback.TargetAudience == CashbackTargetAudience.All)
            {
                var notebooks = await context.ContactNotebooks
                    .AsNoTracking()
                    .Where(cn => cn.UserId == userId && !cn.IsDeleted)
                    .Select(cn => cn.Id)
                    .ToListAsync(cancellationToken);

                contacts = await context.Contacts
                    .AsNoTracking()
                    .Where(c => notebooks.Contains(c.ContactNotebookId) && !c.IsDeleted)
                    .ToListAsync(cancellationToken);
            }
            else if (cashback.TargetAudience == CashbackTargetAudience.NewContacts)
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-15);
                var notebooks = await context.ContactNotebooks
                    .AsNoTracking()
                    .Where(cn => cn.UserId == userId && !cn.IsDeleted)
                    .Select(cn => cn.Id)
                    .ToListAsync(cancellationToken);

                contacts = await context.Contacts
                    .AsNoTracking()
                    .Where(c => notebooks.Contains(c.ContactNotebookId) &&
                           !c.IsDeleted &&
                           c.CreatedAt >= cutoffDate)
                    .ToListAsync(cancellationToken);
            }
            else if (cashback.TargetAudience == CashbackTargetAudience.SpecificNotebooks &&
                     !string.IsNullOrEmpty(cashback.TargetNotebookIds))
            {
                try
                {
                    var notebookIds = JsonSerializer.Deserialize<List<int>>(cashback.TargetNotebookIds);
                    if (notebookIds != null && notebookIds.Any())
                    {
                        contacts = await context.Contacts
                            .AsNoTracking()
                            .Where(c => notebookIds.Contains(c.ContactNotebookId) && !c.IsDeleted)
                            .ToListAsync(cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطا در deserialize کردن TargetNotebookIds برای کش‌بک {CashbackId}", cashback.Id);
                }
            }

            // فیلتر بر اساس تگ‌ها
            if (cashback.SendToSpecificTags && !string.IsNullOrEmpty(cashback.TargetTagIds))
            {
                try
                {
                    var tagIds = JsonSerializer.Deserialize<List<int>>(cashback.TargetTagIds);
                    if (tagIds != null && tagIds.Any())
                    {
                        var contactIdsWithTags = await context.ContactTags
                            .AsNoTracking()
                            .Where(ct => tagIds.Contains(ct.TagId))
                            .Select(ct => ct.ContactId)
                            .Distinct()
                            .ToListAsync(cancellationToken);

                        contacts = contacts.Where(c => contactIdsWithTags.Contains(c.Id)).ToList();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطا در فیلتر کردن مخاطبین بر اساس تگ برای کش‌بک {CashbackId}", cashback.Id);
                }
            }

            return contacts;
        }

        /// <summary>
        /// محاسبه مبلغ کش‌بک
        /// </summary>
        private static decimal CalculateCashbackAmount(Cashback cashback, decimal? purchaseAmount)
        {
            // محاسبه کش‌بک (پشتیبانی از ترکیب درصدی و ثابت)
            decimal percentageAmount = 0;
            decimal fixedAmount = 0;

            // محاسبه کش‌بک درصدی (اگر درصد موجود باشد)
            if (cashback.Percentage.HasValue && cashback.Percentage > 0)
            {
                if (purchaseAmount.HasValue && purchaseAmount > 0)
                {
                    percentageAmount = (purchaseAmount.Value * cashback.Percentage.Value) / 100;

                    // اعمال حداکثر مبلغ کش‌بک (فقط برای بخش درصدی)
                    if (cashback.MaxCashbackAmount.HasValue && percentageAmount > cashback.MaxCashbackAmount.Value)
                    {
                        percentageAmount = cashback.MaxCashbackAmount.Value;
                    }
                }
            }

            // اضافه کردن مبلغ ثابت (اگر موجود باشد)
            if (cashback.FixedAmount.HasValue && cashback.FixedAmount > 0)
            {
                fixedAmount = cashback.FixedAmount.Value;
            }

            // مجموع کش‌بک = درصدی + ثابت
            return percentageAmount + fixedAmount;
        }

        /// <summary>
        /// تولید متن پیامک کش‌بک
        /// </summary>
        private static string GenerateCashbackMessage(Cashback cashback, decimal amount, decimal? purchaseAmount = null)
        {
            var amountFormatted = $"{amount:N0} تومان";

            string message;
            
            // بررسی آیا هر دو درصد و مبلغ ثابت موجود است
            bool hasPercentage = cashback.Percentage.HasValue && cashback.Percentage > 0;
            bool hasFixedAmount = cashback.FixedAmount.HasValue && cashback.FixedAmount > 0;
            
            if (hasPercentage && hasFixedAmount)
            {
                // ترکیب درصدی و ثابت
                if (purchaseAmount.HasValue && purchaseAmount > 0)
                {
                    var percentageAmount = (purchaseAmount.Value * cashback.Percentage!.Value) / 100;
                    // اعمال حداکثر مبلغ کش‌بک (فقط برای بخش درصدی)
                    if (cashback.MaxCashbackAmount.HasValue && percentageAmount > cashback.MaxCashbackAmount.Value)
                    {
                        percentageAmount = cashback.MaxCashbackAmount.Value;
                    }
                    
                    var purchaseFormatted = $"{purchaseAmount.Value:N0} تومان";
                    var percentageFormatted = $"{percentageAmount:N0} تومان";
                    var fixedFormatted = $"{cashback.FixedAmount!.Value:N0} تومان";
                    
                    message = $"🎁 کش‌بک شما: {amountFormatted}\n" +
                             $"{cashback.Percentage}% از {purchaseFormatted} = {percentageFormatted}\n" +
                             $"مبلغ ثابت: {fixedFormatted}\n" +
                             $"مهلت استفاده: {cashback.ValidityDays} روز\n" +
                             "لغو11";
                }
                else
                {
                    var fixedFormatted = $"{cashback.FixedAmount!.Value:N0} تومان";
                    message = $"🎁 کش‌بک شما: {amountFormatted}\n" +
                             $"معادل {cashback.Percentage}% از خرید + {fixedFormatted} ثابت\n" +
                             $"مهلت استفاده: {cashback.ValidityDays} روز\n" +
                             "لغو11";
                }
            }
            else if (hasPercentage)
            {
                // فقط درصدی
                if (purchaseAmount.HasValue && purchaseAmount > 0)
                {
                    // نمایش محاسبه دقیق: مثلا "10% از 20,000 تومان = 2,000 تومان"
                    var purchaseFormatted = $"{purchaseAmount.Value:N0} تومان";
                    message = $"🎁 کش‌بک شما: {amountFormatted}\n" +
                             $"معادل {cashback.Percentage}% از {purchaseFormatted}\n" +
                             $"مهلت استفاده: {cashback.ValidityDays} روز\n" +
                             "لغو11";
                }
                else
                {
                    message = $"🎁 کش‌بک شما: {amountFormatted}\n" +
                             $"معادل {cashback.Percentage}% از خرید شما\n" +
                             $"مهلت استفاده: {cashback.ValidityDays} روز\n" +
                             "لغو11";
                }
            }
            else
            {
                // فقط مبلغ ثابت
                message = $"🎁 کش‌بک شما: {amountFormatted}\n" +
                         $"مهلت استفاده: {cashback.ValidityDays} روز\n" +
                         "لغو11";
            }

            return message;
        }

        /// <summary>
        /// نرمال‌سازی شماره موبایل به فرمت استاندارد (09xxxxxxxxx)
        /// </summary>
        private static string NormalizePhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return string.Empty;

            // حذف فاصله و کاراکترهای غیر عددی
            var normalized = new string(phoneNumber.Where(char.IsDigit).ToArray());

            // تبدیل به فرمت استاندارد (09xxxxxxxxx)
            if (normalized.StartsWith("98"))
            {
                normalized = "0" + normalized.Substring(2);
            }
            else if (normalized.StartsWith("9"))
            {
                normalized = "0" + normalized;
            }

            return normalized;
        }

        /// <summary>
        /// نتیجه پردازش کش‌بک
        /// </summary>
        private class CashbackProcessResult
        {
            public bool Success { get; set; }
            public int TotalContacts { get; set; }
            public int SuccessCount { get; set; }
            public int FailedCount { get; set; }
            public decimal TotalCashbackAmount { get; set; }
            public decimal TotalSmsCost { get; set; }
            public string? ErrorMessage { get; set; }
        }
    }
}





