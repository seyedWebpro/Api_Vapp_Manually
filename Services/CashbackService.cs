using Api_Vapp.Data;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Cashback;
using Api_Vapp.DTOs.Sms;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;

namespace Api_Vapp.Services
{
    /// <summary>
    /// پیاده‌سازی سرویس کش‌بک
    /// </summary>
    public class CashbackService : ICashbackService
    {
        private readonly Api_Context _context;
        private readonly ICashbackRepository _cashbackRepository;
        private readonly ICashbackTransactionRepository _cashbackTransactionRepository;
        private readonly ICashbackDraftRepository _cashbackDraftRepository;
        private readonly IContactRepository _contactRepository;
        private readonly IContactNotebookRepository _notebookRepository;
        private readonly IWalletService _walletService;
        private readonly ISmsService _smsService;
        private readonly ILogger<CashbackService> _logger;
        private const decimal CostPerSms = 160; // هزینه هر پیامک
        private const int DraftExpirationHours = 24; // draft به مدت 24 ساعت معتبر است

        public CashbackService(
            Api_Context context,
            ICashbackRepository cashbackRepository,
            ICashbackTransactionRepository cashbackTransactionRepository,
            ICashbackDraftRepository cashbackDraftRepository,
            IContactRepository contactRepository,
            IContactNotebookRepository notebookRepository,
            IWalletService walletService,
            ISmsService smsService,
            ILogger<CashbackService> logger)
        {
            _context = context;
            _cashbackRepository = cashbackRepository;
            _cashbackTransactionRepository = cashbackTransactionRepository;
            _cashbackDraftRepository = cashbackDraftRepository;
            _contactRepository = contactRepository;
            _notebookRepository = notebookRepository;
            _walletService = walletService;
            _smsService = smsService;
            _logger = logger;
        }

        public async Task<ApiResponse<CashbackListDto>> GetCashbacksAsync(int userId, int pageNumber = 1, int pageSize = 10, bool? isActive = null)
        {
            try
            {
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                var cashbacks = await _cashbackRepository.GetByUserIdAsync(userId, pageNumber, pageSize, isActive);
                var totalCount = await _cashbackRepository.GetCountByUserIdAsync(userId, isActive);
                var activeCount = await _cashbackRepository.GetCountByUserIdAsync(userId, true);
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var cashbackDtos = new List<CashbackDto>();
                foreach (var cashback in cashbacks)
                {
                    cashbackDtos.Add(await MapToCashbackDtoAsync(cashback));
                }

                var result = new CashbackListDto
                {
                    Cashbacks = cashbackDtos,
                    TotalCount = totalCount,
                    ActiveCount = activeCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                return ApiResponse<CashbackListDto>.CreateSuccess(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت لیست کش‌بک‌های کاربر {UserId}", userId);
                throw;
            }
        }

        public async Task<ApiResponse<List<CashbackDto>>> GetActiveCashbacksAsync(int userId)
        {
            try
            {
                var cashbacks = await _cashbackRepository.GetActiveByUserIdAsync(userId);
                var cashbackDtos = new List<CashbackDto>();
                foreach (var cashback in cashbacks)
                {
                    cashbackDtos.Add(await MapToCashbackDtoAsync(cashback));
                }
                return ApiResponse<List<CashbackDto>>.CreateSuccess(cashbackDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت کش‌بک‌های فعال کاربر {UserId}", userId);
                throw;
            }
        }

        public async Task<ApiResponse<CashbackDto>> GetCashbackByIdAsync(int id, int userId)
        {
            try
            {
                var cashback = await _cashbackRepository.GetByIdAndUserIdAsync(id, userId);
                if (cashback == null)
                {
                    return ApiResponse<CashbackDto>.NotFound("کش‌بک یافت نشد");
                }

                return ApiResponse<CashbackDto>.CreateSuccess(await MapToCashbackDtoAsync(cashback));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت کش‌بک {CashbackId} برای کاربر {UserId}", id, userId);
                throw;
            }
        }

        public async Task<ApiResponse<CashbackDto>> CreateCashbackAsync(int userId, CreateCashbackDto createDto)
        {
            try
            {
                // اگر draftId ارسال شده، داده‌ها را از draft بخوان
                if (!string.IsNullOrEmpty(createDto.DraftId))
                {
                    var draft = await _cashbackDraftRepository.GetActiveByDraftIdAsync(createDto.DraftId, userId);
                    if (draft == null)
                    {
                        return ApiResponse<CashbackDto>.BadRequest("Draft یافت نشد یا منقضی شده است");
                    }

                    var step1Dto = JsonSerializer.Deserialize<CashbackStep1Dto>(draft.Step1Data);
                    var step2Dto = !string.IsNullOrEmpty(draft.Step2Data) 
                        ? JsonSerializer.Deserialize<CashbackStep2Dto>(draft.Step2Data) 
                        : null;
                    var step3Settings = !string.IsNullOrEmpty(draft.Step3Data)
                        ? JsonSerializer.Deserialize<SaveCashbackStep3SettingsDto>(draft.Step3Data)
                        : null;

                    if (step1Dto == null)
                    {
                        return ApiResponse<CashbackDto>.BadRequest("خطا در خواندن داده‌های draft");
                    }

                    if (step2Dto == null)
                    {
                        return ApiResponse<CashbackDto>.BadRequest("مرحله 2 هنوز تکمیل نشده است");
                    }

                    // پر کردن createDto از draft
                    createDto.CashbackType = step1Dto.CashbackType;
                    createDto.Percentage = step1Dto.Percentage;
                    createDto.FixedAmount = step1Dto.FixedAmount;
                    createDto.ValidityDays = step1Dto.ValidityDays;
                    createDto.TargetAudience = step2Dto.TargetAudience;
                    createDto.TargetNotebookIds = step2Dto.TargetNotebookIds;

                    // اگر Step3Settings وجود دارد، از آن استفاده کن (فقط اگر در createDto تنظیم نشده باشد)
                    if (step3Settings != null)
                    {
                        // زمان واریز (اگر در createDto تنظیم نشده باشد)
                        if (createDto.ScheduledDepositDateTime == null && step3Settings.ScheduledDepositDateTime.HasValue)
                        {
                            createDto.ScheduledDepositDateTime = step3Settings.ScheduledDepositDateTime;
                        }

                        // فیلتر تگ (اگر در createDto تنظیم نشده باشد)
                        if (!createDto.SendToSpecificTags && step3Settings.SendToSpecificTags)
                        {
                            createDto.SendToSpecificTags = step3Settings.SendToSpecificTags;
                            createDto.TargetTagIds = step3Settings.TargetTagIds;
                        }

                        // وضعیت (اگر در createDto تنظیم نشده باشد)
                        if (createDto.IsActive == true && !step3Settings.IsActive)
                        {
                            createDto.IsActive = step3Settings.IsActive;
                        }
                    }
                }

                // اعتبارسنجی نوع کش‌بک و مقادیر مربوطه
                if (createDto.CashbackType == CashbackTypes.Percentage)
                {
                    if (!createDto.Percentage.HasValue || createDto.Percentage <= 0 || createDto.Percentage > 50)
                    {
                        return ApiResponse<CashbackDto>.BadRequest("درصد کش‌بک باید بین 1 تا 50 باشد");
                    }
                }
                else if (createDto.CashbackType == CashbackTypes.FixedAmount)
                {
                    if (!createDto.FixedAmount.HasValue || createDto.FixedAmount < 1000 || createDto.FixedAmount > 10000000)
                    {
                        return ApiResponse<CashbackDto>.BadRequest("مبلغ کش‌بک باید بین 1,000 تا 10,000,000 تومان باشد");
                    }
                }
                else
                {
                    return ApiResponse<CashbackDto>.BadRequest("نوع کش‌بک نامعتبر است");
                }

                // اعتبارسنجی نوع مخاطبین
                if (createDto.TargetAudience == CashbackTargetAudience.SpecificNotebooks)
                {
                    if (createDto.TargetNotebookIds == null || !createDto.TargetNotebookIds.Any())
                    {
                        return ApiResponse<CashbackDto>.BadRequest("حداقل یک دفترچه باید انتخاب شود");
                    }

                    // بررسی مالکیت و حذف نشدن دفترچه‌ها
                    foreach (var notebookId in createDto.TargetNotebookIds)
                    {
                        var notebook = await _notebookRepository.GetByIdAsync(notebookId);
                        if (notebook == null || notebook.UserId != userId || notebook.IsDeleted)
                        {
                            return ApiResponse<CashbackDto>.BadRequest($"دفترچه با شناسه {notebookId} یافت نشد، متعلق به شما نیست یا حذف شده است");
                        }
                    }
                }

                // اعتبارسنجی TargetTagIds
                if (createDto.SendToSpecificTags && createDto.TargetTagIds != null && createDto.TargetTagIds.Any())
                {
                    var tags = await _context.MessageTags
                        .Where(t => createDto.TargetTagIds.Contains(t.Id) && t.UserId == userId && !t.IsDeleted && t.IsActive)
                        .ToListAsync();
                    
                    if (tags.Count != createDto.TargetTagIds.Count)
                    {
                        var invalidTagIds = createDto.TargetTagIds.Except(tags.Select(t => t.Id));
                        return ApiResponse<CashbackDto>.BadRequest($"تگ‌های زیر یافت نشدند، متعلق به شما نیستند یا حذف شده‌اند: {string.Join(", ", invalidTagIds)}");
                    }
                }

                // تنظیم زمان واریز
                TimeSpan? scheduledTime = null;
                if (createDto.DepositTiming == CashbackDepositTiming.Scheduled && !string.IsNullOrEmpty(createDto.ScheduledDepositTime))
                {
                    if (TimeSpan.TryParse(createDto.ScheduledDepositTime, out var parsedTime))
                    {
                        scheduledTime = parsedTime;
                    }
                }

                var now = DateTime.UtcNow;

                // تنظیم وضعیت زمان‌بندی
                string scheduleStatus = CashbackScheduleStatus.None;
                DateTime? scheduledDepositDateTime = null;

                if (createDto.DepositTiming == CashbackDepositTiming.Scheduled)
                {
                    if (createDto.ScheduledDepositDateTime.HasValue)
                    {
                        // تبدیل به UTC اگر نیاز است
                        scheduledDepositDateTime = createDto.ScheduledDepositDateTime.Value.Kind == DateTimeKind.Utc
                            ? createDto.ScheduledDepositDateTime.Value
                            : createDto.ScheduledDepositDateTime.Value.ToUniversalTime();

                        // بررسی اینکه زمان آینده باشد
                        if (scheduledDepositDateTime <= now)
                        {
                            return ApiResponse<CashbackDto>.BadRequest("زمان واریز زمان‌بندی شده باید در آینده باشد");
                        }

                        scheduleStatus = CashbackScheduleStatus.Pending;
                    }
                }

                // اعتبارسنجی ValidityDays
                if (createDto.ValidityDays < 1 || createDto.ValidityDays > 365)
                {
                    return ApiResponse<CashbackDto>.BadRequest("مدت اعتبار باید بین 1 تا 365 روز باشد");
                }

                // بررسی تداخل کش‌بک‌های فعال برای دفترچه‌های انتخاب شده (اطلاع‌رسانی)
                if (createDto.TargetAudience == CashbackTargetAudience.SpecificNotebooks && 
                    createDto.TargetNotebookIds != null && createDto.TargetNotebookIds.Any())
                {
                    var activeCashbacksForNotebooks = await _context.Cashbacks
                        .Where(c => c.UserId == userId && 
                                   c.IsActive && 
                                   !c.IsDeleted &&
                                   c.TargetAudience == CashbackTargetAudience.SpecificNotebooks &&
                                   !string.IsNullOrEmpty(c.TargetNotebookIds) &&
                                   (c.EndDate == null || c.EndDate >= now))
                        .ToListAsync();

                    var conflictingCashbacks = new List<string>();
                    foreach (var activeCashback in activeCashbacksForNotebooks)
                    {
                        try
                        {
                            if (string.IsNullOrEmpty(activeCashback.TargetNotebookIds))
                            {
                                continue;
                            }

                            var activeNotebookIds = JsonSerializer.Deserialize<List<int>>(activeCashback.TargetNotebookIds);
                            if (activeNotebookIds != null && activeNotebookIds.Any())
                            {
                                var hasOverlap = activeNotebookIds.Any(id => createDto.TargetNotebookIds.Contains(id));
                                if (hasOverlap)
                                {
                                    conflictingCashbacks.Add($"'{activeCashback.Title}' (شناسه: {activeCashback.Id})");
                                }
                            }
                        }
                        catch
                        {
                            // ignore deserialization errors
                        }
                    }

                    if (conflictingCashbacks.Any())
                    {
                        var message = $"توجه: برای دفترچه‌های انتخاب شده، کش‌بک‌های فعال زیر وجود دارند: {string.Join("، ", conflictingCashbacks)}. " +
                                     "می‌توانید ادامه دهید، اما توصیه می‌شود ابتدا کش‌بک‌های قبلی را بررسی کنید.";
                        // فقط لاگ می‌کنیم - خطا نمی‌دهیم (طبق تصمیم 4)
                        _logger.LogWarning("تداخل کش‌بک‌ها برای کاربر {UserId}: {Message}", userId, message);
                    }
                }

                var cashback = new Cashback
                {
                    UserId = userId,
                    Title = createDto.Title,
                    Description = createDto.Description,
                    CashbackType = createDto.CashbackType,
                    Percentage = createDto.Percentage,
                    FixedAmount = createDto.FixedAmount,
                    MaxCashbackAmount = createDto.MaxCashbackAmount,
                    MinPurchaseAmount = createDto.MinPurchaseAmount,
                    ValidityDays = createDto.ValidityDays,
                    StartDate = now,
                    EndDate = now.AddDays(createDto.ValidityDays),
                    DepositTiming = createDto.DepositTiming,
                    ScheduledDepositTime = scheduledTime,
                    ScheduledDepositDateTime = scheduledDepositDateTime,
                    ScheduleStatus = scheduleStatus,
                    TargetAudience = createDto.TargetAudience,
                    TargetNotebookIds = createDto.TargetNotebookIds != null ? JsonSerializer.Serialize(createDto.TargetNotebookIds) : null,
                    TargetTagIds = createDto.TargetTagIds != null ? JsonSerializer.Serialize(createDto.TargetTagIds) : null,
                    SendToSpecificTags = createDto.SendToSpecificTags,
                    IsActive = createDto.IsActive,
                    CreatedAt = now
                };

                await _context.Cashbacks.AddAsync(cashback);
                await _context.SaveChangesAsync();

                _logger.LogInformation("کش‌بک جدید با شناسه {CashbackId} برای کاربر {UserId} ایجاد شد", cashback.Id, userId);

                // حذف draft بعد از ایجاد موفق کش‌بک
                if (!string.IsNullOrEmpty(createDto.DraftId))
                {
                    try
                    {
                        await _cashbackDraftRepository.DeleteAsync(createDto.DraftId, userId);
                        _logger.LogInformation("Draft با شناسه {DraftId} بعد از ایجاد کش‌بک حذف شد", createDto.DraftId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "خطا در حذف draft بعد از ایجاد کش‌بک - DraftId: {DraftId}", createDto.DraftId);
                        // خطا نمی‌دهیم، فقط لاگ می‌کنیم
                    }
                }

                return ApiResponse<CashbackDto>.CreateSuccess(await MapToCashbackDtoAsync(cashback), "کش‌بک با موفقیت ایجاد شد", 201);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ایجاد کش‌بک برای کاربر {UserId}", userId);
                throw;
            }
        }

        public async Task<ApiResponse<CashbackDto>> UpdateCashbackAsync(int id, int userId, UpdateCashbackDto updateDto)
        {
            try
            {
                var cashback = await _cashbackRepository.GetByIdAndUserIdAsync(id, userId);
                if (cashback == null)
                {
                    return ApiResponse<CashbackDto>.NotFound("کش‌بک یافت نشد");
                }

                // به‌روزرسانی فیلدها
                if (!string.IsNullOrWhiteSpace(updateDto.Title))
                {
                    cashback.Title = updateDto.Title;
                }

                if (updateDto.Description != null)
                {
                    cashback.Description = updateDto.Description;
                }

                if (updateDto.Percentage.HasValue)
                {
                    if (cashback.CashbackType == CashbackTypes.Percentage)
                    {
                        if (updateDto.Percentage <= 0 || updateDto.Percentage > 50)
                        {
                            return ApiResponse<CashbackDto>.BadRequest("درصد کش‌بک باید بین 1 تا 50 باشد");
                        }
                        cashback.Percentage = updateDto.Percentage;
                    }
                    else
                    {
                        return ApiResponse<CashbackDto>.BadRequest("نمی‌توان درصد را برای کش‌بک مبلغ ثابت تنظیم کرد");
                    }
                }

                if (updateDto.FixedAmount.HasValue)
                {
                    if (cashback.CashbackType == CashbackTypes.FixedAmount)
                    {
                        if (updateDto.FixedAmount < 1000 || updateDto.FixedAmount > 10000000)
                        {
                            return ApiResponse<CashbackDto>.BadRequest("مبلغ کش‌بک باید بین 1,000 تا 10,000,000 تومان باشد");
                        }
                        cashback.FixedAmount = updateDto.FixedAmount;
                    }
                    else
                    {
                        return ApiResponse<CashbackDto>.BadRequest("نمی‌توان مبلغ ثابت را برای کش‌بک درصدی تنظیم کرد");
                    }
                }

                if (updateDto.MaxCashbackAmount.HasValue)
                {
                    if (updateDto.MaxCashbackAmount < 0 || updateDto.MaxCashbackAmount > 10000000)
                    {
                        return ApiResponse<CashbackDto>.BadRequest("حداکثر مبلغ کش‌بک باید بین 0 تا 10,000,000 تومان باشد");
                    }
                    cashback.MaxCashbackAmount = updateDto.MaxCashbackAmount;
                }

                if (updateDto.MinPurchaseAmount.HasValue)
                {
                    if (updateDto.MinPurchaseAmount < 0 || updateDto.MinPurchaseAmount > 100000000)
                    {
                        return ApiResponse<CashbackDto>.BadRequest("حداقل مبلغ خرید باید بین 0 تا 100,000,000 تومان باشد");
                    }
                    cashback.MinPurchaseAmount = updateDto.MinPurchaseAmount;
                }

                if (updateDto.ValidityDays.HasValue)
                {
                    if (updateDto.ValidityDays < 1 || updateDto.ValidityDays > 365)
                    {
                        return ApiResponse<CashbackDto>.BadRequest("مدت اعتبار باید بین 1 تا 365 روز باشد");
                    }
                    
                    cashback.ValidityDays = updateDto.ValidityDays.Value;
                    var newEndDate = cashback.StartDate.AddDays(updateDto.ValidityDays.Value);
                    
                    // بررسی اینکه EndDate جدید از الان کمتر نباشد (اگر کش‌بک فعال است)
                    if (cashback.IsActive && newEndDate < DateTime.UtcNow)
                    {
                        return ApiResponse<CashbackDto>.BadRequest("نمی‌توان مدت اعتبار را به گونه‌ای تغییر داد که تاریخ انقضا در گذشته باشد");
                    }
                    
                    cashback.EndDate = newEndDate;
                }

                if (updateDto.SendToSpecificTags.HasValue)
                {
                    cashback.SendToSpecificTags = updateDto.SendToSpecificTags.Value;
                }

                if (updateDto.TargetTagIds != null)
                {
                    cashback.TargetTagIds = JsonSerializer.Serialize(updateDto.TargetTagIds);
                }

                cashback.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("کش‌بک {CashbackId} برای کاربر {UserId} به‌روزرسانی شد", id, userId);

                return ApiResponse<CashbackDto>.CreateSuccess(await MapToCashbackDtoAsync(cashback), "کش‌بک با موفقیت به‌روزرسانی شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در به‌روزرسانی کش‌بک {CashbackId} برای کاربر {UserId}", id, userId);
                throw;
            }
        }

        public async Task<ApiResponse<CashbackDto>> ToggleStatusAsync(int id, int userId, bool isActive)
        {
            try
            {
                var cashback = await _cashbackRepository.GetByIdAndUserIdAsync(id, userId);
                if (cashback == null)
                {
                    return ApiResponse<CashbackDto>.NotFound("کش‌بک یافت نشد");
                }

                cashback.IsActive = isActive;
                cashback.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                var statusText = isActive ? "فعال" : "غیرفعال";
                _logger.LogInformation("کش‌بک {CashbackId} برای کاربر {UserId} {Status} شد", id, userId, statusText);

                return ApiResponse<CashbackDto>.CreateSuccess(await MapToCashbackDtoAsync(cashback), $"کش‌بک با موفقیت {statusText} شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در تغییر وضعیت کش‌بک {CashbackId} برای کاربر {UserId}", id, userId);
                throw;
            }
        }

        public async Task<ApiResponse<bool>> DeleteCashbackAsync(int id, int userId)
        {
            try
            {
                var cashback = await _cashbackRepository.GetByIdAndUserIdAsync(id, userId);
                if (cashback == null)
                {
                    return ApiResponse<bool>.NotFound("کش‌بک یافت نشد");
                }

                // Soft Delete
                cashback.IsDeleted = true;
                cashback.IsActive = false;
                cashback.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("کش‌بک {CashbackId} برای کاربر {UserId} حذف شد", id, userId);

                return ApiResponse<bool>.CreateSuccess(true, "کش‌بک با موفقیت حذف شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در حذف کش‌بک {CashbackId} برای کاربر {UserId}", id, userId);
                throw;
            }
        }

        public async Task<ApiResponse<CashbackFinalSummaryDto>> GetCashbackFinalSummaryAsync(
            int userId, 
            CashbackStep1Dto step1Dto, 
            CashbackStep2Dto step2Dto, 
            CashbackStep3Dto step3Dto)
        {
            try
            {
                // محاسبه تعداد مخاطبین
                int contactsCount = 0;

                if (step2Dto.TargetAudience == CashbackTargetAudience.All)
                {
                    var notebooks = await _context.ContactNotebooks
                        .Where(cn => cn.UserId == userId && !cn.IsDeleted)
                        .Select(cn => cn.Id)
                        .ToListAsync();

                    contactsCount = await _context.Contacts
                        .Where(c => notebooks.Contains(c.ContactNotebookId) && !c.IsDeleted)
                        .CountAsync();
                }
                else if (step2Dto.TargetAudience == CashbackTargetAudience.NewContacts)
                {
                    var cutoffDate = DateTime.UtcNow.AddDays(-15);
                    var notebooks = await _context.ContactNotebooks
                        .Where(cn => cn.UserId == userId && !cn.IsDeleted)
                        .Select(cn => cn.Id)
                        .ToListAsync();

                    contactsCount = await _context.Contacts
                        .Where(c => notebooks.Contains(c.ContactNotebookId) && 
                               !c.IsDeleted && 
                               c.CreatedAt >= cutoffDate)
                        .CountAsync();
                }
                else if (step2Dto.TargetAudience == CashbackTargetAudience.SpecificNotebooks && 
                         step2Dto.TargetNotebookIds != null)
                {
                    contactsCount = await _context.Contacts
                        .Where(c => step2Dto.TargetNotebookIds.Contains(c.ContactNotebookId) && !c.IsDeleted)
                        .CountAsync();
                }

                // محاسبه هزینه تخمینی (هزینه ارسال پیامک)
                decimal costPerPart = 160; // هزینه هر پیامک
                decimal estimatedTotalCost = contactsCount * costPerPart;

                // بررسی موجودی کیف پول
                // غیرفعال شده - دیگر کیف پول چک نمی‌شود
                // var balance = await _walletService.GetBalanceAsync(userId);
                // var hasSufficientBalance = balance >= estimatedTotalCost;
                const decimal currentWalletBalance = 0m;
                var hasSufficientBalance = true; // همیشه کافی است

                // تعیین نوع کش‌بک
                string cashbackTypeDescription = step1Dto.CashbackType == CashbackTypes.Percentage
                    ? $"درصدی ({step1Dto.Percentage}%)"
                    : $"مبلغ ثابت ({step1Dto.FixedAmount:N0} تومان)";

                // تعیین زمان اجرا
                string executionTime;
                if (step3Dto.ScheduledDepositDateTime.HasValue)
                {
                    var persianDate = ToPersianDate(step3Dto.ScheduledDepositDateTime.Value);
                    executionTime = $"زمان‌بندی شده: {persianDate}";
                }
                else
                {
                    executionTime = "فوری";
                }

                var summary = new CashbackFinalSummaryDto
                {
                    CashbackType = step1Dto.CashbackType,
                    CashbackTypeDescription = cashbackTypeDescription,
                    ExecutionTime = executionTime,
                    ContactsCount = contactsCount,
                    CostPerPart = costPerPart,
                    EstimatedTotalCost = estimatedTotalCost,
                    FormattedEstimatedCost = $"{estimatedTotalCost:N0} تومان",
                    WalletStatus = hasSufficientBalance ? "موجودی کافی است" : "موجودی ناکافی",
                    HasSufficientBalance = hasSufficientBalance,
                    CurrentWalletBalance = currentWalletBalance,
                    FormattedWalletBalance = $"{currentWalletBalance:N0} تومان"
                };

                return ApiResponse<CashbackFinalSummaryDto>.CreateSuccess(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در محاسبه خلاصه نهایی کش‌بک برای کاربر {UserId}", userId);
                throw;
            }
        }

        public async Task<ApiResponse<CashbackSummaryDto>> GetCashbackSummaryAsync(
            int userId,
            GetCashbackSummaryRequestDto request)
        {
            try
            {
                CashbackStep1Dto step1Dto;
                CashbackStep2Dto step2Dto;

                SaveCashbackStep3SettingsDto? step3Settings = null;

                // اگر draftId ارسال شده، از draft استفاده کن
                if (!string.IsNullOrEmpty(request.DraftId))
                {
                    var draft = await _cashbackDraftRepository.GetActiveByDraftIdAsync(request.DraftId, userId);
                    if (draft == null)
                    {
                        return ApiResponse<CashbackSummaryDto>.BadRequest("Draft یافت نشد یا منقضی شده است");
                    }

                    step1Dto = JsonSerializer.Deserialize<CashbackStep1Dto>(draft.Step1Data) 
                        ?? throw new InvalidOperationException("خطا در خواندن داده‌های مرحله 1 از draft");

                    if (string.IsNullOrEmpty(draft.Step2Data))
                    {
                        return ApiResponse<CashbackSummaryDto>.BadRequest("مرحله 2 هنوز تکمیل نشده است");
                    }

                    step2Dto = JsonSerializer.Deserialize<CashbackStep2Dto>(draft.Step2Data) 
                        ?? throw new InvalidOperationException("خطا در خواندن داده‌های مرحله 2 از draft");

                    // اگر Step3Data وجود دارد، از آن استفاده کن
                    if (!string.IsNullOrEmpty(draft.Step3Data))
                    {
                        step3Settings = JsonSerializer.Deserialize<SaveCashbackStep3SettingsDto>(draft.Step3Data);
                    }
                }
                else if (request.Step1 != null && request.Step2 != null)
                {
                    // استفاده از داده‌های مستقیم
                    step1Dto = request.Step1;
                    step2Dto = request.Step2;
                }
                else
                {
                    return ApiResponse<CashbackSummaryDto>.BadRequest("باید draftId یا داده‌های step1 و step2 ارسال شود");
                }

                // محاسبه تعداد مخاطبین (با در نظر گرفتن فیلتر تگ اگر Step3Settings وجود دارد)
                List<int>? targetTagIds = null;
                bool sendToSpecificTags = false;
                if (step3Settings != null)
                {
                    sendToSpecificTags = step3Settings.SendToSpecificTags;
                    targetTagIds = step3Settings.SendToSpecificTags ? step3Settings.TargetTagIds : null;
                }

                var contactIds = await GetContactIdsAsync(userId, step2Dto, targetTagIds, sendToSpecificTags);

                // تعیین نوع کش‌بک
                string cashbackType = step1Dto.CashbackType == CashbackTypes.Percentage ? "درصدی" : "مبلغ ثابت";

                // درصد
                string percentage = step1Dto.Percentage.HasValue ? $"{step1Dto.Percentage}%" : "";

                // مبلغ ثابت
                string fixedAmount = step1Dto.FixedAmount.HasValue 
                    ? $"{step1Dto.FixedAmount:N0} تومان" 
                    : "";

                // مبلغ کل خرید
                string totalPurchaseAmount = step1Dto.TotalPurchaseAmount.HasValue 
                    ? $"{step1Dto.TotalPurchaseAmount:N0} تومان" 
                    : "";

                // اعتبار کش‌بک
                string cashbackValidity = $"{step1Dto.ValidityDays} روز";

                // مخاطبین (با در نظر گرفتن فیلتر تگ اگر Step3Settings وجود دارد)
                string audience = step3Settings != null
                    ? GetAudienceDescription(step2Dto, step3Settings.SendToSpecificTags, step3Settings.TargetTagIds)
                    : GetAudienceDescription(step2Dto);

                var summary = new CashbackSummaryDto
                {
                    CashbackType = cashbackType,
                    Percentage = percentage,
                    FixedAmount = fixedAmount,
                    TotalPurchaseAmount = totalPurchaseAmount,
                    CashbackValidity = cashbackValidity,
                    Audience = audience
                };

                return ApiResponse<CashbackSummaryDto>.CreateSuccess(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت خلاصه کش‌بک برای کاربر {UserId}", userId);
                throw;
            }
        }

        public async Task<ApiResponse<CashbackSummaryDto>> SaveCashbackStep3SettingsAsync(
            int userId,
            SaveCashbackStep3RequestDto request)
        {
            try
            {
                CashbackStep1Dto step1Dto;
                CashbackStep2Dto step2Dto;

                // اگر draftId ارسال شده، از draft استفاده کن
                if (!string.IsNullOrEmpty(request.DraftId))
                {
                    var draft = await _cashbackDraftRepository.GetActiveByDraftIdAsync(request.DraftId, userId);
                    if (draft == null)
                    {
                        return ApiResponse<CashbackSummaryDto>.BadRequest("Draft یافت نشد یا منقضی شده است");
                    }

                    step1Dto = JsonSerializer.Deserialize<CashbackStep1Dto>(draft.Step1Data) 
                        ?? throw new InvalidOperationException("خطا در خواندن داده‌های مرحله 1 از draft");

                    if (string.IsNullOrEmpty(draft.Step2Data))
                    {
                        return ApiResponse<CashbackSummaryDto>.BadRequest("مرحله 2 هنوز تکمیل نشده است");
                    }

                    step2Dto = JsonSerializer.Deserialize<CashbackStep2Dto>(draft.Step2Data) 
                        ?? throw new InvalidOperationException("خطا در خواندن داده‌های مرحله 2 از draft");
                }
                else if (request.Step1 != null && request.Step2 != null)
                {
                    // استفاده از داده‌های مستقیم
                    step1Dto = request.Step1;
                    step2Dto = request.Step2;
                }
                else
                {
                    return ApiResponse<CashbackSummaryDto>.BadRequest("باید draftId یا داده‌های step1 و step2 ارسال شود");
                }

                if (request.Settings == null)
                {
                    return ApiResponse<CashbackSummaryDto>.BadRequest("تنظیمات مرحله 3 الزامی است");
                }

                // ذخیره تنظیمات Step3 در Draft (اگر draftId ارسال شده باشد)
                if (!string.IsNullOrEmpty(request.DraftId))
                {
                    var draft = await _cashbackDraftRepository.GetActiveByDraftIdAsync(request.DraftId, userId);
                    if (draft != null)
                    {
                        // سریالایز کردن تنظیمات Step3
                        draft.Step3Data = JsonSerializer.Serialize(request.Settings);
                        draft.UpdatedAt = DateTime.UtcNow;
                        // تمدید انقضا (24 ساعت از الان)
                        draft.ExpiresAt = DateTime.UtcNow.AddHours(24);
                        await _cashbackDraftRepository.UpdateAsync(draft);
                    }
                }

                // محاسبه تعداد مخاطبین با در نظر گرفتن فیلتر تگ
                var contactIds = await GetContactIdsAsync(
                    userId, 
                    step2Dto, 
                    request.Settings.SendToSpecificTags ? request.Settings.TargetTagIds : null,
                    request.Settings.SendToSpecificTags);

                // تعیین نوع کش‌بک
                string cashbackType = step1Dto.CashbackType == CashbackTypes.Percentage ? "درصدی" : "مبلغ ثابت";

                // درصد
                string percentage = step1Dto.Percentage.HasValue ? $"{step1Dto.Percentage}%" : "";

                // مبلغ ثابت
                string fixedAmount = step1Dto.FixedAmount.HasValue 
                    ? $"{step1Dto.FixedAmount:N0} تومان" 
                    : "";

                // مبلغ کل خرید
                string totalPurchaseAmount = step1Dto.TotalPurchaseAmount.HasValue 
                    ? $"{step1Dto.TotalPurchaseAmount:N0} تومان" 
                    : "";

                // اعتبار کش‌بک
                string cashbackValidity = $"{step1Dto.ValidityDays} روز";

                // مخاطبین (با در نظر گرفتن فیلتر تگ)
                string audience = GetAudienceDescription(step2Dto, request.Settings.SendToSpecificTags, request.Settings.TargetTagIds);

                var summary = new CashbackSummaryDto
                {
                    CashbackType = cashbackType,
                    Percentage = percentage,
                    FixedAmount = fixedAmount,
                    TotalPurchaseAmount = totalPurchaseAmount,
                    CashbackValidity = cashbackValidity,
                    Audience = audience
                };

                return ApiResponse<CashbackSummaryDto>.CreateSuccess(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ذخیره تنظیمات مرحله 3 کش‌بک برای کاربر {UserId}", userId);
                throw;
            }
        }

        public async Task<ApiResponse<CashbackCostSummaryDto>> CalculateCostSummaryAsync(int userId, CreateCashbackDto cashbackDto)
        {
            try
            {
                // محاسبه تعداد مخاطبین
                int contactsCount = 0;

                if (cashbackDto.TargetAudience == CashbackTargetAudience.All)
                {
                    // همه مخاطبین کاربر
                    var notebooks = await _context.ContactNotebooks
                        .Where(cn => cn.UserId == userId && !cn.IsDeleted)
                        .Select(cn => cn.Id)
                        .ToListAsync();

                    contactsCount = await _context.Contacts
                        .Where(c => notebooks.Contains(c.ContactNotebookId) && !c.IsDeleted)
                        .CountAsync();
                }
                else if (cashbackDto.TargetAudience == CashbackTargetAudience.NewContacts)
                {
                    // مخاطبین جدید (15 روز اخیر)
                    var cutoffDate = DateTime.UtcNow.AddDays(-15);
                    var notebooks = await _context.ContactNotebooks
                        .Where(cn => cn.UserId == userId && !cn.IsDeleted)
                        .Select(cn => cn.Id)
                        .ToListAsync();

                    contactsCount = await _context.Contacts
                        .Where(c => notebooks.Contains(c.ContactNotebookId) && 
                               !c.IsDeleted && 
                               c.CreatedAt >= cutoffDate)
                        .CountAsync();
                }
                else if (cashbackDto.TargetAudience == CashbackTargetAudience.SpecificNotebooks && 
                         cashbackDto.TargetNotebookIds != null)
                {
                    contactsCount = await _context.Contacts
                        .Where(c => cashbackDto.TargetNotebookIds.Contains(c.ContactNotebookId) && !c.IsDeleted)
                        .CountAsync();
                }

                // محاسبه هزینه تخمینی
                decimal costPerPart = 160; // هزینه هر پیامک/اعلان (هر پارت)
                decimal estimatedTotalCost = contactsCount * costPerPart;

                // بررسی موجودی کیف پول
                // غیرفعال شده - دیگر کیف پول چک نمی‌شود
                // var balance = await _walletService.GetBalanceAsync(userId);
                // var hasSufficientBalance = balance >= estimatedTotalCost;
                var hasSufficientBalance = true; // همیشه کافی است

                var summary = new CashbackCostSummaryDto
                {
                    AutomationType = "کش‌بک",
                    ExecutionTime = cashbackDto.DepositTiming == CashbackDepositTiming.Immediate ? "فوری" : "زمان‌بندی شده",
                    ContactsCount = contactsCount,
                    CostPerPart = costPerPart,
                    EstimatedTotalCost = estimatedTotalCost,
                    FormattedEstimatedCost = $"{estimatedTotalCost:N0} تومان",
                    WalletStatus = hasSufficientBalance ? "موجودی کافی است" : "موجودی ناکافی",
                    HasSufficientBalance = hasSufficientBalance
                };

                return ApiResponse<CashbackCostSummaryDto>.CreateSuccess(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در محاسبه هزینه کش‌بک برای کاربر {UserId}", userId);
                throw;
            }
        }

        public async Task<ApiResponse<List<CashbackTransactionDto>>> GetCashbackTransactionsAsync(int cashbackId, int userId, int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                // بررسی مالکیت کش‌بک
                var cashback = await _cashbackRepository.GetByIdAndUserIdAsync(cashbackId, userId);
                if (cashback == null)
                {
                    return ApiResponse<List<CashbackTransactionDto>>.NotFound("کش‌بک یافت نشد");
                }

                var transactions = await _cashbackTransactionRepository.GetByCashbackIdAsync(cashbackId, pageNumber, pageSize);
                var transactionDtos = transactions.Select(t => MapToCashbackTransactionDto(t, cashback)).ToList();

                return ApiResponse<List<CashbackTransactionDto>>.CreateSuccess(transactionDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت تراکنش‌های کش‌بک {CashbackId}", cashbackId);
                throw;
            }
        }

        #region Private Methods

        private async Task<CashbackDto> MapToCashbackDtoAsync(Cashback cashback)
        {
            var now = DateTime.UtcNow;
            int? remainingDays = null;
            
            if (cashback.EndDate.HasValue && cashback.EndDate > now)
            {
                remainingDays = (int)(cashback.EndDate.Value - now).TotalDays;
            }

            List<int>? notebookIds = null;
            if (!string.IsNullOrEmpty(cashback.TargetNotebookIds))
            {
                try
                {
                    notebookIds = JsonSerializer.Deserialize<List<int>>(cashback.TargetNotebookIds);
                }
                catch { }
            }

            List<int>? tagIds = null;
            if (!string.IsNullOrEmpty(cashback.TargetTagIds))
            {
                try
                {
                    tagIds = JsonSerializer.Deserialize<List<int>>(cashback.TargetTagIds);
                }
                catch { }
            }

            // تعیین مبلغ فرمت شده
            string formattedAmount;
            if (cashback.CashbackType == CashbackTypes.Percentage)
            {
                formattedAmount = $"{cashback.Percentage}%";
            }
            else
            {
                formattedAmount = $"{cashback.FixedAmount:N0} تومان";
            }

            // توضیح نوع مخاطبین
            string audienceDescription;
            if (cashback.TargetAudience == CashbackTargetAudience.SpecificNotebooks && notebookIds != null && notebookIds.Any())
            {
                // دریافت نام دفترچه‌ها
                var notebooks = await _context.ContactNotebooks
                    .Where(n => notebookIds.Contains(n.Id) && !n.IsDeleted)
                    .Select(n => n.Name)
                    .ToListAsync();

                if (notebooks.Any())
                {
                    audienceDescription = string.Join("، ", notebooks);
                }
                else
                {
                    audienceDescription = "دفترچه‌های خاص";
                }
            }
            else
            {
                audienceDescription = cashback.TargetAudience switch
                {
                    CashbackTargetAudience.All => "همه مخاطبین",
                    CashbackTargetAudience.NewContacts => "مخاطبین جدید",
                    _ => "نامشخص"
                };
            }

            // توضیح وضعیت زمان‌بندی
            string? scheduleStatusDescription = cashback.ScheduleStatus switch
            {
                CashbackScheduleStatus.None => null,
                CashbackScheduleStatus.Pending => cashback.ScheduledDepositDateTime.HasValue
                    ? $"در انتظار پردازش - {ToPersianDate(cashback.ScheduledDepositDateTime.Value)}"
                    : "در انتظار پردازش",
                CashbackScheduleStatus.Processing => "در حال پردازش",
                CashbackScheduleStatus.Completed => cashback.LastScheduledProcessedAt.HasValue
                    ? $"پردازش شده - {ToPersianDate(cashback.LastScheduledProcessedAt.Value)}"
                    : "پردازش شده",
                CashbackScheduleStatus.Failed => "پردازش ناموفق",
                CashbackScheduleStatus.Cancelled => "لغو شده",
                _ => null
            };

            return new CashbackDto
            {
                Id = cashback.Id,
                Title = cashback.Title,
                Description = cashback.Description,
                CashbackType = cashback.CashbackType,
                Percentage = cashback.Percentage,
                FixedAmount = cashback.FixedAmount,
                FormattedAmount = formattedAmount,
                MaxCashbackAmount = cashback.MaxCashbackAmount,
                MinPurchaseAmount = cashback.MinPurchaseAmount,
                ValidityDays = cashback.ValidityDays,
                RemainingDays = remainingDays,
                StartDate = cashback.StartDate,
                EndDate = cashback.EndDate,
                DepositTiming = cashback.DepositTiming,
                ScheduledDepositTime = cashback.ScheduledDepositTime,
                ScheduledDepositDateTime = cashback.ScheduledDepositDateTime,
                ScheduleStatus = cashback.ScheduleStatus,
                ScheduleStatusDescription = scheduleStatusDescription,
                TargetAudience = cashback.TargetAudience,
                TargetAudienceDescription = audienceDescription,
                TargetNotebookIds = notebookIds,
                TargetTagIds = tagIds,
                SendToSpecificTags = cashback.SendToSpecificTags,
                IsActive = cashback.IsActive,
                CreatedAt = cashback.CreatedAt,
                UpdatedAt = cashback.UpdatedAt
            };
        }

        private CashbackTransactionDto MapToCashbackTransactionDto(CashbackTransaction transaction, Cashback cashback)
        {
            return new CashbackTransactionDto
            {
                Id = transaction.Id,
                CashbackId = transaction.CashbackId,
                CashbackTitle = cashback.Title,
                ContactId = transaction.ContactId,
                ContactName = transaction.Contact?.FullName ?? "نامشخص",
                ContactMobile = transaction.Contact?.MobileNumber ?? "",
                Amount = transaction.Amount,
                FormattedAmount = $"{transaction.Amount:N0} تومان",
                PurchaseAmount = transaction.PurchaseAmount,
                Status = transaction.Status,
                DepositedAt = transaction.DepositedAt,
                CreatedAt = transaction.CreatedAt
            };
        }

        public async Task<ApiResponse<List<CashbackNotebookDto>>> GetNotebooksForCashbackAsync(int userId)
        {
            try
            {
                var notebooks = await _notebookRepository.GetByUserIdAsync(userId, true);
                var notebookDtos = new List<CashbackNotebookDto>();

                foreach (var notebook in notebooks)
                {
                    var contactsCount = await _context.Contacts
                        .CountAsync(c => c.ContactNotebookId == notebook.Id && !c.IsDeleted);

                    notebookDtos.Add(new CashbackNotebookDto
                    {
                        Id = notebook.Id,
                        Name = notebook.Name,
                        MembersCount = contactsCount
                    });
                }

                return ApiResponse<List<CashbackNotebookDto>>.CreateSuccess(notebookDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت لیست دفترچه‌ها برای کاربر {UserId}", userId);
                throw;
            }
        }

        public async Task<ApiResponse<CashbackStep2ValidationResponseDto>> ValidateCashbackStep2Async(int userId, CashbackStep2Dto step2Dto)
        {
            try
            {
                var errors = new List<string>();
                int totalContactsCount = 0;
                string audienceDescription = string.Empty;

                // اعتبارسنجی نوع مخاطبین
                if (string.IsNullOrWhiteSpace(step2Dto.TargetAudience))
                {
                    errors.Add("نوع مخاطبین الزامی است");
                }
                else if (step2Dto.TargetAudience != CashbackTargetAudience.All &&
                         step2Dto.TargetAudience != CashbackTargetAudience.NewContacts &&
                         step2Dto.TargetAudience != CashbackTargetAudience.SpecificNotebooks)
                {
                    errors.Add("نوع مخاطبین نامعتبر است");
                }

                // محاسبه تعداد مخاطبین بر اساس نوع انتخاب شده
                if (step2Dto.TargetAudience == CashbackTargetAudience.All)
                {
                    // همه مخاطبین کاربر
                    var notebooks = await _context.ContactNotebooks
                        .Where(cn => cn.UserId == userId && !cn.IsDeleted)
                        .Select(cn => cn.Id)
                        .ToListAsync();

                    totalContactsCount = await _context.Contacts
                        .Where(c => notebooks.Contains(c.ContactNotebookId) && !c.IsDeleted)
                        .CountAsync();

                    audienceDescription = "همه مخاطبین";
                }
                else if (step2Dto.TargetAudience == CashbackTargetAudience.NewContacts)
                {
                    // مخاطبین جدید (15 روز اخیر)
                    var cutoffDate = DateTime.UtcNow.AddDays(-15);
                    var notebooks = await _context.ContactNotebooks
                        .Where(cn => cn.UserId == userId && !cn.IsDeleted)
                        .Select(cn => cn.Id)
                        .ToListAsync();

                    totalContactsCount = await _context.Contacts
                        .Where(c => notebooks.Contains(c.ContactNotebookId) &&
                               !c.IsDeleted &&
                               c.CreatedAt >= cutoffDate)
                        .CountAsync();

                    audienceDescription = "مخاطبین جدید";
                }
                else if (step2Dto.TargetAudience == CashbackTargetAudience.SpecificNotebooks)
                {
                    // دفترچه‌های خاص
                    if (step2Dto.TargetNotebookIds == null || !step2Dto.TargetNotebookIds.Any())
                    {
                        errors.Add("حداقل یک دفترچه باید انتخاب شود");
                    }
                    else
                    {
                        // بررسی مالکیت دفترچه‌ها
                        var userNotebooks = await _context.ContactNotebooks
                            .Where(cn => cn.UserId == userId && !cn.IsDeleted)
                            .Select(cn => cn.Id)
                            .ToListAsync();

                        var invalidNotebooks = step2Dto.TargetNotebookIds
                            .Where(id => !userNotebooks.Contains(id))
                            .ToList();

                        if (invalidNotebooks.Any())
                        {
                            errors.Add($"دفترچه‌های با شناسه‌های {string.Join(", ", invalidNotebooks)} یافت نشد یا متعلق به شما نیست");
                        }
                        else
                        {
                            totalContactsCount = await _context.Contacts
                                .Where(c => step2Dto.TargetNotebookIds.Contains(c.ContactNotebookId) && !c.IsDeleted)
                                .CountAsync();

                            // دریافت نام دفترچه‌ها
                            var notebookNames = await _context.ContactNotebooks
                                .Where(cn => step2Dto.TargetNotebookIds.Contains(cn.Id))
                                .Select(cn => cn.Name)
                                .ToListAsync();

                            audienceDescription = string.Join("، ", notebookNames);
                        }
                    }
                }

                var isValid = errors.Count == 0;
                var response = new CashbackStep2ValidationResponseDto
                {
                    IsValid = isValid,
                    Errors = errors,
                    TotalContactsCount = totalContactsCount,
                    TargetAudienceDescription = audienceDescription
                };

                // اگر اعتبارسنجی موفق بود و draftId وجود داشت، Draft را به‌روزرسانی می‌کنیم
                if (isValid && !string.IsNullOrEmpty(step2Dto.DraftId))
                {
                    try
                    {
                        var draft = await _cashbackDraftRepository.GetActiveByDraftIdAsync(step2Dto.DraftId, userId);
                        if (draft != null)
                        {
                            var step2Json = JsonSerializer.Serialize(step2Dto);
                            draft.Step2Data = step2Json;
                            draft.UpdatedAt = DateTime.UtcNow;
                            draft.ExpiresAt = DateTime.UtcNow.AddHours(DraftExpirationHours); // تمدید انقضا

                            await _cashbackDraftRepository.UpdateAsync(draft);

                            _logger.LogInformation("Draft کش‌بک با شناسه {DraftId} برای کاربر {UserId} به‌روزرسانی شد", step2Dto.DraftId, userId);
                        }
                        else
                        {
                            _logger.LogWarning("Draft با شناسه {DraftId} برای کاربر {UserId} یافت نشد یا منقضی شده است", step2Dto.DraftId, userId);
                            errors.Add("Draft یافت نشد یا منقضی شده است. لطفاً از مرحله 1 دوباره شروع کنید");
                            response.IsValid = false;
                            response.Errors = errors;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "خطا در به‌روزرسانی draft برای کاربر {UserId}", userId);
                        // اگر draft به‌روزرسانی نشد، خطا نمی‌دهیم (فقط لاگ می‌کنیم)
                    }
                }
                else if (isValid && string.IsNullOrEmpty(step2Dto.DraftId))
                {
                    errors.Add("شناسه Draft الزامی است. لطفاً از مرحله 1 دوباره شروع کنید");
                    response.IsValid = false;
                    response.Errors = errors;
                }

                if (response.IsValid)
                {
                    return ApiResponse<CashbackStep2ValidationResponseDto>.CreateSuccess(response, "اطلاعات مرحله 2 معتبر است");
                }
                else
                {
                    return ApiResponse<CashbackStep2ValidationResponseDto>.BadRequest("خطا در اعتبارسنجی", response.Errors);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در اعتبارسنجی مرحله 2 کش‌بک برای کاربر {UserId}", userId);
                throw;
            }
        }

        public async Task<ApiResponse<CashbackStep1ValidationResponseDto>> ValidateCashbackStep1Async(int userId, CashbackStep1Dto step1Dto)
        {
            try
            {
                var errors = new List<string>();
                decimal? calculatedAmount = null;
                string? formattedAmount = null;

                // اعتبارسنجی نوع کش‌بک
                if (string.IsNullOrWhiteSpace(step1Dto.CashbackType))
                {
                    errors.Add("نوع کش‌بک الزامی است");
                }
                else if (step1Dto.CashbackType != CashbackTypes.Percentage && step1Dto.CashbackType != CashbackTypes.FixedAmount)
                {
                    errors.Add("نوع کش‌بک نامعتبر است. باید 'Percentage' یا 'FixedAmount' باشد");
                }

                // اعتبارسنجی بر اساس نوع کش‌بک
                if (step1Dto.CashbackType == CashbackTypes.Percentage)
                {
                    if (!step1Dto.Percentage.HasValue)
                    {
                        errors.Add("درصد کش‌بک الزامی است");
                    }
                    else if (step1Dto.Percentage <= 0 || step1Dto.Percentage > 50)
                    {
                        errors.Add("درصد کش‌بک باید بین 1 تا 50 باشد");
                    }
                    else
                    {
                        // محاسبه مبلغ کش‌بک در صورت وجود مبلغ خرید
                        if (step1Dto.TotalPurchaseAmount.HasValue && step1Dto.TotalPurchaseAmount > 0)
                        {
                            calculatedAmount = (step1Dto.TotalPurchaseAmount.Value * step1Dto.Percentage.Value) / 100;
                            formattedAmount = $"{calculatedAmount:N0} تومان";
                        }
                        else if (step1Dto.TotalPurchaseAmount.HasValue && step1Dto.TotalPurchaseAmount <= 0)
                        {
                            errors.Add("مبلغ کل خرید باید بزرگتر از صفر باشد");
                        }
                    }
                }
                else if (step1Dto.CashbackType == CashbackTypes.FixedAmount)
                {
                    if (!step1Dto.FixedAmount.HasValue)
                    {
                        errors.Add("مبلغ ثابت کش‌بک الزامی است");
                    }
                    else if (step1Dto.FixedAmount <= 0)
                    {
                        errors.Add("مبلغ ثابت کش‌بک باید بزرگتر از صفر باشد");
                    }
                    else if (step1Dto.FixedAmount < 1000 || step1Dto.FixedAmount > 10000000)
                    {
                        errors.Add("مبلغ کش‌بک باید بین 1,000 تا 10,000,000 تومان باشد");
                    }
                    else
                    {
                        calculatedAmount = step1Dto.FixedAmount.Value;
                        formattedAmount = $"{calculatedAmount:N0} تومان";
                    }
                }

                // اعتبارسنجی مدت اعتبار
                if (step1Dto.ValidityDays < 1 || step1Dto.ValidityDays > 365)
                {
                    errors.Add("مدت اعتبار باید بین 1 تا 365 روز باشد");
                }

                var isValid = errors.Count == 0;
                var response = new CashbackStep1ValidationResponseDto
                {
                    IsValid = isValid,
                    Errors = errors,
                    CalculatedCashbackAmount = calculatedAmount,
                    FormattedCashbackAmount = formattedAmount
                };

                // اگر اعتبارسنجی موفق بود، Draft را در دیتابیس ایجاد می‌کنیم
                if (isValid)
                {
                    try
                    {
                        // ایجاد Draft در دیتابیس
                        var draftId = $"{userId}_{Guid.NewGuid()}";
                        var step1Json = JsonSerializer.Serialize(step1Dto);
                        var expiresAt = DateTime.UtcNow.AddHours(DraftExpirationHours);

                        var draft = new CashbackDraft
                        {
                            UserId = userId,
                            DraftId = draftId,
                            Step1Data = step1Json,
                            Step2Data = null,
                            ExpiresAt = expiresAt,
                            CreatedAt = DateTime.UtcNow
                        };

                        await _cashbackDraftRepository.AddAsync(draft);

                        response.DraftId = draftId;
                        response.DraftExpiresAt = expiresAt;

                        _logger.LogInformation("Draft کش‌بک با شناسه {DraftId} برای کاربر {UserId} ایجاد شد", draftId, userId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "خطا در ایجاد draft برای کاربر {UserId}", userId);
                        // اگر draft ایجاد نشد، خطا نمی‌دهیم (فقط لاگ می‌کنیم)
                    }
                }

                if (isValid)
                {
                    return ApiResponse<CashbackStep1ValidationResponseDto>.CreateSuccess(response, "اطلاعات مرحله 1 معتبر است");
                }
                else
                {
                    return ApiResponse<CashbackStep1ValidationResponseDto>.BadRequest("خطا در اعتبارسنجی", errors);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در اعتبارسنجی مرحله 1 کش‌بک");
                throw;
            }
        }

        public async Task<ApiResponse<ApplyCashbackResultDto>> ApplyCashbackAsync(int cashbackId, int userId, decimal? purchaseAmount = null)
        {
            try
            {
                // دریافت کش‌بک
                var cashback = await _cashbackRepository.GetByIdAndUserIdAsync(cashbackId, userId);
                if (cashback == null)
                {
                    return ApiResponse<ApplyCashbackResultDto>.NotFound("کش‌بک یافت نشد");
                }

                if (!cashback.IsActive)
                {
                    return ApiResponse<ApplyCashbackResultDto>.BadRequest("کش‌بک غیرفعال است");
                }

                // بررسی انقضا
                var now = DateTime.UtcNow;
                if (cashback.EndDate.HasValue && cashback.EndDate < now)
                {
                    return ApiResponse<ApplyCashbackResultDto>.BadRequest("کش‌بک منقضی شده است");
                }

                // دریافت مخاطبین بر اساس TargetAudience
                var contacts = await GetTargetContactsAsync(userId, cashback);

                if (!contacts.Any())
                {
                    return ApiResponse<ApplyCashbackResultDto>.BadRequest("هیچ مخاطبی برای اعمال کش‌بک یافت نشد");
                }

                // محاسبه هزینه ارسال پیامک
                var smsCost = contacts.Count * CostPerSms;

                // بررسی موجودی کیف پول
                // غیرفعال شده - دیگر کیف پول چک نمی‌شود
                /*
                var walletBalance = await _walletService.GetBalanceAsync(userId);
                if (walletBalance < smsCost)
                {
                    var requiredAmount = smsCost - walletBalance;
                    var message = $"موجودی کیف پول کافی نیست. " +
                        $"برای ارسال کش‌بک به {contacts.Count} مخاطب، به {smsCost:N0} تومان موجودی نیاز دارید. " +
                        $"موجودی فعلی: {walletBalance:N0} تومان. " +
                        $"لطفاً {requiredAmount:N0} تومان به کیف پول خود اضافه کنید.";
                    return ApiResponse<ApplyCashbackResultDto>.BadRequest(message);
                }
                */

                var successCount = 0;
                var failedCount = 0;
                var totalCashbackAmount = 0m;

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    foreach (var contact in contacts)
                    {
                        try
                        {
                            // محاسبه مبلغ کش‌بک (پشتیبانی از ترکیب درصدی و ثابت)
                            decimal cashbackAmount = 0;
                            decimal percentageAmount = 0;
                            decimal fixedAmount = 0;

                            // محاسبه کش‌بک درصدی (اگر درصد و مبلغ خرید موجود باشد)
                            if (cashback.Percentage.HasValue && cashback.Percentage > 0)
                            {
                                if (!purchaseAmount.HasValue || purchaseAmount <= 0)
                                {
                                    _logger.LogWarning("مبلغ خرید برای کش‌بک درصدی وارد نشده - ContactId: {ContactId}", contact.Id);
                                    // اگر فقط درصدی است و مبلغ خرید نداریم، خطا
                                    if (!cashback.FixedAmount.HasValue || cashback.FixedAmount <= 0)
                                    {
                                        failedCount++;
                                        continue;
                                    }
                                }
                                else
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
                            cashbackAmount = percentageAmount + fixedAmount;

                            // اگر مجموع صفر باشد، خطا
                            if (cashbackAmount <= 0)
                            {
                                _logger.LogWarning("مبلغ کش‌بک محاسبه شده صفر است - ContactId: {ContactId}", contact.Id);
                                failedCount++;
                                continue;
                            }

                            // ایجاد تراکنش کش‌بک
                            var cashbackTransaction = new CashbackTransaction
                            {
                                CashbackId = cashbackId,
                                ContactId = contact.Id,
                                Amount = cashbackAmount,
                                PurchaseAmount = purchaseAmount,
                                Status = CashbackTransactionStatuses.Pending,
                                CreatedAt = now
                            };

                            await _context.CashbackTransactions.AddAsync(cashbackTransaction);
                            await _context.SaveChangesAsync();

                            // ارسال پیامک
                            var message = GenerateCashbackMessage(cashback, cashbackAmount, purchaseAmount);
                            var smsRequest = new DTOs.Sms.SendSmsRequestDto
                            {
                                Mobile = contact.MobileNumber,
                                Message = message
                            };

                            var smsResult = await _smsService.SendSmsAsync(smsRequest);

                            // Sid > 0 یعنی پیام ارسال شده (حتی اگر Status = 0 باشد)
                            bool isSuccess = smsResult.Success && smsResult.Data != null && 
                                (smsResult.Data.Sid > 0 || smsResult.Data.Status > 0);
                            
                            if (isSuccess)
                            {
                                // به‌روزرسانی وضعیت تراکنش
                                cashbackTransaction.Status = CashbackTransactionStatuses.Deposited;
                                cashbackTransaction.DepositedAt = now;
                                cashbackTransaction.Description = "کش‌بک با موفقیت ارسال شد";
                                successCount++;
                                totalCashbackAmount += cashbackAmount;
                            }
                            else
                            {
                                cashbackTransaction.Status = CashbackTransactionStatuses.Failed;
                                cashbackTransaction.Description = ControlledErrorHelper.SmsFailed;
                                failedCount++;
                            }

                            await _context.SaveChangesAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "خطا در اعمال کش‌بک برای مخاطب {ContactId}", contact.Id);
                            failedCount++;
                        }
                    }

                    // کسر هزینه ارسال پیامک از کیف پول
                    // غیرفعال شده - دیگر از کیف پول کسر نمی‌شود
                    /*
                    if (successCount > 0)
                    {
                        var actualSmsCost = successCount * CostPerSms;
                        await _walletService.DeductBalanceAsync(
                            userId,
                            actualSmsCost,
                            "ارسال کش‌بک",
                            $"هزینه ارسال {successCount} پیامک برای کش‌بک {cashback.Title}");
                    }
                    */

                    await transaction.CommitAsync();

                    var result = new ApplyCashbackResultDto
                    {
                        TotalContacts = contacts.Count,
                        SuccessCount = successCount,
                        FailedCount = failedCount,
                        TotalCashbackAmount = totalCashbackAmount,
                        FormattedTotalCashbackAmount = $"{totalCashbackAmount:N0} تومان",
                        SmsCost = successCount * CostPerSms,
                        FormattedSmsCost = $"{(successCount * CostPerSms):N0} تومان"
                    };

                    _logger.LogInformation("کش‌بک {CashbackId} برای کاربر {UserId} اعمال شد - موفق: {Success}, ناموفق: {Failed}", 
                        cashbackId, userId, successCount, failedCount);

                    return ApiResponse<ApplyCashbackResultDto>.CreateSuccess(result, "کش‌بک با موفقیت اعمال شد");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "خطا در اعمال کش‌بک {CashbackId} برای کاربر {UserId}", cashbackId, userId);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در اعمال کش‌بک {CashbackId} برای کاربر {UserId}", cashbackId, userId);
                throw;
            }
        }

        private async Task<List<Models.Contact>> GetTargetContactsAsync(int userId, Cashback cashback)
        {
            var contacts = new List<Models.Contact>();

            if (cashback.TargetAudience == CashbackTargetAudience.All)
            {
                var notebooks = await _context.ContactNotebooks
                    .Where(cn => cn.UserId == userId && !cn.IsDeleted)
                    .Select(cn => cn.Id)
                    .ToListAsync();

                contacts = await _context.Contacts
                    .Where(c => notebooks.Contains(c.ContactNotebookId) && !c.IsDeleted)
                    .ToListAsync();
            }
            else if (cashback.TargetAudience == CashbackTargetAudience.NewContacts)
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-15);
                var notebooks = await _context.ContactNotebooks
                    .Where(cn => cn.UserId == userId && !cn.IsDeleted)
                    .Select(cn => cn.Id)
                    .ToListAsync();

                contacts = await _context.Contacts
                    .Where(c => notebooks.Contains(c.ContactNotebookId) &&
                           !c.IsDeleted &&
                           c.CreatedAt >= cutoffDate)
                    .ToListAsync();
            }
            else if (cashback.TargetAudience == CashbackTargetAudience.SpecificNotebooks && 
                     !string.IsNullOrEmpty(cashback.TargetNotebookIds))
            {
                try
                {
                    var notebookIds = JsonSerializer.Deserialize<List<int>>(cashback.TargetNotebookIds);
                    if (notebookIds != null && notebookIds.Any())
                    {
                        contacts = await _context.Contacts
                            .Where(c => notebookIds.Contains(c.ContactNotebookId) && !c.IsDeleted)
                            .ToListAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطا در deserialize کردن TargetNotebookIds برای کش‌بک {CashbackId}", cashback.Id);
                }
            }

            // فیلتر بر اساس تگ‌ها (در صورت فعال بودن)
            if (cashback.SendToSpecificTags && !string.IsNullOrEmpty(cashback.TargetTagIds))
            {
                try
                {
                    var tagIds = JsonSerializer.Deserialize<List<int>>(cashback.TargetTagIds);
                    if (tagIds != null && tagIds.Any())
                    {
                        var contactIdsWithTags = await _context.ContactTags
                            .Where(ct => tagIds.Contains(ct.TagId))
                            .Select(ct => ct.ContactId)
                            .Distinct()
                            .ToListAsync();

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

        public async Task<ApiResponse<ApplyCashbackToContactResultDto>> ApplyCashbackToContactAsync(
            int userId, 
            ApplyCashbackToContactDto request)
        {
            try
            {
                // نرمال‌سازی شماره موبایل
                var normalizedMobile = NormalizePhoneNumber(request.MobileNumber);

                // پیدا کردن مخاطب
                var contact = await _context.Contacts
                    .Include(c => c.ContactNotebook)
                    .FirstOrDefaultAsync(c => c.MobileNumber == normalizedMobile && 
                                         !c.IsDeleted &&
                                         c.ContactNotebook.UserId == userId);

                if (contact == null)
                {
                    return ApiResponse<ApplyCashbackToContactResultDto>.NotFound("مخاطب با این شماره موبایل یافت نشد");
                }

                // پیدا کردن کش‌بک
                Cashback? cashback = null;
                if (request.CashbackId.HasValue)
                {
                    cashback = await _cashbackRepository.GetByIdAndUserIdAsync(request.CashbackId.Value, userId);
                    if (cashback == null)
                    {
                        return ApiResponse<ApplyCashbackToContactResultDto>.NotFound("کش‌بک یافت نشد");
                    }
                }
                else
                {
                    // پیدا کردن اولین کش‌بک فعال مناسب
                    var activeCashbacks = await _cashbackRepository.GetActiveByUserIdAsync(userId);
                    var currentTime = DateTime.UtcNow;
                    
                    cashback = activeCashbacks
                        .Where(c => c.IsActive && 
                               c.StartDate <= currentTime &&
                               (c.EndDate == null || c.EndDate >= currentTime))
                        .OrderByDescending(c => c.CreatedAt)
                        .FirstOrDefault();

                    if (cashback == null)
                    {
                        return ApiResponse<ApplyCashbackToContactResultDto>.NotFound("هیچ کش‌بک فعالی یافت نشد");
                    }
                }

                // بررسی اعتبار کش‌بک
                var now = DateTime.UtcNow;
                if (!cashback.IsActive)
                {
                    return ApiResponse<ApplyCashbackToContactResultDto>.BadRequest("کش‌بک غیرفعال است");
                }

                if (cashback.EndDate.HasValue && cashback.EndDate < now)
                {
                    return ApiResponse<ApplyCashbackToContactResultDto>.BadRequest("کش‌بک منقضی شده است");
                }

                // بررسی MinPurchaseAmount
                if (cashback.MinPurchaseAmount.HasValue && cashback.MinPurchaseAmount > 0)
                {
                    if (request.PurchaseAmount <= 0 || request.PurchaseAmount < cashback.MinPurchaseAmount.Value)
                    {
                        return ApiResponse<ApplyCashbackToContactResultDto>.BadRequest(
                            $"حداقل مبلغ خرید برای دریافت این کش‌بک {cashback.MinPurchaseAmount:N0} تومان است");
                    }
                }

                // محاسبه مبلغ کش‌بک (پشتیبانی از ترکیب درصدی و ثابت)
                decimal cashbackAmount = 0;
                decimal percentageAmount = 0;
                decimal fixedAmount = 0;

                // محاسبه کش‌بک درصدی (اگر درصد موجود باشد)
                if (cashback.Percentage.HasValue && cashback.Percentage > 0)
                {
                    if (request.PurchaseAmount <= 0)
                    {
                        // اگر فقط درصدی است و مبلغ خرید نداریم، خطا
                        if (!cashback.FixedAmount.HasValue || cashback.FixedAmount <= 0)
                        {
                            return ApiResponse<ApplyCashbackToContactResultDto>.BadRequest("برای کش‌بک درصدی، مبلغ خرید الزامی است");
                        }
                    }
                    else
                    {
                        percentageAmount = (request.PurchaseAmount * cashback.Percentage.Value) / 100;

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
                cashbackAmount = percentageAmount + fixedAmount;

                // اگر مجموع صفر باشد، خطا
                if (cashbackAmount <= 0)
                {
                    return ApiResponse<ApplyCashbackToContactResultDto>.BadRequest("مبلغ کش‌بک محاسبه شده صفر است. لطفاً درصد یا مبلغ ثابت را تنظیم کنید.");
                }

                // بررسی موجودی کیف پول برای ارسال پیامک
                // غیرفعال شده - دیگر کیف پول چک نمی‌شود
                /*
                var walletBalance = await _walletService.GetBalanceAsync(userId);
                if (walletBalance < CostPerSms)
                {
                    var requiredAmount = CostPerSms - walletBalance;
                    var message = $"موجودی کیف پول کافی نیست. " +
                        $"برای ارسال این کش‌بک به {CostPerSms:N0} تومان موجودی نیاز دارید. " +
                        $"موجودی فعلی: {walletBalance:N0} تومان. " +
                        $"لطفاً {requiredAmount:N0} تومان به کیف پول خود اضافه کنید.";
                    return ApiResponse<ApplyCashbackToContactResultDto>.BadRequest(message);
                }
                */

                // استفاده از IsolationLevel.Serializable برای جلوگیری از Race Condition
                using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                try
                {
                    // دریافت یا ایجاد موجودی کش‌بک با Pessimistic Lock (FOR UPDATE)
                    var balance = await _context.ContactCashbackBalances
                        .FromSqlRaw(
                            "SELECT * FROM ContactCashbackBalances WITH (UPDLOCK, ROWLOCK) WHERE ContactId = {0} AND UserId = {1}",
                            contact.Id, userId)
                        .FirstOrDefaultAsync();

                    decimal balanceBefore = 0;
                    bool hadPreviousBalance = false;

                    if (balance == null)
                    {
                        // ایجاد رکورد جدید
                        balance = new ContactCashbackBalance
                        {
                            ContactId = contact.Id,
                            UserId = userId,
                            TotalBalance = 0,
                            UsableBalance = 0,
                            CreatedAt = now
                        };
                        await _context.ContactCashbackBalances.AddAsync(balance);
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        balanceBefore = balance.TotalBalance;
                        hadPreviousBalance = balanceBefore > 0;
                        // Attach entity to track changes
                        _context.Entry(balance).State = EntityState.Modified;
                    }

                    // بررسی تکراری بودن تراکنش (در 5 دقیقه اخیر)
                    var existingTransaction = await _context.CashbackTransactions
                        .Where(ct => ct.CashbackId == cashback.Id && 
                                     ct.ContactId == contact.Id && 
                                     ct.Status == CashbackTransactionStatuses.Pending &&
                                     ct.CreatedAt >= now.AddMinutes(-5))
                        .FirstOrDefaultAsync();

                    if (existingTransaction != null)
                    {
                        await transaction.RollbackAsync();
                        return ApiResponse<ApplyCashbackToContactResultDto>.BadRequest(
                            "یک تراکنش کش‌بک در حال پردازش برای این مخاطب وجود دارد. لطفاً چند لحظه صبر کنید.");
                    }

                    // محاسبه تاریخ انقضا
                    var expiryDate = now.AddDays(cashback.ValidityDays);

                    // ایجاد تراکنش کش‌بک
                    var cashbackTransaction = new CashbackTransaction
                    {
                        CashbackId = cashback.Id,
                        ContactId = contact.Id,
                        Amount = cashbackAmount,
                        PurchaseAmount = request.PurchaseAmount > 0 ? request.PurchaseAmount : null,
                        Status = CashbackTransactionStatuses.Pending,
                        CreatedAt = now
                    };

                    await _context.CashbackTransactions.AddAsync(cashbackTransaction);
                    await _context.SaveChangesAsync();

                    // به‌روزرسانی موجودی کش‌بک
                    balance.TotalBalance = balanceBefore + cashbackAmount;
                    balance.UsableBalance = balance.TotalBalance;
                    balance.UpdatedAt = now;

                    // به‌روزرسانی درصد کش‌بک فعال
                    if (cashback.Percentage.HasValue && cashback.Percentage > 0)
                    {
                        balance.ActiveCashbackPercentage = cashback.Percentage;
                    }

                    // به‌روزرسانی تاریخ انقضا (اگر این تراکنش زودتر منقضی می‌شود یا تاریخ ندارد)
                    if (!balance.ExpiryDate.HasValue || expiryDate < balance.ExpiryDate)
                    {
                        balance.ExpiryDate = expiryDate;
                        balance.ExpiryDays = cashback.ValidityDays;
                    }

                    await _context.SaveChangesAsync();

                    // ارسال پیامک
                    var smsMessage = GenerateCashbackMessage(cashback, cashbackAmount, request.PurchaseAmount > 0 ? request.PurchaseAmount : null);
                    var smsRequest = new SendSmsRequestDto
                    {
                        Mobile = normalizedMobile,
                        Message = smsMessage
                    };

                    var smsResult = await _smsService.SendSmsAsync(smsRequest);

                    // Sid > 0 یعنی پیام ارسال شده (حتی اگر Status = 0 باشد)
                    bool isSmsSent = smsResult.Success && smsResult.Data != null && 
                        (smsResult.Data.Sid > 0 || smsResult.Data.Status > 0);

                    if (isSmsSent)
                    {
                        // به‌روزرسانی وضعیت تراکنش
                        cashbackTransaction.Status = CashbackTransactionStatuses.Deposited;
                        cashbackTransaction.DepositedAt = now;
                        cashbackTransaction.Description = "کش‌بک با موفقیت ارسال شد";

                        // کسر هزینه ارسال پیامک
                        // غیرفعال شده - دیگر از کیف پول کسر نمی‌شود
                        /*
                        await _walletService.DeductBalanceAsync(
                            userId,
                            CostPerSms,
                            "ارسال کش‌بک",
                            $"هزینه ارسال پیامک کش‌بک برای {normalizedMobile}");
                        */

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        // ساخت پیام response
                        string responseMessage;
                        if (hadPreviousBalance)
                        {
                            responseMessage = $"کش‌بک با موفقیت به موجودی قبلی اضافه شد. " +
                                $"موجودی قبلی: {balanceBefore:N0} تومان، " +
                                $"مبلغ افزوده شده: {cashbackAmount:N0} تومان، " +
                                $"موجودی جدید: {balance.TotalBalance:N0} تومان";
                        }
                        else
                        {
                            responseMessage = $"کش‌بک با موفقیت اعمال شد. " +
                                $"مبلغ: {cashbackAmount:N0} تومان، " +
                                $"موجودی جدید: {balance.TotalBalance:N0} تومان";
                        }

                        var result = new ApplyCashbackToContactResultDto
                        {
                            IsSuccess = true,
                            CashbackAmount = cashbackAmount,
                            FormattedCashbackAmount = $"{cashbackAmount:N0} تومان",
                            CashbackTransactionId = cashbackTransaction.Id,
                            CashbackId = cashback.Id,
                            CashbackTitle = cashback.Title,
                            PreviousBalance = balanceBefore,
                            NewBalance = balance.TotalBalance,
                            FormattedPreviousBalance = $"{balanceBefore:N0} تومان",
                            FormattedNewBalance = $"{balance.TotalBalance:N0} تومان",
                            HadPreviousBalance = hadPreviousBalance
                        };

                        _logger.LogInformation("کش‌بک {CashbackId} برای مخاطب {ContactId} ({Mobile}) با موفقیت اعمال شد - مبلغ: {Amount}, موجودی قبلی: {PreviousBalance}, موجودی جدید: {NewBalance}", 
                            cashback.Id, contact.Id, normalizedMobile, cashbackAmount, balanceBefore, balance.TotalBalance);

                        return ApiResponse<ApplyCashbackToContactResultDto>.CreateSuccess(result, responseMessage);
                    }
                    else
                    {
                        // در صورت خطا در ارسال پیامک، موجودی را برگردان
                        balance.TotalBalance = balanceBefore;
                        balance.UsableBalance = balanceBefore;
                        cashbackTransaction.Status = CashbackTransactionStatuses.Failed;
                        cashbackTransaction.Description = ControlledErrorHelper.SmsFailed;
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        return ApiResponse<ApplyCashbackToContactResultDto>.BadRequest(ControlledErrorHelper.SmsFailed);
                    }
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogWarning(ex, "تداخل همزمانی در اعمال کش‌بک برای مخاطب {Mobile}", normalizedMobile);
                    return ApiResponse<ApplyCashbackToContactResultDto>.BadRequest("خطا در پردازش درخواست. لطفاً مجدداً تلاش کنید");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "خطا در اعمال کش‌بک برای مخاطب {Mobile}", normalizedMobile);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در اعمال کش‌بک برای مخاطب {Mobile}", request.MobileNumber);
                throw;
            }
        }

        private string NormalizePhoneNumber(string phoneNumber)
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

        private string GenerateCashbackMessage(Cashback cashback, decimal amount, decimal? purchaseAmount = null)
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

        private static string ToPersianDate(DateTime date)
        {
            try
            {
                var pc = new System.Globalization.PersianCalendar();
                var year = pc.GetYear(date);
                var month = pc.GetMonth(date);
                var day = pc.GetDayOfMonth(date);
                var hour = date.Hour;
                var minute = date.Minute;

                var monthNames = new[] { "", "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", 
                                         "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند" };

                return $"{hour:00}:{minute:00} {day} {monthNames[month]}";
            }
            catch
            {
                return date.ToString("yyyy-MM-dd HH:mm");
            }
        }

        /// <summary>
        /// دریافت لیست شناسه مخاطبین بر اساس معیارهای انتخاب
        /// </summary>
        private async Task<List<int>> GetContactIdsAsync(
            int userId,
            CashbackStep2Dto step2Dto,
            List<int>? targetTagIds,
            bool? sendToSpecificTags)
        {
            IQueryable<Contact> contactsQuery = _context.Contacts
                .Where(c => !c.IsDeleted);

            // فیلتر بر اساس دفترچه‌های کاربر
            var userNotebookIds = await _context.ContactNotebooks
                .Where(cn => cn.UserId == userId && !cn.IsDeleted)
                .Select(cn => cn.Id)
                .ToListAsync();

            contactsQuery = contactsQuery.Where(c => userNotebookIds.Contains(c.ContactNotebookId));

            // فیلتر بر اساس نوع مخاطبین
            if (step2Dto.TargetAudience == CashbackTargetAudience.NewContacts)
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-15);
                contactsQuery = contactsQuery.Where(c => c.CreatedAt >= cutoffDate);
            }
            else if (step2Dto.TargetAudience == CashbackTargetAudience.SpecificNotebooks && 
                     step2Dto.TargetNotebookIds != null && step2Dto.TargetNotebookIds.Any())
            {
                contactsQuery = contactsQuery.Where(c => step2Dto.TargetNotebookIds.Contains(c.ContactNotebookId));
            }

            // فیلتر بر اساس تگ‌ها (اگر فعال باشد)
            if (sendToSpecificTags == true && targetTagIds != null && targetTagIds.Any())
            {
                var contactIdsWithTags = await _context.ContactTags
                    .Where(ct => targetTagIds.Contains(ct.TagId))
                    .Select(ct => ct.ContactId)
                    .Distinct()
                    .ToListAsync();

                contactsQuery = contactsQuery.Where(c => contactIdsWithTags.Contains(c.Id));
            }

            return await contactsQuery.Select(c => c.Id).ToListAsync();
        }

        /// <summary>
        /// دریافت توضیح مخاطبین
        /// </summary>
        private string GetAudienceDescription(
            CashbackStep2Dto step2Dto,
            bool? sendToSpecificTags = null,
            List<int>? targetTagIds = null)
        {
            if (sendToSpecificTags == true && targetTagIds != null && targetTagIds.Any())
            {
                return $"تگ‌های خاص ({targetTagIds.Count} تگ)";
            }

            return step2Dto.TargetAudience switch
            {
                CashbackTargetAudience.All => "همه مخاطبین",
                CashbackTargetAudience.NewContacts => "مخاطبین جدید",
                CashbackTargetAudience.SpecificNotebooks => step2Dto.TargetNotebookIds != null && step2Dto.TargetNotebookIds.Any()
                    ? $"دفترچه خاص ({step2Dto.TargetNotebookIds.Count} دفترچه)"
                    : "دفترچه خاص",
                _ => "همه مخاطبین"
            };
        }

        #endregion

        #region Manual Cashback Methods

        public async Task<ApiResponse<ContactCashbackSummaryDto>> GetContactCashbackSummaryAsync(int userId, int contactId)
        {
            try
            {
                // بررسی دسترسی به مخاطب
                var contact = await _context.Contacts
                    .Include(c => c.ContactNotebook)
                    .FirstOrDefaultAsync(c => c.Id == contactId && !c.IsDeleted);

                if (contact == null)
                {
                    return ApiResponse<ContactCashbackSummaryDto>.NotFound("مخاطب یافت نشد");
                }

                // بررسی مالکیت و حذف نشدن دفترچه
                if (contact.ContactNotebook.UserId != userId || contact.ContactNotebook.IsDeleted)
                {
                    return ApiResponse<ContactCashbackSummaryDto>.Forbidden("شما دسترسی به این مخاطب را ندارید");
                }

                // دریافت یا ایجاد موجودی کش‌بک مخاطب
                var balance = await _context.ContactCashbackBalances
                    .FirstOrDefaultAsync(b => b.ContactId == contactId && b.UserId == userId);

                // محاسبه موجودی از تراکنش‌ها (برای اطمینان از صحت)
                var now = DateTime.UtcNow;
                var totalFromTransactions = await CalculateContactCashbackBalanceAsync(contactId, userId, now);

                // دریافت اطلاعات آخرین کش‌بک فعال کاربر
                var activeCashback = await _context.Cashbacks
                    .Where(c => c.UserId == userId && c.IsActive && !c.IsDeleted && 
                           (c.EndDate == null || c.EndDate > now))
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync();

                // محاسبه تاریخ انقضا و روزهای باقیمانده
                DateTime? expiryDate = null;
                int? expiryDays = null;
                
                if (balance != null && balance.ExpiryDate.HasValue && balance.ExpiryDate > now)
                {
                    expiryDate = balance.ExpiryDate;
                    expiryDays = (int)(balance.ExpiryDate.Value - now).TotalDays;
                    if (expiryDays < 0) expiryDays = 0;
                }
                else
                {
                    // محاسبه از تراکنش‌های دستی
                    var lastAddTransaction = await _context.ManualCashbackTransactions
                        .Where(t => t.ContactId == contactId && t.UserId == userId && 
                               t.TransactionType == ManualCashbackTransactionTypes.Add &&
                               t.ExpiryDate.HasValue && t.ExpiryDate > now)
                        .OrderBy(t => t.ExpiryDate)
                        .FirstOrDefaultAsync();

                    if (lastAddTransaction != null)
                    {
                        expiryDate = lastAddTransaction.ExpiryDate;
                        expiryDays = (int)(lastAddTransaction.ExpiryDate!.Value - now).TotalDays;
                        if (expiryDays < 0) expiryDays = 0;
                    }
                }

                var summary = new ContactCashbackSummaryDto
                {
                    ContactId = contactId,
                    ContactName = contact.FullName ?? contact.MobileNumber,
                    MobileNumber = contact.MobileNumber,
                    TotalCashback = totalFromTransactions,
                    FormattedTotalCashback = $"{totalFromTransactions:N0} تومان",
                    UsableCashback = totalFromTransactions, // در این ورژن، همه موجودی قابل استفاده است
                    FormattedUsableCashback = $"{totalFromTransactions:N0} تومان",
                    ExpiryDays = expiryDays,
                    ExpiryDaysText = expiryDays.HasValue ? $"{expiryDays} روز" : "-",
                    CashbackPercentage = activeCashback?.Percentage,
                    CashbackPercentageText = activeCashback?.Percentage.HasValue == true 
                        ? $"{activeCashback.Percentage}%" 
                        : "-",
                    ExpiryDate = expiryDate,
                    HasCashback = totalFromTransactions > 0,
                    LastUpdatedAt = balance?.UpdatedAt ?? balance?.CreatedAt
                };

                return ApiResponse<ContactCashbackSummaryDto>.CreateSuccess(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت خلاصه کش‌بک مخاطب {ContactId} برای کاربر {UserId}", contactId, userId);
                throw;
            }
        }

        public async Task<ApiResponse<AddManualCashbackResultDto>> AddManualCashbackAsync(int userId, AddManualCashbackDto request)
        {
            const int maxRetries = 3;
            int retryCount = 0;

            while (retryCount < maxRetries)
            {
                try
                {
                    // بررسی دسترسی به مخاطب
                    var contact = await _context.Contacts
                        .Include(c => c.ContactNotebook)
                        .FirstOrDefaultAsync(c => c.Id == request.ContactId && !c.IsDeleted);

                    if (contact == null)
                    {
                        return ApiResponse<AddManualCashbackResultDto>.NotFound("مخاطب یافت نشد");
                    }

                    // بررسی مالکیت مخاطب
                    if (contact.ContactNotebook.UserId != userId)
                    {
                        return ApiResponse<AddManualCashbackResultDto>.Forbidden("شما دسترسی به این مخاطب را ندارید");
                    }

                    // اعتبارسنجی مبلغ
                    if (request.Amount <= 0)
                    {
                        return ApiResponse<AddManualCashbackResultDto>.BadRequest("مبلغ کش‌بک باید بیشتر از صفر باشد");
                    }

                    // بررسی موجودی کیف پول برای ارسال پیامک
                    // غیرفعال شده - دیگر کیف پول چک نمی‌شود
                    /*
                    var walletBalance = await _walletService.GetBalanceAsync(userId);
                    if (walletBalance < CostPerSms)
                    {
                        var requiredAmount = CostPerSms - walletBalance;
                        var message = $"موجودی کیف پول کافی نیست. " +
                            $"برای ارسال این کش‌بک به {CostPerSms:N0} تومان موجودی نیاز دارید. " +
                            $"موجودی فعلی: {walletBalance:N0} تومان. " +
                            $"لطفاً {requiredAmount:N0} تومان به کیف پول خود اضافه کنید.";
                        return ApiResponse<AddManualCashbackResultDto>.BadRequest(message);
                    }
                    */

                    var now = DateTime.UtcNow;

                    // استفاده از IsolationLevel.Serializable برای جلوگیری از Race Condition
                    using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                    try
                    {
                        // دریافت موجودی با Pessimistic Lock (FOR UPDATE)
                        var balance = await _context.ContactCashbackBalances
                            .FromSqlRaw(
                                "SELECT * FROM ContactCashbackBalances WITH (UPDLOCK, ROWLOCK) WHERE ContactId = {0} AND UserId = {1}",
                                request.ContactId, userId)
                            .FirstOrDefaultAsync();

                        decimal balanceBefore = 0;

                        if (balance == null)
                        {
                            // ایجاد رکورد جدید
                            balance = new ContactCashbackBalance
                            {
                                ContactId = request.ContactId,
                                UserId = userId,
                                TotalBalance = 0,
                                UsableBalance = 0,
                                CreatedAt = now
                            };
                            await _context.ContactCashbackBalances.AddAsync(balance);
                            await _context.SaveChangesAsync();
                        }
                        else
                        {
                            balanceBefore = balance.TotalBalance;
                            // Attach entity to track changes
                            _context.Entry(balance).State = EntityState.Modified;
                        }

                        // محاسبه تاریخ انقضا
                        var expiryDate = now.AddDays(request.ValidityDays);

                        // ایجاد تراکنش افزودن کش‌بک
                        var cashbackTransaction = new ManualCashbackTransaction
                        {
                            ContactId = request.ContactId,
                            UserId = userId,
                            TransactionType = ManualCashbackTransactionTypes.Add,
                            Amount = request.Amount,
                            BalanceBefore = balanceBefore,
                            BalanceAfter = balanceBefore + request.Amount,
                            Description = request.Description ?? "افزودن دستی کش‌بک",
                            ExpiryDate = expiryDate,
                            ValidityDays = request.ValidityDays,
                            CreatedAt = now
                        };

                        await _context.ManualCashbackTransactions.AddAsync(cashbackTransaction);

                        // به‌روزرسانی موجودی
                        balance.TotalBalance = balanceBefore + request.Amount;
                        balance.UsableBalance = balance.TotalBalance;
                        balance.UpdatedAt = now;

                        // به‌روزرسانی تاریخ انقضا (اگر این تراکنش زودتر منقضی می‌شود یا تاریخ ندارد)
                        if (!balance.ExpiryDate.HasValue || expiryDate < balance.ExpiryDate)
                        {
                            balance.ExpiryDate = expiryDate;
                            balance.ExpiryDays = request.ValidityDays;
                        }

                        await _context.SaveChangesAsync();

                        // ارسال پیامک (قبل از commit - مشابه ApplyCashbackToContactAsync)
                        try
                        {
                            // دریافت نام کاربر (اختیاری - برای نمایش در پیامک)
                            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                            var businessName = !string.IsNullOrWhiteSpace(user?.FullName) ? user.FullName : "ما";
                            
                            var message = $"🎁 کش‌بک شما به مبلغ {request.Amount:N0} تومان توسط {businessName} به حساب شما واریز شد.\n" +
                                         $"💰 موجودی جدید: {balance.TotalBalance:N0} تومان\n" +
                                         $"⏰ مهلت استفاده: {request.ValidityDays} روز\n" +
                                         "لغو11";

                            var smsRequest = new SendSmsRequestDto
                            {
                                Mobile = contact.MobileNumber,
                                Message = message
                            };

                            var smsResult = await _smsService.SendSmsAsync(smsRequest);

                            // Sid > 0 یعنی پیام ارسال شده (حتی اگر Status = 0 باشد)
                            bool isSmsSent = smsResult.Success && smsResult.Data != null && 
                                (smsResult.Data.Sid > 0 || smsResult.Data.Status > 0);

                            if (isSmsSent)
                            {
                                // TODO: کسر هزینه ارسال پیامک - فعلاً غیرفعال است (موجودی کیف پول کامل نشده)
                                // var deductResult = await _walletService.DeductBalanceAsync(
                                //     userId,
                                //     CostPerSms,
                                //     "ارسال پیامک کش‌بک دستی",
                                //     $"هزینه ارسال پیامک کش‌بک دستی برای {contact.MobileNumber}");

                                // if (deductResult.Success)
                                // {
                                    _logger.LogInformation(
                                        "پیامک کش‌بک دستی با موفقیت ارسال شد - ContactId: {ContactId}, Mobile: {Mobile}",
                                        request.ContactId, contact.MobileNumber);
                                // }
                                // else
                                // {
                                //     _logger.LogWarning(
                                //         "پیامک ارسال شد اما کسر موجودی ناموفق بود - ContactId: {ContactId}, Error: {Error}",
                                //         request.ContactId, deductResult.Message);
                                // }
                            }
                            else
                            {
                                _logger.LogWarning(
                                    "خطا در ارسال پیامک کش‌بک دستی - ContactId: {ContactId}, Mobile: {Mobile}, Error: {Error}",
                                    request.ContactId, contact.MobileNumber, smsResult.Message);
                            }
                        }
                        catch (Exception smsEx)
                        {
                            // در صورت خطا در ارسال پیامک، کش‌بک همچنان اضافه شده است
                            _logger.LogError(smsEx, 
                                "خطا در ارسال پیامک کش‌بک دستی - ContactId: {ContactId}, Mobile: {Mobile}",
                                request.ContactId, contact.MobileNumber);
                        }

                        await transaction.CommitAsync();

                        var result = new AddManualCashbackResultDto
                        {
                            IsSuccess = true,
                            TransactionId = cashbackTransaction.Id,
                            AddedAmount = request.Amount,
                            FormattedAddedAmount = $"{request.Amount:N0} تومان",
                            NewBalance = balance.TotalBalance,
                            FormattedNewBalance = $"{balance.TotalBalance:N0} تومان",
                            ExpiryDate = expiryDate
                        };

                        _logger.LogInformation(
                            "کش‌بک دستی با مبلغ {Amount} تومان به مخاطب {ContactId} توسط کاربر {UserId} افزوده شد - TransactionId: {TransactionId}",
                            request.Amount, request.ContactId, userId, cashbackTransaction.Id);

                        return ApiResponse<AddManualCashbackResultDto>.CreateSuccess(result, "کش‌بک با موفقیت افزوده شد", 201);
                    }
                    catch (DbUpdateConcurrencyException ex)
                    {
                        await transaction.RollbackAsync();
                        retryCount++;
                        _logger.LogWarning(ex, "تداخل همزمانی در افزودن کش‌بک دستی - تلاش {RetryCount} از {MaxRetries}", retryCount, maxRetries);
                        
                        if (retryCount >= maxRetries)
                        {
                            _logger.LogError(ex, "تداخل همزمانی پس از {MaxRetries} تلاش - ContactId: {ContactId}", maxRetries, request.ContactId);
                            return ApiResponse<AddManualCashbackResultDto>.InternalServerError("خطا در پردازش درخواست. لطفاً مجدداً تلاش کنید");
                        }
                        
                        // تأخیر قبل از تلاش مجدد
                        await Task.Delay(100 * retryCount);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "خطا در افزودن کش‌بک دستی به مخاطب {ContactId}", request.ContactId);
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطا در افزودن کش‌بک دستی به مخاطب {ContactId} برای کاربر {UserId}", request.ContactId, userId);
                    throw;
                }
            }

            return ApiResponse<AddManualCashbackResultDto>.InternalServerError("خطای غیرمنتظره در پردازش درخواست");
        }

        public async Task<ApiResponse<WithdrawCashbackResultDto>> WithdrawCashbackAsync(int userId, WithdrawCashbackDto request)
        {
            const int maxRetries = 3;
            int retryCount = 0;

            while (retryCount < maxRetries)
            {
                try
                {
                    // بررسی دسترسی به مخاطب
                    var contact = await _context.Contacts
                        .Include(c => c.ContactNotebook)
                        .FirstOrDefaultAsync(c => c.Id == request.ContactId && !c.IsDeleted);

                    if (contact == null)
                    {
                        return ApiResponse<WithdrawCashbackResultDto>.NotFound("مخاطب یافت نشد");
                    }

                    // بررسی مالکیت مخاطب
                    if (contact.ContactNotebook.UserId != userId)
                    {
                        return ApiResponse<WithdrawCashbackResultDto>.Forbidden("شما دسترسی به این مخاطب را ندارید");
                    }

                    // اعتبارسنجی مبلغ
                    if (request.Amount <= 0)
                    {
                        return ApiResponse<WithdrawCashbackResultDto>.BadRequest("مبلغ برداشت باید بیشتر از صفر باشد");
                    }

                    // TODO: بررسی موجودی کیف پول - فعلاً غیرفعال است (موجودی کیف پول کامل نشده)
                    // var walletBalance = await _walletService.GetBalanceAsync(userId);
                    // if (walletBalance < CostPerSms)
                    // {
                    //     var requiredAmount = CostPerSms - walletBalance;
                    //     var message = $"موجودی کیف پول کافی نیست. " +
                    //         $"برای ارسال این پیامک به {CostPerSms:N0} تومان موجودی نیاز دارید. " +
                    //         $"موجودی فعلی: {walletBalance:N0} تومان. " +
                    //         $"لطفاً {requiredAmount:N0} تومان به کیف پول خود اضافه کنید.";
                    //     return ApiResponse<WithdrawCashbackResultDto>.BadRequest(message);
                    // }

                    var now = DateTime.UtcNow;

                    // استفاده از IsolationLevel.Serializable برای جلوگیری از Race Condition
                    using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                    try
                    {
                        // دریافت موجودی کش‌بک با Pessimistic Lock
                        var balance = await _context.ContactCashbackBalances
                            .FromSqlRaw(
                                "SELECT * FROM ContactCashbackBalances WITH (UPDLOCK, ROWLOCK) WHERE ContactId = {0} AND UserId = {1}",
                                request.ContactId, userId)
                            .FirstOrDefaultAsync();

                        if (balance == null)
                        {
                            await transaction.RollbackAsync();
                            return ApiResponse<WithdrawCashbackResultDto>.BadRequest("این مخاطب موجودی کش‌بک ندارد");
                        }

                        // محاسبه موجودی واقعی از تراکنش‌ها (داخل Lock)
                        var actualBalance = await CalculateContactCashbackBalanceAsync(request.ContactId, userId, now);

                        if (actualBalance <= 0)
                        {
                            await transaction.RollbackAsync();
                            return ApiResponse<WithdrawCashbackResultDto>.BadRequest("این مخاطب موجودی کش‌بک ندارد");
                        }

                        if (request.Amount > actualBalance)
                        {
                            await transaction.RollbackAsync();
                            return ApiResponse<WithdrawCashbackResultDto>.BadRequest(
                                $"مبلغ برداشت نمی‌تواند بیشتر از موجودی فعلی ({actualBalance:N0} تومان) باشد");
                        }

                        var balanceBefore = actualBalance;
                        var balanceAfter = actualBalance - request.Amount;

                        // Attach entity to track changes
                        _context.Entry(balance).State = EntityState.Modified;

                        // ایجاد تراکنش برداشت
                        var cashbackTransaction = new ManualCashbackTransaction
                        {
                            ContactId = request.ContactId,
                            UserId = userId,
                            TransactionType = ManualCashbackTransactionTypes.Withdraw,
                            Amount = request.Amount,
                            BalanceBefore = balanceBefore,
                            BalanceAfter = balanceAfter,
                            Description = request.Reason ?? "برداشت از کش‌بک",
                            CreatedAt = now
                        };

                        await _context.ManualCashbackTransactions.AddAsync(cashbackTransaction);

                        // به‌روزرسانی موجودی
                        balance.TotalBalance = balanceAfter;
                        balance.UsableBalance = balanceAfter;
                        balance.UpdatedAt = now;

                        await _context.SaveChangesAsync();

                        // ارسال پیامک (قبل از commit - مشابه ApplyCashbackToContactAsync)
                        try
                        {
                            // دریافت نام کاربر (اختیاری - برای نمایش در پیامک)
                            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                            var businessName = !string.IsNullOrWhiteSpace(user?.FullName) ? user.FullName : "ما";
                            
                            var message = $"💰 مبلغ {request.Amount:N0} تومان از کش‌بک شما توسط {businessName} کسر شد.\n" +
                                         $"💵 موجودی جدید: {balanceAfter:N0} تومان\n" +
                                         "لغو11";

                            var smsRequest = new SendSmsRequestDto
                            {
                                Mobile = contact.MobileNumber,
                                Message = message
                            };

                            var smsResult = await _smsService.SendSmsAsync(smsRequest);

                            // Sid > 0 یعنی پیام ارسال شده (حتی اگر Status = 0 باشد)
                            bool isSmsSent = smsResult.Success && smsResult.Data != null && 
                                (smsResult.Data.Sid > 0 || smsResult.Data.Status > 0);

                            if (isSmsSent)
                            {
                                // TODO: کسر هزینه ارسال پیامک - فعلاً غیرفعال است (موجودی کیف پول کامل نشده)
                                // var deductResult = await _walletService.DeductBalanceAsync(
                                //     userId,
                                //     CostPerSms,
                                //     "ارسال پیامک برداشت کش‌بک",
                                //     $"هزینه ارسال پیامک برداشت کش‌بک برای {contact.MobileNumber}");

                                // if (deductResult.Success)
                                // {
                                    _logger.LogInformation(
                                        "پیامک برداشت کش‌بک با موفقیت ارسال شد - ContactId: {ContactId}, Mobile: {Mobile}",
                                        request.ContactId, contact.MobileNumber);
                                // }
                                // else
                                // {
                                //     _logger.LogWarning(
                                //         "پیامک ارسال شد اما کسر موجودی ناموفق بود - ContactId: {ContactId}, Error: {Error}",
                                //         request.ContactId, deductResult.Message);
                                // }
                            }
                            else
                            {
                                _logger.LogWarning(
                                    "خطا در ارسال پیامک برداشت کش‌بک - ContactId: {ContactId}, Mobile: {Mobile}, Error: {Error}",
                                    request.ContactId, contact.MobileNumber, smsResult.Message);
                            }
                        }
                        catch (Exception smsEx)
                        {
                            // در صورت خطا در ارسال پیامک، برداشت همچنان انجام شده است
                            _logger.LogError(smsEx, 
                                "خطا در ارسال پیامک برداشت کش‌بک - ContactId: {ContactId}, Mobile: {Mobile}",
                                request.ContactId, contact.MobileNumber);
                        }

                        await transaction.CommitAsync();

                        var result = new WithdrawCashbackResultDto
                        {
                            IsSuccess = true,
                            TransactionId = cashbackTransaction.Id,
                            WithdrawnAmount = request.Amount,
                            FormattedWithdrawnAmount = $"{request.Amount:N0} تومان",
                            NewBalance = balanceAfter,
                            FormattedNewBalance = $"{balanceAfter:N0} تومان"
                        };

                        _logger.LogInformation(
                            "کش‌بک با مبلغ {Amount} تومان از مخاطب {ContactId} توسط کاربر {UserId} برداشت شد - TransactionId: {TransactionId}",
                            request.Amount, request.ContactId, userId, cashbackTransaction.Id);

                        return ApiResponse<WithdrawCashbackResultDto>.CreateSuccess(result, "برداشت با موفقیت انجام شد");
                    }
                    catch (DbUpdateConcurrencyException ex)
                    {
                        await transaction.RollbackAsync();
                        retryCount++;
                        _logger.LogWarning(ex, "تداخل همزمانی در برداشت کش‌بک - تلاش {RetryCount} از {MaxRetries}", retryCount, maxRetries);
                        
                        if (retryCount >= maxRetries)
                        {
                            _logger.LogError(ex, "تداخل همزمانی پس از {MaxRetries} تلاش - ContactId: {ContactId}", maxRetries, request.ContactId);
                            return ApiResponse<WithdrawCashbackResultDto>.InternalServerError("خطا در پردازش درخواست. لطفاً مجدداً تلاش کنید");
                        }
                        
                        // تأخیر قبل از تلاش مجدد
                        await Task.Delay(100 * retryCount);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "خطا در برداشت کش‌بک از مخاطب {ContactId}", request.ContactId);
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطا در برداشت کش‌بک از مخاطب {ContactId} برای کاربر {UserId}", request.ContactId, userId);
                    throw;
                }
            }

            return ApiResponse<WithdrawCashbackResultDto>.InternalServerError("خطای غیرمنتظره در پردازش درخواست");
        }

        public async Task<ApiResponse<ManualCashbackTransactionListDto>> GetManualCashbackTransactionsAsync(
            int userId, 
            int contactId, 
            int pageNumber = 1, 
            int pageSize = 10)
        {
            try
            {
                // بررسی دسترسی به مخاطب
                var contact = await _context.Contacts
                    .Include(c => c.ContactNotebook)
                    .FirstOrDefaultAsync(c => c.Id == contactId && !c.IsDeleted);

                if (contact == null)
                {
                    return ApiResponse<ManualCashbackTransactionListDto>.NotFound("مخاطب یافت نشد");
                }

                // بررسی مالکیت مخاطب
                if (contact.ContactNotebook.UserId != userId)
                {
                    return ApiResponse<ManualCashbackTransactionListDto>.Forbidden("شما دسترسی به این مخاطب را ندارید");
                }

                // اعتبارسنجی پارامترهای صفحه‌بندی
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                var query = _context.ManualCashbackTransactions
                    .Where(t => t.ContactId == contactId && t.UserId == userId)
                    .OrderByDescending(t => t.CreatedAt);

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var transactions = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var transactionDtos = transactions.Select(t => new ManualCashbackTransactionDto
                {
                    Id = t.Id,
                    TransactionType = t.TransactionType,
                    TransactionTypePersian = t.TransactionType == ManualCashbackTransactionTypes.Add 
                        ? "افزودن کش‌بک" 
                        : "برداشت کش‌بک",
                    Amount = t.Amount,
                    FormattedAmount = $"{t.Amount:N0} تومان",
                    BalanceBefore = t.BalanceBefore,
                    BalanceAfter = t.BalanceAfter,
                    Description = t.Description,
                    ExpiryDate = t.ExpiryDate,
                    CreatedAt = t.CreatedAt,
                    FormattedCreatedAt = ToPersianDate(t.CreatedAt)
                }).ToList();

                var result = new ManualCashbackTransactionListDto
                {
                    Transactions = transactionDtos,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                return ApiResponse<ManualCashbackTransactionListDto>.CreateSuccess(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت تراکنش‌های کش‌بک دستی مخاطب {ContactId} برای کاربر {UserId}", contactId, userId);
                throw;
            }
        }

        /// <summary>
        /// محاسبه موجودی کش‌بک مخاطب از روی تراکنش‌ها
        /// </summary>
        private async Task<decimal> CalculateContactCashbackBalanceAsync(int contactId, int userId, DateTime now)
        {
            // جمع کل افزودن‌ها
            var totalAdded = await _context.ManualCashbackTransactions
                .Where(t => t.ContactId == contactId && 
                       t.UserId == userId && 
                       t.TransactionType == ManualCashbackTransactionTypes.Add)
                .SumAsync(t => (decimal?)t.Amount) ?? 0;

            // جمع کل برداشت‌ها
            var totalWithdrawn = await _context.ManualCashbackTransactions
                .Where(t => t.ContactId == contactId && 
                       t.UserId == userId && 
                       t.TransactionType == ManualCashbackTransactionTypes.Withdraw)
                .SumAsync(t => (decimal?)t.Amount) ?? 0;

            // جمع تراکنش‌های کش‌بک اتوماتیک (از سیستم کش‌بک قبلی)
            var totalFromCashbackTransactions = await _context.CashbackTransactions
                .Where(t => t.ContactId == contactId && 
                       t.Status == CashbackTransactionStatuses.Deposited)
                .SumAsync(t => (decimal?)t.Amount) ?? 0;

            return totalAdded + totalFromCashbackTransactions - totalWithdrawn;
        }

        #endregion

        #region Draft Methods

        public async Task<ApiResponse<CashbackDraftDto>> GetCashbackDraftAsync(int userId, string draftId)
        {
            try
            {
                if (string.IsNullOrEmpty(draftId))
                {
                    return ApiResponse<CashbackDraftDto>.BadRequest("شناسه draft الزامی است");
                }

                var draft = await _cashbackDraftRepository.GetActiveByDraftIdAsync(draftId, userId);
                if (draft == null)
                {
                    return ApiResponse<CashbackDraftDto>.BadRequest("Draft یافت نشد یا منقضی شده است");
                }

                var step1Dto = JsonSerializer.Deserialize<CashbackStep1Dto>(draft.Step1Data);
                var step2Dto = !string.IsNullOrEmpty(draft.Step2Data) 
                    ? JsonSerializer.Deserialize<CashbackStep2Dto>(draft.Step2Data) 
                    : null;

                if (step1Dto == null)
                {
                    return ApiResponse<CashbackDraftDto>.BadRequest("خطا در خواندن داده‌های draft");
                }

                var draftDto = new CashbackDraftDto
                {
                    Step1 = step1Dto,
                    Step2 = step2Dto ?? new CashbackStep2Dto()
                };

                _logger.LogInformation("Draft کش‌بک با شناسه {DraftId} برای کاربر {UserId} بازیابی شد", draftId, userId);

                return ApiResponse<CashbackDraftDto>.CreateSuccess(draftDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت draft کش‌بک برای کاربر {UserId}", userId);
                return ApiResponse<CashbackDraftDto>.InternalServerError("خطا در دریافت draft");
            }
        }

        public async Task<ApiResponse<bool>> DeleteCashbackDraftAsync(int userId, string draftId)
        {
            try
            {
                if (string.IsNullOrEmpty(draftId))
                {
                    return ApiResponse<bool>.BadRequest("شناسه draft الزامی است");
                }

                var deleted = await _cashbackDraftRepository.DeleteAsync(draftId, userId);
                if (!deleted)
                {
                    return ApiResponse<bool>.BadRequest("Draft یافت نشد یا شما مجاز به حذف آن نیستید");
                }

                _logger.LogInformation("Draft کش‌بک با شناسه {DraftId} برای کاربر {UserId} حذف شد", draftId, userId);

                return ApiResponse<bool>.CreateSuccess(true, "Draft با موفقیت حذف شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در حذف draft کش‌بک برای کاربر {UserId}", userId);
                return ApiResponse<bool>.InternalServerError("خطا در حذف draft");
            }
        }

        #endregion
    }
}




