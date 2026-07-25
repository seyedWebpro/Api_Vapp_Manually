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
            {
                var toDate = SmsReportDateRangePresets.NormalizeToDateEndOfDay(filter.ToDate.Value)!;
                query = query.Where(r => r.SentAt <= toDate);
            }

            return query;
        }

        private static IQueryable<SmsDeliveryRecord> ApplySendListBaseFilter(
            IQueryable<SmsDeliveryRecord> query,
            SmsSendListFilterDto filter)
        {
            var (fromDate, toDate) = SmsReportDateRangePresets.Resolve(
                filter.DateRangePreset, filter.FromDate, filter.ToDate);

            if (fromDate.HasValue)
                query = query.Where(r => r.SentAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(r => r.SentAt <= toDate.Value);

            var modules = SmsSendTypeFilters.ResolveSourceModules(filter.SendType);
            if (modules.Count > 0)
                query = query.Where(r => modules.Contains(r.SourceModule));

            return query;
        }

        private static IQueryable<SmsDeliveryRecord> ApplyRecipientFilter(
            IQueryable<SmsDeliveryRecord> query,
            SmsSendRecipientFilterDto filter)
        {
            if (!string.IsNullOrWhiteSpace(filter.DeliveryCategory))
                query = query.Where(r => r.DeliveryCategory == filter.DeliveryCategory);

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim();
                query = query.Where(r => r.Mobile.Contains(search));
            }

            return query;
        }

        private static SmsDeliverySummaryDto BuildSummaryFromGrouped(List<(string Category, int Count)> grouped)
        {
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

            return BuildSummaryFromGrouped(grouped.Select(x => (x.Category, x.Count)).ToList());
        }

        public Task<List<long>> GetDistinctPendingSidsAsync(DateTime sentBeforeUtc, int maxAttempts, int take) =>
            _dbSet
                .Where(r => !r.IsDeleted
                    && r.SendStatus == SmsSendStatuses.Sent
                    && !r.IsDeliveryFinal
                    && r.Sid > 0
                    && r.SentAt <= sentBeforeUtc
                    && r.CheckAttempts < maxAttempts)
                .GroupBy(r => r.Sid)
                .Select(g => new { Sid = g.Key, OldestSentAt = g.Min(x => x.SentAt) })
                .OrderBy(x => x.OldestSentAt)
                .ThenBy(x => x.Sid)
                .Take(take)
                .Select(x => x.Sid)
                .ToListAsync();

        public Task<List<SmsDeliveryRecord>> GetActivePendingBySidAsync(long sid, int maxAttempts) =>
            _dbSet
                .Where(r => !r.IsDeleted
                    && r.Sid == sid
                    && r.SendStatus == SmsSendStatuses.Sent
                    && !r.IsDeliveryFinal
                    && r.CheckAttempts < maxAttempts)
                .ToListAsync();

        public async Task<(List<SmsSendBatchProjection> Items, int TotalCount)> GetSendBatchesAsync(
            int userId, SmsSendListFilterDto filter)
        {
            var pageNumber = filter.PageNumber < 1 ? 1 : filter.PageNumber;
            var pageSize = filter.PageSize < 1 ? 20 : Math.Min(filter.PageSize, 100);

            var baseQuery = ApplySendListBaseFilter(UserQuery(userId), filter);

            var grouped = baseQuery
                .GroupBy(r => r.Sid)
                .Select(g => new SmsSendBatchProjection
                {
                    Sid = g.Key,
                    Title = g.Max(x => x.SourceEntityLabel),
                    SourceModule = g.Max(x => x.SourceModule)!,
                    SourceEntityId = g.Max(x => x.SourceEntityId),
                    SendCount = g.Count(),
                    SentAt = g.Min(x => x.SentAt)
                });

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim();
                if (long.TryParse(search, out var sidSearch))
                {
                    grouped = grouped.Where(x =>
                        x.Sid == sidSearch ||
                        (x.Title != null && x.Title.Contains(search)));
                }
                else
                {
                    grouped = grouped.Where(x => x.Title != null && x.Title.Contains(search));
                }
            }

            var totalCount = await grouped.CountAsync();

            var items = await grouped
                .AsNoTracking()
                .OrderByDescending(x => x.SentAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<SmsSendBatchProjection?> GetSendBatchBySidAsync(int userId, long sid)
        {
            return await UserQuery(userId)
                .Where(r => r.Sid == sid)
                .GroupBy(r => r.Sid)
                .Select(g => new SmsSendBatchProjection
                {
                    Sid = g.Key,
                    Title = g.Max(x => x.SourceEntityLabel),
                    SourceModule = g.Max(x => x.SourceModule)!,
                    SourceEntityId = g.Max(x => x.SourceEntityId),
                    SendCount = g.Count(),
                    SentAt = g.Min(x => x.SentAt)
                })
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }

        public Task<string?> GetSampleMessageTextBySidAsync(int userId, long sid) =>
            UserQuery(userId)
                .AsNoTracking()
                .Where(r => r.Sid == sid && r.MessageText != null && r.MessageText != "")
                .OrderBy(r => r.Id)
                .Select(r => r.MessageText)
                .FirstOrDefaultAsync();

        public async Task<Dictionary<long, string?>> GetSampleMessageTextsBySidsAsync(int userId, IEnumerable<long> sids)
        {
            var sidList = sids.Distinct().ToList();
            if (sidList.Count == 0)
                return new Dictionary<long, string?>();

            var rows = await UserQuery(userId)
                .AsNoTracking()
                .Where(r => sidList.Contains(r.Sid) && r.MessageText != null && r.MessageText != "")
                .Select(r => new { r.Sid, r.Id, r.MessageText })
                .ToListAsync();

            return rows
                .GroupBy(r => r.Sid)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Id).First().MessageText);
        }

        public async Task<(List<SmsDeliveryRecord> Items, int TotalCount)> GetRecipientsBySidAsync(
            int userId, long sid, SmsSendRecipientFilterDto filter)
        {
            var pageNumber = filter.PageNumber < 1 ? 1 : filter.PageNumber;
            var pageSize = filter.PageSize < 1 ? 20 : Math.Min(filter.PageSize, 100);

            var query = ApplyRecipientFilter(
                UserQuery(userId).Where(r => r.Sid == sid),
                filter);

            var totalCount = await query.CountAsync();

            var items = await query
                .AsNoTracking()
                .OrderBy(r => r.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<List<SmsDeliveryRecord>> GetAllRecipientsBySidForExportAsync(
            int userId, long sid, SmsSendRecipientFilterDto filter, int maxRows)
        {
            var take = Math.Clamp(maxRows, 1, 50_000);

            return await ApplyRecipientFilter(
                    UserQuery(userId).Where(r => r.Sid == sid),
                    filter)
                .AsNoTracking()
                .OrderBy(r => r.Id)
                .Take(take)
                .ToListAsync();
        }

        public async Task<SmsDeliverySummaryDto> GetSummaryBySidAsync(
            int userId, long sid, SmsSendRecipientFilterDto? filter = null)
        {
            var query = UserQuery(userId).Where(r => r.Sid == sid);
            if (filter != null)
                query = ApplyRecipientFilter(query, filter);

            var grouped = await query
                .GroupBy(r => r.DeliveryCategory)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .ToListAsync();

            return BuildSummaryFromGrouped(grouped.Select(x => (x.Category, x.Count)).ToList());
        }

        public Task<bool> UserOwnsSidAsync(int userId, long sid) =>
            UserQuery(userId).AnyAsync(r => r.Sid == sid);

        public Task<List<SmsDeliveryRecord>> GetSentRecordsBySidForUserAsync(int userId, long sid) =>
            UserQuery(userId)
                .Where(r => r.Sid == sid && r.SendStatus == SmsSendStatuses.Sent && r.Sid > 0)
                .ToListAsync();

        public async Task<Dictionary<int, int>> GetCampaignPartsCountsAsync(IEnumerable<int> campaignIds)
        {
            var ids = campaignIds.Distinct().ToList();
            if (ids.Count == 0)
                return new Dictionary<int, int>();

            return await _context.MessageCampaigns
                .AsNoTracking()
                .Where(c => ids.Contains(c.Id) && !c.IsDeleted)
                .Select(c => new { c.Id, c.PartsCount })
                .ToDictionaryAsync(c => c.Id, c => c.PartsCount);
        }

        public async Task<string?> ResolveCampaignMessageTextAsync(int campaignId, string mobile)
        {
            if (!string.IsNullOrWhiteSpace(mobile))
            {
                var personalized = await _context.MessageRecipients
                    .AsNoTracking()
                    .Where(r => r.CampaignId == campaignId && r.MobileNumber == mobile)
                    .Select(r => r.PersonalizedContent)
                    .FirstOrDefaultAsync();

                if (!string.IsNullOrWhiteSpace(personalized))
                    return personalized;
            }

            return await _context.MessageCampaigns
                .AsNoTracking()
                .Where(c => c.Id == campaignId && !c.IsDeleted)
                .Select(c => c.Message != null ? c.Message.Content : null)
                .FirstOrDefaultAsync();
        }

        public Task<string?> ResolveDirectMessageTextAsync(int messageId) =>
            _context.Messages
                .AsNoTracking()
                .Where(m => m.Id == messageId && !m.IsDeleted)
                .Select(m => (string?)m.Content)
                .FirstOrDefaultAsync();
    }
}
