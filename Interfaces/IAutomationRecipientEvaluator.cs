using Api_Vapp.Models;
using Api_Vapp.Utilities;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// ارزیابی واجد شرایط بودن مخاطبین برای انواع پیام خودکار
    /// </summary>
    public interface IAutomationRecipientEvaluator
    {
        Task<IReadOnlyDictionary<int, AutomationContactMetrics>> LoadContactMetricsAsync(
            int userId,
            IEnumerable<int> contactIds,
            CancellationToken cancellationToken = default);

        bool IsCashbackExpiryEligible(AutomationContactMetrics metrics, DateTime todayUtc, int daysBeforeExpiry);

        bool IsPurchaseReminderEligible(AutomationContactMetrics metrics, DateTime todayUtc, int daysWithoutPurchase);

        bool IsCustomEligible(AutomationContactMetrics metrics, DateTime todayUtc, CustomAutomationConditions conditions);
    }

    /// <summary>
    /// داده‌های تجمیعی لازم برای ارزیابی اتوماسیون یک مخاطب
    /// </summary>
    public sealed class AutomationContactMetrics
    {
        public int ContactId { get; init; }

        public DateTime CreatedAt { get; init; }

        public bool HasDateOfBirth { get; init; }

        public decimal CashbackBalance { get; init; }

        public DateTime? CashbackExpiryDate { get; init; }

        public DateTime? LastPurchaseAt { get; init; }
    }
}
