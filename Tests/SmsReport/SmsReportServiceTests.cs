using Api_Vapp.Constants;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Sms;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services;
using Api_Vapp._Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq.Expressions;
using Xunit;

namespace Api_Vapp.Tests.SmsReport;

public class SmsReportServiceTests
{
    [Fact]
    public async Task GetFilterOptions_ReturnsSendTypesAndPresets()
    {
        var service = CreateService(new FakeSmsDeliveryRecordRepository());

        var result = await service.GetFilterOptionsAsync();

        Assert.True(result.Success);
        Assert.Contains(result.Data!.SendTypes, x => x.Value == SmsSendTypeFilters.Campaign);
        Assert.Contains(result.Data.DateRangePresets, x => x.Value == SmsReportDateRangePresets.Last7Days);
        Assert.Contains(result.Data.DeliveryCategories, x => x.Value == SmsDeliveryCategories.DeliveredToPhone);
    }

    [Fact]
    public async Task GetSendBatches_GroupsBySid_AndMapsLabels()
    {
        var repo = new FakeSmsDeliveryRecordRepository();
        repo.SeedBatch(userId: 7, sid: 4217652, title: "تخفیف تابستان", module: SmsSourceModules.MessageCampaign, count: 3);
        var service = CreateService(repo);

        var result = await service.GetSendBatchesAsync(7, new SmsSendListFilterDto
        {
            DateRangePreset = SmsReportDateRangePresets.Custom,
            FromDate = DateTime.UtcNow.AddDays(-30),
            ToDate = DateTime.UtcNow.AddDays(1),
            PageNumber = 1,
            PageSize = 20
        });

        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.TotalCount);
        Assert.Single(result.Data.Items);
        Assert.Equal(4217652, result.Data.Items[0].Sid);
        Assert.Equal("تخفیف تابستان", result.Data.Items[0].Title);
        Assert.Equal(SmsSendTypeFilters.Campaign, result.Data.Items[0].SendType);
        Assert.Equal(3, result.Data.Items[0].SendCount);
    }

    [Fact]
    public async Task GetSendBatchDetail_NotFound_Returns404()
    {
        var service = CreateService(new FakeSmsDeliveryRecordRepository());

        var result = await service.GetSendBatchDetailAsync(7, 999);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetMessageDetail_ReturnsStoredMessageText()
    {
        var repo = new FakeSmsDeliveryRecordRepository();
        var recordId = repo.SeedRecord(7, 1001, "09120000000", "سلام تست", SmsDeliveryCategories.DeliveredToPhone);
        var service = CreateService(repo);

        var result = await service.GetMessageDetailAsync(7, recordId);

        Assert.True(result.Success);
        Assert.Equal("سلام تست", result.Data!.MessageText);
        Assert.Equal("09120000000", result.Data.Mobile);
        Assert.Contains("تحویل", result.Data.StatusHint);
    }

    [Fact]
    public async Task ExportRecipients_ReturnsExcelBytes()
    {
        var repo = new FakeSmsDeliveryRecordRepository();
        repo.SeedBatch(7, 55, "کمپین تست", SmsSourceModules.Cashback, 2);
        var service = CreateService(repo);

        var result = await service.ExportRecipientsToExcelAsync(7, 55, new SmsSendRecipientFilterDto());

        Assert.True(result.Success);
        Assert.True(result.Data!.FileContent.Length > 0);
        Assert.Equal(2, result.Data.ExportedCount);
        Assert.EndsWith(".xlsx", result.Data.FileName);
    }

    [Fact]
    public async Task GetSendBatches_InvalidSendType_Returns400()
    {
        var service = CreateService(new FakeSmsDeliveryRecordRepository());

        var result = await service.GetSendBatchesAsync(7, new SmsSendListFilterDto
        {
            SendType = "InvalidType"
        });

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(ErrorCodes.InvalidInput, result.ErrorCode);
    }

    [Fact]
    public async Task GetSendBatches_InvalidDatePreset_Returns400()
    {
        var service = CreateService(new FakeSmsDeliveryRecordRepository());

        var result = await service.GetSendBatchesAsync(7, new SmsSendListFilterDto
        {
            DateRangePreset = "Last999Days"
        });

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(ErrorCodes.InvalidInput, result.ErrorCode);
    }

    [Fact]
    public async Task GetSendBatches_UsesMessageTextForPartsCount()
    {
        var repo = new FakeSmsDeliveryRecordRepository();
        // متن فارسی بیش از ۷۰ کاراکتر → حداقل ۲ پارت
        var longText = new string('ا', 80);
        repo.SeedRecord(7, 88, "09121111111", longText, SmsDeliveryCategories.PendingSync, "تست پارت", SmsSourceModules.Cashback);
        var service = CreateService(repo);

        var result = await service.GetSendBatchesAsync(7, new SmsSendListFilterDto
        {
            DateRangePreset = SmsReportDateRangePresets.Custom,
            FromDate = DateTime.UtcNow.AddDays(-1),
            ToDate = DateTime.UtcNow.AddDays(1)
        });

        Assert.True(result.Success);
        Assert.True(result.Data!.Items[0].PartsCount >= 2);
    }

    private static SmsReportService CreateService(ISmsDeliveryRecordRepository repository)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sms:SenderNumber"] = "90002034"
            })
            .Build();

        return new SmsReportService(
            repository,
            new FakeTrackingService(),
            new FakeSmsPricingService(),
            config,
            NullLogger<SmsReportService>.Instance);
    }

    private sealed class FakeSmsPricingService : ISmsPricingService
    {
        public Task<SmsPricingRuntime> GetRuntimeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(SmsPricingRuntime.Defaults);

        public Task<ApiResponse<SmsPricingSettingResponseDto>> GetAdminSettingsAsync() =>
            Task.FromResult(ApiResponse<SmsPricingSettingResponseDto>.CreateSuccess(new SmsPricingSettingResponseDto()));

        public Task<ApiResponse<SmsPricingSettingResponseDto>> UpdateAdminSettingsAsync(UpdateSmsPricingSettingDto dto) =>
            Task.FromResult(ApiResponse<SmsPricingSettingResponseDto>.CreateSuccess(new SmsPricingSettingResponseDto()));

        public Task<ApiResponse<SmsPricingPreviewResponseDto>> PreviewAsync(SmsPricingPreviewRequestDto dto) =>
            Task.FromResult(ApiResponse<SmsPricingPreviewResponseDto>.CreateSuccess(new SmsPricingPreviewResponseDto()));
    }

    private sealed class FakeTrackingService : ISmsDeliveryTrackingService
    {
        public Task TrackSuccessfulSendAsync(SmsDeliveryTrackRequestDto request) => Task.CompletedTask;

        public Task<ApiResponse<SmsDeliveryRecordDto>> GetByIdAsync(int userId, int id) =>
            Task.FromResult(ApiResponse<SmsDeliveryRecordDto>.NotFound());

        public Task<ApiResponse<SmsDeliveryReportListDto>> GetReportAsync(int userId, SmsDeliveryReportFilterDto filter) =>
            Task.FromResult(ApiResponse<SmsDeliveryReportListDto>.CreateSuccess(new SmsDeliveryReportListDto()));

        public Task<ApiResponse<SmsDeliverySummaryDto>> GetSummaryAsync(int userId, SmsDeliveryReportFilterDto filter) =>
            Task.FromResult(ApiResponse<SmsDeliverySummaryDto>.CreateSuccess(new SmsDeliverySummaryDto()));

        public Task<ApiResponse<SmsDeliveryRecordDto>> RefreshRecordAsync(int userId, int id) =>
            Task.FromResult(ApiResponse<SmsDeliveryRecordDto>.NotFound());

        public Task<ApiResponse<SmsDeliverySummaryDto>> RefreshBySidAsync(int userId, long sid) =>
            Task.FromResult(ApiResponse<SmsDeliverySummaryDto>.CreateSuccess(new SmsDeliverySummaryDto()));

        public Task SyncPendingDeliveriesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeSmsDeliveryRecordRepository : ISmsDeliveryRecordRepository
    {
        private readonly List<SmsDeliveryRecord> _records = new();
        private int _nextId = 1;

        public void SeedBatch(int userId, long sid, string title, string module, int count)
        {
            for (var i = 0; i < count; i++)
            {
                SeedRecord(userId, sid, $"0912{i:0000000}", $"متن {i}", SmsDeliveryCategories.PendingSync, title, module);
            }
        }

        public int SeedRecord(
            int userId,
            long sid,
            string mobile,
            string messageText,
            string category,
            string title = "عنوان تست",
            string module = SmsSourceModules.MessageCampaign)
        {
            var record = new SmsDeliveryRecord
            {
                Id = _nextId++,
                UserId = userId,
                Sid = sid,
                Mobile = mobile,
                MessageText = messageText,
                SourceModule = module,
                SourceEntityId = 10,
                SourceEntityLabel = title,
                DeliveryCategory = category,
                SendStatus = SmsSendStatuses.Sent,
                SentAt = DateTime.UtcNow.AddHours(-2),
                CreatedAt = DateTime.UtcNow
            };
            _records.Add(record);
            return record.Id;
        }

        public Task SaveChangesAsync() => Task.CompletedTask;

        public Task<SmsDeliveryRecord?> GetByIdAsync(int id, int userId) =>
            Task.FromResult(_records.FirstOrDefault(r => r.Id == id && r.UserId == userId && !r.IsDeleted));

        public Task<(List<SmsDeliveryRecord> Items, int TotalCount)> GetByUserAsync(int userId, SmsDeliveryReportFilterDto filter) =>
            throw new NotImplementedException();

        public Task<SmsDeliverySummaryDto> GetSummaryAsync(int userId, SmsDeliveryReportFilterDto filter) =>
            throw new NotImplementedException();

        public Task<List<long>> GetDistinctPendingSidsAsync(DateTime sentBeforeUtc, int maxAttempts, int take) =>
            throw new NotImplementedException();

        public Task<List<SmsDeliveryRecord>> GetActivePendingBySidAsync(long sid, int maxAttempts) =>
            throw new NotImplementedException();

        public Task<(List<SmsSendBatchProjection> Items, int TotalCount)> GetSendBatchesAsync(int userId, SmsSendListFilterDto filter)
        {
            var items = _records
                .Where(r => r.UserId == userId && !r.IsDeleted)
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
                .OrderByDescending(x => x.SentAt)
                .ToList();

            return Task.FromResult((items, items.Count));
        }

        public Task<SmsSendBatchProjection?> GetSendBatchBySidAsync(int userId, long sid)
        {
            var items = _records.Where(r => r.UserId == userId && r.Sid == sid && !r.IsDeleted).ToList();
            if (items.Count == 0)
                return Task.FromResult<SmsSendBatchProjection?>(null);

            return Task.FromResult<SmsSendBatchProjection?>(new SmsSendBatchProjection
            {
                Sid = sid,
                Title = items.Max(x => x.SourceEntityLabel),
                SourceModule = items.Max(x => x.SourceModule)!,
                SourceEntityId = items.Max(x => x.SourceEntityId),
                SendCount = items.Count,
                SentAt = items.Min(x => x.SentAt)
            });
        }

        public Task<(List<SmsDeliveryRecord> Items, int TotalCount)> GetRecipientsBySidAsync(
            int userId, long sid, SmsSendRecipientFilterDto filter)
        {
            var items = _records.Where(r => r.UserId == userId && r.Sid == sid && !r.IsDeleted).ToList();
            return Task.FromResult((items, items.Count));
        }

        public Task<List<SmsDeliveryRecord>> GetAllRecipientsBySidForExportAsync(
            int userId, long sid, SmsSendRecipientFilterDto filter, int maxRows)
        {
            var items = _records.Where(r => r.UserId == userId && r.Sid == sid && !r.IsDeleted).Take(maxRows).ToList();
            return Task.FromResult(items);
        }

        public Task<SmsDeliverySummaryDto> GetSummaryBySidAsync(int userId, long sid, SmsSendRecipientFilterDto? filter = null)
        {
            var items = _records.Where(r => r.UserId == userId && r.Sid == sid && !r.IsDeleted).ToList();
            return Task.FromResult(new SmsDeliverySummaryDto
            {
                Total = items.Count,
                DeliveredToPhone = items.Count(x => x.DeliveryCategory == SmsDeliveryCategories.DeliveredToPhone),
                PendingSync = items.Count(x => x.DeliveryCategory == SmsDeliveryCategories.PendingSync)
            });
        }

        public Task<bool> UserOwnsSidAsync(int userId, long sid) =>
            Task.FromResult(_records.Any(r => r.UserId == userId && r.Sid == sid && !r.IsDeleted));

        public Task<List<SmsDeliveryRecord>> GetSentRecordsBySidForUserAsync(int userId, long sid) =>
            Task.FromResult(_records.Where(r => r.UserId == userId && r.Sid == sid && !r.IsDeleted).ToList());

        public Task<string?> GetSampleMessageTextBySidAsync(int userId, long sid) =>
            Task.FromResult(_records.Where(r => r.UserId == userId && r.Sid == sid && r.MessageText != null)
                .OrderBy(r => r.Id)
                .Select(r => r.MessageText)
                .FirstOrDefault());

        public Task<Dictionary<long, string?>> GetSampleMessageTextsBySidsAsync(int userId, IEnumerable<long> sids)
        {
            var sidSet = sids.ToHashSet();
            var dict = _records
                .Where(r => r.UserId == userId && sidSet.Contains(r.Sid) && r.MessageText != null)
                .GroupBy(r => r.Sid)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Id).First().MessageText);
            return Task.FromResult(dict);
        }

        public Task<Dictionary<int, int>> GetCampaignPartsCountsAsync(IEnumerable<int> campaignIds) =>
            Task.FromResult(new Dictionary<int, int>());

        public Task<string?> ResolveCampaignMessageTextAsync(int campaignId, string mobile) =>
            Task.FromResult<string?>(null);

        public Task<string?> ResolveDirectMessageTextAsync(int messageId) =>
            Task.FromResult<string?>(null);

        public Task<SmsDeliveryRecord?> GetByIdAsync(int id) =>
            Task.FromResult(_records.FirstOrDefault(r => r.Id == id));

        public Task<IEnumerable<SmsDeliveryRecord>> GetAllAsync() =>
            Task.FromResult<IEnumerable<SmsDeliveryRecord>>(_records);

        public Task<IEnumerable<SmsDeliveryRecord>> FindAsync(Expression<Func<SmsDeliveryRecord, bool>> predicate) =>
            Task.FromResult(_records.AsQueryable().Where(predicate).AsEnumerable());

        public Task<SmsDeliveryRecord?> FirstOrDefaultAsync(Expression<Func<SmsDeliveryRecord, bool>> predicate) =>
            Task.FromResult(_records.AsQueryable().FirstOrDefault(predicate));

        public Task<bool> AnyAsync(Expression<Func<SmsDeliveryRecord, bool>> predicate) =>
            Task.FromResult(_records.AsQueryable().Any(predicate));

        public Task<SmsDeliveryRecord> AddAsync(SmsDeliveryRecord entity)
        {
            entity.Id = _nextId++;
            _records.Add(entity);
            return Task.FromResult(entity);
        }

        public Task<SmsDeliveryRecord> UpdateAsync(SmsDeliveryRecord entity) => Task.FromResult(entity);

        public Task DeleteAsync(SmsDeliveryRecord entity)
        {
            _records.Remove(entity);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(int id)
        {
            _records.RemoveAll(r => r.Id == id);
            return Task.CompletedTask;
        }

        public Task<int> CountAsync(Expression<Func<SmsDeliveryRecord, bool>>? predicate = null) =>
            Task.FromResult(predicate == null ? _records.Count : _records.AsQueryable().Count(predicate));
    }
}
