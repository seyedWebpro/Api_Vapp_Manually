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

            // ارسال گروهی (کمپین / پیام مستقیم به دفترچه / پیام خودکار): یک ردیف به‌ازای SourceEntityId
            // بقیه ماژول‌ها: یک ردیف به‌ازای Sid ایران‌نوین
            var campaignModule = SmsSourceModules.MessageCampaign;
            var directModule = SmsSourceModules.MessageDirect;
            var automatedModule = SmsSourceModules.AutomatedMessage;

            var groupedModules = new[]
            {
                SmsSourceModules.MessageCampaign,
                SmsSourceModules.MessageDirect,
                SmsSourceModules.AutomatedMessage
            };

            IQueryable<SmsSendBatchProjection> grouped;

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim();
                List<int> matchingCampaignIds;
                List<int> matchingDirectIds;
                List<int> matchingAutomatedIds;
                List<long> matchingSids;

                if (long.TryParse(search, out var idSearch))
                {
                    matchingCampaignIds = await GetMatchingGroupedEntityIdsAsync(
                        baseQuery, campaignModule, idSearch);
                    matchingDirectIds = await GetMatchingGroupedEntityIdsAsync(
                        baseQuery, directModule, idSearch);
                    matchingAutomatedIds = await GetMatchingGroupedEntityIdsAsync(
                        baseQuery, automatedModule, idSearch);

                    matchingSids = await baseQuery
                        .Where(r => r.Sid == idSearch
                            && (r.SourceEntityId == null || !groupedModules.Contains(r.SourceModule)))
                        .Select(r => r.Sid)
                        .Distinct()
                        .ToListAsync();
                }
                else
                {
                    matchingCampaignIds = new List<int>();
                    matchingDirectIds = new List<int>();
                    matchingAutomatedIds = new List<int>();
                    matchingSids = new List<long>();
                }

                if (matchingCampaignIds.Count == 0
                    && matchingDirectIds.Count == 0
                    && matchingAutomatedIds.Count == 0
                    && matchingSids.Count == 0)
                {
                    matchingCampaignIds = await GetMatchingGroupedEntityIdsByTitleAsync(
                        baseQuery, campaignModule, search);
                    matchingDirectIds = await GetMatchingGroupedEntityIdsByTitleAsync(
                        baseQuery, directModule, search);
                    matchingAutomatedIds = await GetMatchingGroupedEntityIdsByTitleAsync(
                        baseQuery, automatedModule, search);

                    matchingSids = await baseQuery
                        .Where(r => (r.SourceEntityId == null || !groupedModules.Contains(r.SourceModule))
                            && r.SourceEntityLabel != null
                            && r.SourceEntityLabel.Contains(search))
                        .Select(r => r.Sid)
                        .Distinct()
                        .ToListAsync();
                }

                if (matchingCampaignIds.Count == 0
                    && matchingDirectIds.Count == 0
                    && matchingAutomatedIds.Count == 0
                    && matchingSids.Count == 0)
                    return (new List<SmsSendBatchProjection>(), 0);

                baseQuery = baseQuery.Where(r =>
                    (r.SourceModule == campaignModule
                        && r.SourceEntityId != null
                        && matchingCampaignIds.Contains(r.SourceEntityId.Value))
                    || (r.SourceModule == directModule
                        && r.SourceEntityId != null
                        && matchingDirectIds.Contains(r.SourceEntityId.Value))
                    || (r.SourceModule == automatedModule
                        && r.SourceEntityId != null
                        && matchingAutomatedIds.Contains(r.SourceEntityId.Value))
                    || ((r.SourceEntityId == null || !groupedModules.Contains(r.SourceModule))
                        && matchingSids.Contains(r.Sid)));
            }

            var entityGroups = baseQuery
                .Where(r => r.SourceEntityId != null && groupedModules.Contains(r.SourceModule))
                .GroupBy(r => new { r.SourceModule, EntityId = r.SourceEntityId!.Value })
                .Select(g => new SmsSendBatchProjection
                {
                    Sid = g.Min(x => x.Sid),
                    SendId = g.Key.EntityId,
                    IsCampaignBatch = g.Key.SourceModule == campaignModule,
                    Title = g.Max(x => x.SourceEntityLabel),
                    SourceModule = g.Key.SourceModule,
                    SourceEntityId = g.Key.EntityId,
                    SendCount = g.Count(),
                    SentAt = g.Min(x => x.SentAt)
                });

            var sidGroups = baseQuery
                .Where(r => r.SourceEntityId == null || !groupedModules.Contains(r.SourceModule))
                .GroupBy(r => r.Sid)
                .Select(g => new SmsSendBatchProjection
                {
                    Sid = g.Key,
                    SendId = g.Key,
                    IsCampaignBatch = false,
                    Title = g.Max(x => x.SourceEntityLabel),
                    SourceModule = g.Max(x => x.SourceModule)!,
                    SourceEntityId = g.Max(x => x.SourceEntityId),
                    SendCount = g.Count(),
                    SentAt = g.Min(x => x.SentAt)
                });

            grouped = entityGroups.Concat(sidGroups);

            var totalCount = await grouped.CountAsync();

            var items = await grouped
                .AsNoTracking()
                .OrderByDescending(x => x.SentAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        private static Task<List<int>> GetMatchingGroupedEntityIdsAsync(
            IQueryable<SmsDeliveryRecord> baseQuery, string sourceModule, long idSearch) =>
            baseQuery
                .Where(r => r.SourceModule == sourceModule
                    && r.SourceEntityId != null
                    && (r.Sid == idSearch
                        || (idSearch <= int.MaxValue && r.SourceEntityId.Value == (int)idSearch)))
                .Select(r => r.SourceEntityId!.Value)
                .Distinct()
                .ToListAsync();

        private static Task<List<int>> GetMatchingGroupedEntityIdsByTitleAsync(
            IQueryable<SmsDeliveryRecord> baseQuery, string sourceModule, string search) =>
            baseQuery
                .Where(r => r.SourceModule == sourceModule
                    && r.SourceEntityId != null
                    && r.SourceEntityLabel != null
                    && r.SourceEntityLabel.Contains(search))
                .Select(r => r.SourceEntityId!.Value)
                .Distinct()
                .ToListAsync();

        public async Task<SmsSendBatchProjection?> GetSendBatchBySidAsync(int userId, long sid)
        {
            var grouped = await TryResolveGroupedBatchBySidAsync(userId, sid);
            if (grouped.HasValue)
                return await GetSendBatchByModuleEntityAsync(userId, grouped.Value.SourceModule, grouped.Value.EntityId);

            return await UserQuery(userId)
                .Where(r => r.Sid == sid)
                .GroupBy(r => r.Sid)
                .Select(g => new SmsSendBatchProjection
                {
                    Sid = g.Key,
                    SendId = g.Key,
                    IsCampaignBatch = false,
                    Title = g.Max(x => x.SourceEntityLabel),
                    SourceModule = g.Max(x => x.SourceModule)!,
                    SourceEntityId = g.Max(x => x.SourceEntityId),
                    SendCount = g.Count(),
                    SentAt = g.Min(x => x.SentAt)
                })
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }

        public Task<SmsSendBatchProjection?> GetSendBatchByCampaignAsync(int userId, int campaignId) =>
            GetSendBatchByModuleEntityAsync(userId, SmsSourceModules.MessageCampaign, campaignId);

        private Task<SmsSendBatchProjection?> GetSendBatchByModuleEntityAsync(
            int userId, string sourceModule, int entityId)
        {
            var isCampaign = sourceModule == SmsSourceModules.MessageCampaign;
            return UserQuery(userId)
                .Where(r => r.SourceModule == sourceModule && r.SourceEntityId == entityId)
                .GroupBy(r => r.SourceEntityId!.Value)
                .Select(g => new SmsSendBatchProjection
                {
                    Sid = g.Min(x => x.Sid),
                    SendId = g.Key,
                    IsCampaignBatch = isCampaign,
                    Title = g.Max(x => x.SourceEntityLabel),
                    SourceModule = sourceModule,
                    SourceEntityId = g.Key,
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

        public Task<string?> GetSampleMessageTextByCampaignAsync(int userId, int campaignId) =>
            UserQuery(userId)
                .AsNoTracking()
                .Where(r => r.SourceModule == SmsSourceModules.MessageCampaign
                    && r.SourceEntityId == campaignId
                    && r.MessageText != null
                    && r.MessageText != "")
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

        public async Task<Dictionary<int, string?>> GetSampleMessageTextsByCampaignIdsAsync(
            int userId, IEnumerable<int> campaignIds)
        {
            var idList = campaignIds.Distinct().ToList();
            if (idList.Count == 0)
                return new Dictionary<int, string?>();

            var rows = await UserQuery(userId)
                .AsNoTracking()
                .Where(r => r.SourceModule == SmsSourceModules.MessageCampaign
                    && r.SourceEntityId != null
                    && idList.Contains(r.SourceEntityId.Value)
                    && r.MessageText != null
                    && r.MessageText != "")
                .Select(r => new { CampaignId = r.SourceEntityId!.Value, r.Id, r.MessageText })
                .ToListAsync();

            return rows
                .GroupBy(r => r.CampaignId)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Id).First().MessageText);
        }

        public async Task<(List<SmsDeliveryRecord> Items, int TotalCount)> GetRecipientsBySidAsync(
            int userId, long sid, SmsSendRecipientFilterDto filter)
        {
            var grouped = await TryResolveGroupedBatchBySidAsync(userId, sid);
            if (grouped.HasValue)
                return await GetRecipientsByModuleEntityAsync(
                    userId, grouped.Value.SourceModule, grouped.Value.EntityId, filter);

            return await GetRecipientsInternalAsync(
                ApplyRecipientFilter(UserQuery(userId).Where(r => r.Sid == sid), filter),
                filter);
        }

        public Task<(List<SmsDeliveryRecord> Items, int TotalCount)> GetRecipientsByCampaignAsync(
            int userId, int campaignId, SmsSendRecipientFilterDto filter) =>
            GetRecipientsByModuleEntityAsync(userId, SmsSourceModules.MessageCampaign, campaignId, filter);

        private Task<(List<SmsDeliveryRecord> Items, int TotalCount)> GetRecipientsByModuleEntityAsync(
            int userId, string sourceModule, int entityId, SmsSendRecipientFilterDto filter) =>
            GetRecipientsInternalAsync(
                ApplyRecipientFilter(
                    UserQuery(userId).Where(r =>
                        r.SourceModule == sourceModule
                        && r.SourceEntityId == entityId),
                    filter),
                filter);

        private async Task<(List<SmsDeliveryRecord> Items, int TotalCount)> GetRecipientsInternalAsync(
            IQueryable<SmsDeliveryRecord> query,
            SmsSendRecipientFilterDto filter)
        {
            var pageNumber = filter.PageNumber < 1 ? 1 : filter.PageNumber;
            var pageSize = filter.PageSize < 1 ? 20 : Math.Min(filter.PageSize, 100);

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
            var grouped = await TryResolveGroupedBatchBySidAsync(userId, sid);
            if (grouped.HasValue)
                return await GetAllRecipientsByModuleEntityForExportAsync(
                    userId, grouped.Value.SourceModule, grouped.Value.EntityId, filter, maxRows);

            var take = Math.Clamp(maxRows, 1, 50_000);

            return await ApplyRecipientFilter(
                    UserQuery(userId).Where(r => r.Sid == sid),
                    filter)
                .AsNoTracking()
                .OrderBy(r => r.Id)
                .Take(take)
                .ToListAsync();
        }

        public Task<List<SmsDeliveryRecord>> GetAllRecipientsByCampaignForExportAsync(
            int userId, int campaignId, SmsSendRecipientFilterDto filter, int maxRows) =>
            GetAllRecipientsByModuleEntityForExportAsync(
                userId, SmsSourceModules.MessageCampaign, campaignId, filter, maxRows);

        private async Task<List<SmsDeliveryRecord>> GetAllRecipientsByModuleEntityForExportAsync(
            int userId, string sourceModule, int entityId, SmsSendRecipientFilterDto filter, int maxRows)
        {
            var take = Math.Clamp(maxRows, 1, 50_000);

            return await ApplyRecipientFilter(
                    UserQuery(userId).Where(r =>
                        r.SourceModule == sourceModule
                        && r.SourceEntityId == entityId),
                    filter)
                .AsNoTracking()
                .OrderBy(r => r.Id)
                .Take(take)
                .ToListAsync();
        }

        public async Task<SmsDeliverySummaryDto> GetSummaryBySidAsync(
            int userId, long sid, SmsSendRecipientFilterDto? filter = null)
        {
            var grouped = await TryResolveGroupedBatchBySidAsync(userId, sid);
            if (grouped.HasValue)
                return await GetSummaryByModuleEntityAsync(
                    userId, grouped.Value.SourceModule, grouped.Value.EntityId, filter);

            var query = UserQuery(userId).Where(r => r.Sid == sid);
            if (filter != null)
                query = ApplyRecipientFilter(query, filter);

            return await BuildSummaryAsync(query);
        }

        public Task<SmsDeliverySummaryDto> GetSummaryByCampaignAsync(
            int userId, int campaignId, SmsSendRecipientFilterDto? filter = null) =>
            GetSummaryByModuleEntityAsync(userId, SmsSourceModules.MessageCampaign, campaignId, filter);

        private async Task<SmsDeliverySummaryDto> GetSummaryByModuleEntityAsync(
            int userId, string sourceModule, int entityId, SmsSendRecipientFilterDto? filter = null)
        {
            var query = UserQuery(userId).Where(r =>
                r.SourceModule == sourceModule
                && r.SourceEntityId == entityId);

            if (filter != null)
                query = ApplyRecipientFilter(query, filter);

            return await BuildSummaryAsync(query);
        }

        private async Task<SmsDeliverySummaryDto> BuildSummaryAsync(IQueryable<SmsDeliveryRecord> query)
        {
            var grouped = await query
                .GroupBy(r => r.DeliveryCategory)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .ToListAsync();

            return BuildSummaryFromGrouped(grouped.Select(x => (x.Category, x.Count)).ToList());
        }

        public Task<bool> UserOwnsSidAsync(int userId, long sid) =>
            UserQuery(userId).AnyAsync(r => r.Sid == sid);

        public Task<bool> UserOwnsCampaignAsync(int userId, int campaignId) =>
            UserQuery(userId).AnyAsync(r =>
                r.SourceModule == SmsSourceModules.MessageCampaign
                && r.SourceEntityId == campaignId);

        public async Task<int?> TryResolveCampaignIdBySidAsync(int userId, long sid)
        {
            var grouped = await TryResolveGroupedBatchBySidAsync(userId, sid);
            if (grouped.HasValue && grouped.Value.SourceModule == SmsSourceModules.MessageCampaign)
                return grouped.Value.EntityId;

            return null;
        }

        public async Task<(string SourceModule, int EntityId)?> TryResolveGroupedBatchBySidAsync(int userId, long sid)
        {
            var hit = await UserQuery(userId)
                .AsNoTracking()
                .Where(r => r.Sid == sid)
                .Select(r => new { r.SourceModule, r.SourceEntityId })
                .FirstOrDefaultAsync();

            if (hit?.SourceEntityId == null)
                return null;

            if (!SmsSourceModules.IsGroupedReportModule(hit.SourceModule))
                return null;

            return (hit.SourceModule, hit.SourceEntityId.Value);
        }

        public Task<List<long>> GetDistinctSidsByCampaignAsync(int userId, int campaignId) =>
            GetDistinctSidsByModuleEntityAsync(userId, SmsSourceModules.MessageCampaign, campaignId);

        public Task<List<long>> GetDistinctSidsByModuleEntityAsync(int userId, string sourceModule, int entityId) =>
            UserQuery(userId)
                .AsNoTracking()
                .Where(r => r.SourceModule == sourceModule
                    && r.SourceEntityId == entityId
                    && r.Sid > 0)
                .Select(r => r.Sid)
                .Distinct()
                .ToListAsync();

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
