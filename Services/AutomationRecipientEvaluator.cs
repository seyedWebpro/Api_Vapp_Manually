using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Services
{
    /// <summary>
    /// ارزیابی واجد شرایط بودن مخاطبین برای پیام‌های خودکار
    /// </summary>
    public class AutomationRecipientEvaluator : IAutomationRecipientEvaluator
    {
        private readonly Api_Context _context;

        public AutomationRecipientEvaluator(Api_Context context)
        {
            _context = context;
        }

        public async Task<IReadOnlyDictionary<int, AutomationContactMetrics>> LoadContactMetricsAsync(
            int userId,
            IEnumerable<int> contactIds,
            CancellationToken cancellationToken = default)
        {
            var ids = contactIds.Distinct().ToList();
            if (ids.Count == 0)
                return new Dictionary<int, AutomationContactMetrics>();

            var now = DateTime.UtcNow;

            var contacts = await _context.Contacts
                .AsNoTracking()
                .Include(c => c.AdditionalInfo)
                .Where(c => ids.Contains(c.Id) && !c.IsDeleted)
                .Select(c => new
                {
                    c.Id,
                    c.CreatedAt,
                    HasDateOfBirth = c.AdditionalInfo != null && c.AdditionalInfo.DateOfBirth.HasValue
                })
                .ToListAsync(cancellationToken);

            var manualAdds = await _context.ManualCashbackTransactions
                .AsNoTracking()
                .Where(t => t.UserId == userId
                    && ids.Contains(t.ContactId)
                    && t.TransactionType == ManualCashbackTransactionTypes.Add)
                .GroupBy(t => t.ContactId)
                .Select(g => new
                {
                    ContactId = g.Key,
                    TotalAdded = g.Sum(x => x.Amount)
                })
                .ToDictionaryAsync(x => x.ContactId, x => x.TotalAdded, cancellationToken);

            var manualWithdraws = await _context.ManualCashbackTransactions
                .AsNoTracking()
                .Where(t => t.UserId == userId
                    && ids.Contains(t.ContactId)
                    && t.TransactionType == ManualCashbackTransactionTypes.Withdraw)
                .GroupBy(t => t.ContactId)
                .Select(g => new
                {
                    ContactId = g.Key,
                    TotalWithdrawn = g.Sum(x => x.Amount)
                })
                .ToDictionaryAsync(x => x.ContactId, x => x.TotalWithdrawn, cancellationToken);

            var depositedCashbacks = await _context.CashbackTransactions
                .AsNoTracking()
                .Where(t => ids.Contains(t.ContactId)
                    && t.Status == CashbackTransactionStatuses.Deposited
                    && t.Cashback.UserId == userId)
                .GroupBy(t => t.ContactId)
                .Select(g => new
                {
                    ContactId = g.Key,
                    TotalDeposited = g.Sum(x => x.Amount),
                    LastPurchaseAt = g.Max(x => x.DepositedAt ?? x.CreatedAt)
                })
                .ToDictionaryAsync(x => x.ContactId, cancellationToken);

            var referralPurchases = await _context.ReferralUsages
                .AsNoTracking()
                .Where(u => u.UserId == userId
                    && u.CustomerContactId.HasValue
                    && ids.Contains(u.CustomerContactId.Value)
                    && u.Status == ReferralUsageStatuses.Completed)
                .GroupBy(u => u.CustomerContactId!.Value)
                .Select(g => new
                {
                    ContactId = g.Key,
                    LastPurchaseAt = g.Max(x => x.CreatedAt)
                })
                .ToDictionaryAsync(x => x.ContactId, x => x.LastPurchaseAt, cancellationToken);

            var balances = await _context.ContactCashbackBalances
                .AsNoTracking()
                .Where(b => b.UserId == userId && ids.Contains(b.ContactId))
                .ToDictionaryAsync(b => b.ContactId, cancellationToken);

            var futureManualExpiries = await _context.ManualCashbackTransactions
                .AsNoTracking()
                .Where(t => t.UserId == userId
                    && ids.Contains(t.ContactId)
                    && t.TransactionType == ManualCashbackTransactionTypes.Add
                    && t.ExpiryDate.HasValue
                    && t.ExpiryDate > now)
                .GroupBy(t => t.ContactId)
                .Select(g => new
                {
                    ContactId = g.Key,
                    ExpiryDate = g.Min(x => x.ExpiryDate)
                })
                .ToDictionaryAsync(x => x.ContactId, x => x.ExpiryDate, cancellationToken);

            var metrics = new Dictionary<int, AutomationContactMetrics>(ids.Count);

            foreach (var contact in contacts)
            {
                manualAdds.TryGetValue(contact.Id, out var totalAdded);
                manualWithdraws.TryGetValue(contact.Id, out var totalWithdrawn);
                depositedCashbacks.TryGetValue(contact.Id, out var depositedInfo);

                var cashbackBalance = totalAdded + (depositedInfo?.TotalDeposited ?? 0) - totalWithdrawn;
                if (cashbackBalance < 0)
                    cashbackBalance = 0;

                DateTime? lastPurchaseAt = depositedInfo?.LastPurchaseAt;
                if (referralPurchases.TryGetValue(contact.Id, out var referralLastPurchase))
                {
                    lastPurchaseAt = lastPurchaseAt.HasValue
                        ? (referralLastPurchase > lastPurchaseAt.Value ? referralLastPurchase : lastPurchaseAt)
                        : referralLastPurchase;
                }

                DateTime? expiryDate = null;
                if (balances.TryGetValue(contact.Id, out var balance)
                    && balance.TotalBalance > 0
                    && balance.ExpiryDate.HasValue
                    && balance.ExpiryDate > now)
                {
                    expiryDate = balance.ExpiryDate;
                }

                if (futureManualExpiries.TryGetValue(contact.Id, out var manualExpiry))
                {
                    expiryDate = expiryDate.HasValue
                        ? (manualExpiry < expiryDate ? manualExpiry : expiryDate)
                        : manualExpiry;
                }

                metrics[contact.Id] = new AutomationContactMetrics
                {
                    ContactId = contact.Id,
                    CreatedAt = contact.CreatedAt,
                    HasDateOfBirth = contact.HasDateOfBirth,
                    CashbackBalance = cashbackBalance,
                    CashbackExpiryDate = expiryDate,
                    LastPurchaseAt = lastPurchaseAt
                };
            }

            return metrics;
        }

        public bool IsCashbackExpiryEligible(AutomationContactMetrics metrics, DateTime todayUtc, int daysBeforeExpiry)
        {
            if (daysBeforeExpiry < 1 || metrics.CashbackBalance <= 0 || !metrics.CashbackExpiryDate.HasValue)
                return false;

            var targetExpiryDate = todayUtc.Date.AddDays(daysBeforeExpiry);
            return metrics.CashbackExpiryDate.Value.Date == targetExpiryDate;
        }

        public bool IsPurchaseReminderEligible(AutomationContactMetrics metrics, DateTime todayUtc, int daysWithoutPurchase)
        {
            if (daysWithoutPurchase < 1)
                return false;

            var cutoffDate = todayUtc.Date.AddDays(-daysWithoutPurchase);

            if (metrics.LastPurchaseAt.HasValue)
                return metrics.LastPurchaseAt.Value.Date <= cutoffDate;

            return metrics.CreatedAt.Date <= cutoffDate;
        }

        public bool IsCustomEligible(AutomationContactMetrics metrics, DateTime todayUtc, CustomAutomationConditions conditions)
        {
            if (!conditions.HasAnyRule)
                return false;

            if (conditions.DaysWithoutPurchase.HasValue
                && !IsPurchaseReminderEligible(metrics, todayUtc, conditions.DaysWithoutPurchase.Value))
            {
                return false;
            }

            if (conditions.DaysSinceContactCreated.HasValue)
            {
                var minCreatedDate = todayUtc.Date.AddDays(-conditions.DaysSinceContactCreated.Value);
                if (metrics.CreatedAt.Date > minCreatedDate)
                    return false;
            }

            if (conditions.MinCashbackBalance.HasValue && metrics.CashbackBalance < conditions.MinCashbackBalance.Value)
                return false;

            if (conditions.MaxCashbackBalance.HasValue && metrics.CashbackBalance > conditions.MaxCashbackBalance.Value)
                return false;

            if (conditions.HasCashback.HasValue)
            {
                var hasCashback = metrics.CashbackBalance > 0;
                if (conditions.HasCashback.Value != hasCashback)
                    return false;
            }

            if (conditions.HasDateOfBirth.HasValue && conditions.HasDateOfBirth.Value != metrics.HasDateOfBirth)
                return false;

            if (conditions.MinDaysUntilCashbackExpiry.HasValue
                || conditions.MaxDaysUntilCashbackExpiry.HasValue)
            {
                if (!metrics.CashbackExpiryDate.HasValue || metrics.CashbackBalance <= 0)
                    return false;

                var daysUntilExpiry = (int)(metrics.CashbackExpiryDate.Value.Date - todayUtc.Date).TotalDays;

                if (conditions.MinDaysUntilCashbackExpiry.HasValue && daysUntilExpiry < conditions.MinDaysUntilCashbackExpiry.Value)
                    return false;

                if (conditions.MaxDaysUntilCashbackExpiry.HasValue && daysUntilExpiry > conditions.MaxDaysUntilCashbackExpiry.Value)
                    return false;
            }

            return true;
        }
    }
}
