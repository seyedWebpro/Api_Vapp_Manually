using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Sms;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp._Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    public class SmsDeliveryRecordRepository : BaseRepository<SmsDeliveryRecord>, ISmsDeliveryRecordRepository
    {
        public SmsDeliveryRecordRepository(Api_Context context) : base(context)
        {
        }

        private IQueryable<SmsDeliveryRecord> UserQuery(int userId) =>
            _dbSet.Where(r => r.UserId == userId && !r.IsDeleted);

        private static IQueryable<SmsDeliveryRecord> ApplyFilter(IQueryable<SmsDeliveryRecord> query, SmsDeliveryReportFilterDto filter)
        {
            if (!string.IsNullOrWhiteSpace(filter.SourceModule))
                query = query.Where(r => r.SourceModule == filter.SourceModule);

            if (filter.SourceEntityId.HasValue)
                query = query.Where(r => r.SourceEntityId == filter.SourceEntityId.Value);

            if (!string.IsNullOrWhiteSpace(filter.DeliveryCategory))
                query = query.Where(r => r.DeliveryCategory == filter.DeliveryCategory);

            if (filter.FromDate.HasValue)
                query = query.Where(r => r.SentAt >= filter.FromDate.Value);

            if (filter.ToDate.HasValue)
                query = query.Where(r => r.SentAt <= filter.ToDate.Value);

            return query;
        }

        public Task SaveChangesAsync() => _context.SaveChangesAsync();

        public Task<SmsDeliveryRecord?> GetByIdAsync(int id, int userId) =>
            UserQuery(userId).FirstOrDefaultAsync(r => r.Id == id);

        public async Task<(List<SmsDeliveryRecord> Items, int TotalCount)> GetByUserAsync(int userId, SmsDeliveryReportFilterDto filter)
        {
            var pageNumber = filter.PageNumber < 1 ? 1 : filter.PageNumber;
            var pageSize = filter.PageSize < 1 ? 20 : Math.Min(filter.PageSize, 100);

            var query = ApplyFilter(UserQuery(userId), filter);
            var totalCount = await query.CountAsync();

            var items = await query
                .AsNoTracking()
                .OrderByDescending(r => r.SentAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<SmsDeliverySummaryDto> GetSummaryAsync(int userId, SmsDeliveryReportFilterDto filter)
        {
            var query = ApplyFilter(UserQuery(userId), filter);

            var grouped = await query
                .GroupBy(r => r.DeliveryCategory)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .ToListAsync();

            var summary = new SmsDeliverySummaryDto
            {
                Total = grouped.Sum(x => x.Count)
            };

            foreach (var item in grouped)
            {
                switch (item.Category)
                {
                    case SmsDeliveryCategories.DeliveredToPhone:
                        summary.DeliveredToPhone = item.Count;
                        break;
                    case SmsDeliveryCategories.SentToOperator:
                        summary.SentToOperator = item.Count;
                        break;
                    case SmsDeliveryCategories.NotDelivered:
                        summary.NotDelivered = item.Count;
                        break;
                    case SmsDeliveryCategories.PendingApproval:
                        summary.PendingApproval = item.Count;
                        break;
                    case SmsDeliveryCategories.Rejected:
                        summary.Rejected = item.Count;
                        break;
                    case SmsDeliveryCategories.PendingSync:
                        summary.PendingSync = item.Count;
                        break;
                    case SmsDeliveryCategories.SendFailed:
                        summary.SendFailed = item.Count;
                        break;
                }
            }

            return summary;
        }

        public Task<List<long>> GetDistinctPendingSidsAsync(DateTime sentBeforeUtc, int maxAttempts, int take) =>
            _dbSet
                .Where(r => !r.IsDeleted
                    && r.SendStatus == SmsSendStatuses.Sent
                    && !r.IsDeliveryFinal
                    && r.Sid > 0
                    && r.SentAt <= sentBeforeUtc
                    && r.CheckAttempts < maxAttempts)
                .Select(r => r.Sid)
                .Distinct()
                .OrderBy(sid => sid)
                .Take(take)
                .ToListAsync();

        public Task<List<SmsDeliveryRecord>> GetActivePendingBySidAsync(long sid, int maxAttempts) =>
            _dbSet
                .Where(r => !r.IsDeleted
                    && r.Sid == sid
                    && r.SendStatus == SmsSendStatuses.Sent
                    && !r.IsDeliveryFinal
                    && r.CheckAttempts < maxAttempts)
                .ToListAsync();
    }
}
