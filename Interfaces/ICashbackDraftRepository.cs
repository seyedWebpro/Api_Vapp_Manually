using Api_Vapp.Models;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط Repository برای CashbackDraft
    /// </summary>
    public interface ICashbackDraftRepository
    {
        /// <summary>
        /// افزودن Draft جدید
        /// </summary>
        Task<CashbackDraft> AddAsync(CashbackDraft draft);

        /// <summary>
        /// به‌روزرسانی Draft
        /// </summary>
        Task<CashbackDraft> UpdateAsync(CashbackDraft draft);

        /// <summary>
        /// دریافت Draft بر اساس DraftId
        /// </summary>
        Task<CashbackDraft?> GetByDraftIdAsync(string draftId, int userId);

        /// <summary>
        /// دریافت Draft فعال بر اساس DraftId (غیر حذف شده و منقضی نشده)
        /// </summary>
        Task<CashbackDraft?> GetActiveByDraftIdAsync(string draftId, int userId);

        /// <summary>
        /// حذف Draft (Soft Delete)
        /// </summary>
        Task<bool> DeleteAsync(string draftId, int userId);

        /// <summary>
        /// حذف Draft های منقضی شده
        /// </summary>
        Task<int> DeleteExpiredAsync();
    }
}
