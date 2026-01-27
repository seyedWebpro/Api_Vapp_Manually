using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Cashback;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط سرویس کش‌بک
    /// </summary>
    public interface ICashbackService
    {
        /// <summary>
        /// دریافت لیست کش‌بک‌های کاربر
        /// </summary>
        Task<ApiResponse<CashbackListDto>> GetCashbacksAsync(int userId, int pageNumber = 1, int pageSize = 10, bool? isActive = null);

        /// <summary>
        /// دریافت کش‌بک‌های فعال کاربر
        /// </summary>
        Task<ApiResponse<List<CashbackDto>>> GetActiveCashbacksAsync(int userId);

        /// <summary>
        /// دریافت کش‌بک بر اساس شناسه
        /// </summary>
        Task<ApiResponse<CashbackDto>> GetCashbackByIdAsync(int id, int userId);

        /// <summary>
        /// ایجاد کش‌بک جدید
        /// </summary>
        Task<ApiResponse<CashbackDto>> CreateCashbackAsync(int userId, CreateCashbackDto createDto);

        /// <summary>
        /// به‌روزرسانی کش‌بک
        /// </summary>
        Task<ApiResponse<CashbackDto>> UpdateCashbackAsync(int id, int userId, UpdateCashbackDto updateDto);

        /// <summary>
        /// تغییر وضعیت کش‌بک (فعال/غیرفعال)
        /// </summary>
        Task<ApiResponse<CashbackDto>> ToggleStatusAsync(int id, int userId, bool isActive);

        /// <summary>
        /// حذف کش‌بک (Soft Delete)
        /// </summary>
        Task<ApiResponse<bool>> DeleteCashbackAsync(int id, int userId);

        /// <summary>
        /// محاسبه خلاصه هزینه کش‌بک
        /// </summary>
        Task<ApiResponse<CashbackCostSummaryDto>> CalculateCostSummaryAsync(int userId, CreateCashbackDto cashbackDto);

        /// <summary>
        /// دریافت تراکنش‌های کش‌بک
        /// </summary>
        Task<ApiResponse<List<CashbackTransactionDto>>> GetCashbackTransactionsAsync(int cashbackId, int userId, int pageNumber = 1, int pageSize = 10);

        /// <summary>
        /// اعتبارسنجی مرحله 1 ایجاد کش‌بک (نوع و تنظیمات اولیه)
        /// Draft به صورت خودکار ایجاد می‌شود
        /// </summary>
        Task<ApiResponse<CashbackStep1ValidationResponseDto>> ValidateCashbackStep1Async(int userId, CashbackStep1Dto step1Dto);

        /// <summary>
        /// دریافت لیست دفترچه‌ها برای انتخاب در مرحله 2
        /// </summary>
        Task<ApiResponse<List<CashbackNotebookDto>>> GetNotebooksForCashbackAsync(int userId);

        /// <summary>
        /// اعتبارسنجی مرحله 2 ایجاد کش‌بک (انتخاب مخاطبین)
        /// </summary>
        Task<ApiResponse<CashbackStep2ValidationResponseDto>> ValidateCashbackStep2Async(int userId, CashbackStep2Dto step2Dto);

        /// <summary>
        /// دریافت خلاصه نهایی کش‌بک (مرحله 3)
        /// </summary>
        Task<ApiResponse<CashbackFinalSummaryDto>> GetCashbackFinalSummaryAsync(
            int userId, 
            CashbackStep1Dto step1Dto, 
            CashbackStep2Dto step2Dto, 
            CashbackStep3Dto step3Dto);

        /// <summary>
        /// دریافت خلاصه ساده کش‌بک برای نمایش در پایین صفحه (مرحله 3)
        /// می‌تواند از draftId یا داده‌های مستقیم استفاده کند
        /// </summary>
        Task<ApiResponse<CashbackSummaryDto>> GetCashbackSummaryAsync(
            int userId,
            GetCashbackSummaryRequestDto request);

        /// <summary>
        /// ذخیره تنظیمات مرحله 3 (فیلتر تگ و زمان ارسال)
        /// می‌تواند از draftId یا داده‌های مستقیم استفاده کند
        /// </summary>
        Task<ApiResponse<CashbackSummaryDto>> SaveCashbackStep3SettingsAsync(
            int userId,
            SaveCashbackStep3RequestDto request);

        /// <summary>
        /// اعمال کش‌بک به مخاطبین و ارسال پیامک
        /// </summary>
        Task<ApiResponse<ApplyCashbackResultDto>> ApplyCashbackAsync(int cashbackId, int userId, decimal? purchaseAmount = null);

        /// <summary>
        /// اعمال کش‌بک به یک مخاطب خاص (حالت خرید در مغازه)
        /// </summary>
        Task<ApiResponse<ApplyCashbackToContactResultDto>> ApplyCashbackToContactAsync(int userId, ApplyCashbackToContactDto request);

        /// <summary>
        /// دریافت draft کش‌بک بر اساس draftId (برای استفاده داخلی)
        /// </summary>
        Task<ApiResponse<CashbackDraftDto>> GetCashbackDraftAsync(int userId, string draftId);

        /// <summary>
        /// حذف draft کش‌بک (برای استفاده بعد از ایجاد نهایی)
        /// </summary>
        Task<ApiResponse<bool>> DeleteCashbackDraftAsync(int userId, string draftId);

        #region Manual Cashback Methods

        /// <summary>
        /// دریافت خلاصه کش‌بک یک مخاطب خاص
        /// شامل کش‌بک فعلی، قابل استفاده، روز انقضا، درصد کش‌بک
        /// </summary>
        Task<ApiResponse<ContactCashbackSummaryDto>> GetContactCashbackSummaryAsync(int userId, int contactId);

        /// <summary>
        /// افزودن دستی کش‌بک به مخاطب
        /// </summary>
        Task<ApiResponse<AddManualCashbackResultDto>> AddManualCashbackAsync(int userId, AddManualCashbackDto request);

        /// <summary>
        /// برداشت کش‌بک از مخاطب
        /// </summary>
        Task<ApiResponse<WithdrawCashbackResultDto>> WithdrawCashbackAsync(int userId, WithdrawCashbackDto request);

        /// <summary>
        /// دریافت تاریخچه تراکنش‌های کش‌بک دستی مخاطب
        /// </summary>
        Task<ApiResponse<ManualCashbackTransactionListDto>> GetManualCashbackTransactionsAsync(
            int userId, 
            int contactId, 
            int pageNumber = 1, 
            int pageSize = 10);

        #endregion
    }
}




