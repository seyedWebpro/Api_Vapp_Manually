using System.Linq.Expressions;
using Api_Vapp.Constants;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Sms;
using Api_Vapp.DTOs.Wallet;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services;
using Api_Vapp.Services.Audit;
using Api_Vapp.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api_Vapp.Tests.SmsDelivery;

/// <summary>
/// کراول منطقی همه حالت‌های دلیوری و refund کیف پول
/// </summary>
public class SmsDeliveryWalletRefundFlowTests
{
    private static SmsDeliveryTrackingService CreateService(
        FakeDeliveryRepo repo,
        FakeSmsService sms,
        FakeWallet wallet,
        bool refundEnabled = true)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sms:DeliverySync:MaxCheckAttempts"] = "48",
                ["Sms:DeliverySync:MinAgeBeforeFirstCheckMinutes"] = "0",
                ["Sms:DeliverySync:MaxSidsPerBatch"] = "50",
                ["Sms:DeliverySync:RefundOnUndelivered"] = refundEnabled ? "true" : "false"
            })
            .Build();

        return new SmsDeliveryTrackingService(
            repo, sms, wallet, new FakeAudit(), config,
            NullLogger<SmsDeliveryTrackingService>.Instance);
    }

    public static IEnumerable<object[]> AllProviderStatusCases()
    {
        yield return [2, false];
        yield return [0, false];
        yield return [1, false];
        yield return [22, false];
        yield return [3, true];
        yield return [12, true];
        yield return [21, true];
        yield return [28, true];
        yield return [4, true];
        yield return [17, true];
        yield return [19, true];
        yield return [23, true];
        yield return [25, true];
        yield return [27, true];
        yield return [7, true];
    }

    [Theory]
    [MemberData(nameof(AllProviderStatusCases))]
    public async Task RefreshBySid_RefundsOnlyEligibleFinalStatuses(int statusCode, bool expectRefund)
    {
        var repo = new FakeDeliveryRepo();
        var wallet = new FakeWallet();
        var sms = new FakeSmsService();
        var service = CreateService(repo, sms, wallet);

        const int userId = 10;
        const long sid = 900001;
        const decimal charged = 320m;
        const string mobile = "09121234567";

        repo.Seed(new SmsDeliveryRecord
        {
            Id = 1, UserId = userId, Sid = sid, Mobile = mobile,
            ChargedAmount = charged, PartsCount = 2,
            SendStatus = SmsSendStatuses.Sent,
            DeliveryCategory = SmsDeliveryCategories.PendingSync,
            IsDeliveryFinal = false,
            SourceModule = SmsSourceModules.MessageDirect,
            SentAt = DateTime.UtcNow.AddHours(-1),
            CreatedAt = DateTime.UtcNow
        });

        sms.SetDelivery(sid, mobile, statusCode, $"status-{statusCode}");
        var before = wallet.GetBalance(userId);

        var result = await service.RefreshBySidAsync(userId, sid);
        Assert.True(result.Success);

        var record = repo.Get(1)!;
        Assert.Equal(SmsDeliveryStatusMapper.MapToCategory(statusCode), record.DeliveryCategory);
        Assert.Equal(SmsDeliveryStatusMapper.IsFinalStatus(statusCode), record.IsDeliveryFinal);

        if (expectRefund)
        {
            Assert.True(record.WalletRefundTransactionId.HasValue);
            Assert.Equal(before + charged, wallet.GetBalance(userId));
            Assert.Equal(1, wallet.RefundCount(userId));
            Assert.Equal($"SMS-DLR-REFUND-{record.Id}", wallet.LastReference);
        }
        else
        {
            Assert.Null(record.WalletRefundTransactionId);
            Assert.Equal(before, wallet.GetBalance(userId));
            Assert.Equal(0, wallet.RefundCount(userId));
        }
    }

    [Fact]
    public async Task MixedBatch_RefundsOnlyUndeliveredRecipients()
    {
        var repo = new FakeDeliveryRepo();
        var wallet = new FakeWallet();
        var sms = new FakeSmsService();
        var service = CreateService(repo, sms, wallet);

        const int userId = 20;
        const long sid = 900002;

        void Seed(int id, string mobile) => repo.Seed(new SmsDeliveryRecord
        {
            Id = id, UserId = userId, Sid = sid, Mobile = mobile,
            ChargedAmount = 160, PartsCount = 1, SendStatus = SmsSendStatuses.Sent,
            DeliveryCategory = SmsDeliveryCategories.PendingSync, IsDeliveryFinal = false,
            SourceModule = SmsSourceModules.MessageCampaign,
            SentAt = DateTime.UtcNow.AddHours(-1), CreatedAt = DateTime.UtcNow
        });

        Seed(1, "09121111111");
        Seed(2, "09122222222");
        Seed(3, "09123333333");

        sms.SetDeliveries(sid,
        [
            ("09121111111", 2, "رسیده"),
            ("09122222222", 3, "نرسیده"),
            ("09123333333", 4, "رد شده")
        ]);

        var before = wallet.GetBalance(userId);
        await service.RefreshBySidAsync(userId, sid);

        Assert.Null(repo.Get(1)!.WalletRefundTransactionId);
        Assert.NotNull(repo.Get(2)!.WalletRefundTransactionId);
        Assert.NotNull(repo.Get(3)!.WalletRefundTransactionId);
        Assert.Equal(before + 320m, wallet.GetBalance(userId));
        Assert.Equal(2, wallet.RefundCount(userId));
    }

    [Fact]
    public async Task Idempotent_SecondRefresh_DoesNotDoubleRefund()
    {
        var repo = new FakeDeliveryRepo();
        var wallet = new FakeWallet();
        var sms = new FakeSmsService();
        var service = CreateService(repo, sms, wallet);

        const int userId = 30;
        const long sid = 900003;

        repo.Seed(new SmsDeliveryRecord
        {
            Id = 5, UserId = userId, Sid = sid, Mobile = "09120000000",
            ChargedAmount = 500, PartsCount = 1, SendStatus = SmsSendStatuses.Sent,
            DeliveryCategory = SmsDeliveryCategories.PendingSync, IsDeliveryFinal = false,
            SourceModule = SmsSourceModules.Cashback,
            SentAt = DateTime.UtcNow.AddHours(-1), CreatedAt = DateTime.UtcNow
        });

        sms.SetDelivery(sid, "09120000000", 3, "نرسیده");
        await service.RefreshBySidAsync(userId, sid);
        var mid = wallet.GetBalance(userId);
        Assert.Equal(1, wallet.RefundCount(userId));

        await service.RefreshBySidAsync(userId, sid);
        await service.RefreshBySidAsync(userId, sid);

        Assert.Equal(mid, wallet.GetBalance(userId));
        Assert.Equal(1, wallet.RefundCount(userId));
    }

    [Fact]
    public async Task ZeroChargedAmount_NeverRefunds()
    {
        var repo = new FakeDeliveryRepo();
        var wallet = new FakeWallet();
        var sms = new FakeSmsService();
        var service = CreateService(repo, sms, wallet);

        repo.Seed(new SmsDeliveryRecord
        {
            Id = 7, UserId = 40, Sid = 900004, Mobile = "09129999999",
            ChargedAmount = 0, PartsCount = 1, SendStatus = SmsSendStatuses.Sent,
            DeliveryCategory = SmsDeliveryCategories.PendingSync, IsDeliveryFinal = false,
            SourceModule = SmsSourceModules.Manual,
            SentAt = DateTime.UtcNow.AddHours(-1), CreatedAt = DateTime.UtcNow
        });

        sms.SetDelivery(900004, "09129999999", 3, "نرسیده");
        await service.RefreshBySidAsync(40, 900004);

        Assert.Null(repo.Get(7)!.WalletRefundTransactionId);
        Assert.Equal(0, wallet.RefundCount(40));
    }

    [Fact]
    public async Task KillSwitch_DisablesRefund()
    {
        var repo = new FakeDeliveryRepo();
        var wallet = new FakeWallet();
        var sms = new FakeSmsService();
        var service = CreateService(repo, sms, wallet, refundEnabled: false);

        repo.Seed(new SmsDeliveryRecord
        {
            Id = 8, UserId = 50, Sid = 900005, Mobile = "09128888888",
            ChargedAmount = 160, PartsCount = 1, SendStatus = SmsSendStatuses.Sent,
            DeliveryCategory = SmsDeliveryCategories.PendingSync, IsDeliveryFinal = false,
            SourceModule = SmsSourceModules.MessageDirect,
            SentAt = DateTime.UtcNow.AddHours(-1), CreatedAt = DateTime.UtcNow
        });

        sms.SetDelivery(900005, "09128888888", 3, "نرسیده");
        await service.RefreshBySidAsync(50, 900005);

        Assert.True(repo.Get(8)!.IsDeliveryFinal);
        Assert.Equal(SmsDeliveryCategories.NotDelivered, repo.Get(8)!.DeliveryCategory);
        Assert.Null(repo.Get(8)!.WalletRefundTransactionId);
        Assert.Equal(0, wallet.RefundCount(50));
    }

    [Fact]
    public async Task SyncPending_RefundsFinalAwaitingWithoutProviderCall()
    {
        var repo = new FakeDeliveryRepo();
        var wallet = new FakeWallet();
        var sms = new FakeSmsService();
        var service = CreateService(repo, sms, wallet);

        repo.Seed(new SmsDeliveryRecord
        {
            Id = 9, UserId = 60, Sid = 900006, Mobile = "09127777777",
            ChargedAmount = 200, PartsCount = 1, SendStatus = SmsSendStatuses.Sent,
            DeliveryCategory = SmsDeliveryCategories.NotDelivered,
            ProviderStatusCode = 3, IsDeliveryFinal = true,
            SourceModule = SmsSourceModules.BookingReminder,
            SentAt = DateTime.UtcNow.AddHours(-2), CreatedAt = DateTime.UtcNow
        });

        var callsBefore = sms.DeliveryCallCount;
        await service.SyncPendingDeliveriesAsync();

        Assert.Equal(callsBefore, sms.DeliveryCallCount);
        Assert.NotNull(repo.Get(9)!.WalletRefundTransactionId);
        Assert.Equal(1, wallet.RefundCount(60));
        Assert.Equal(200m, wallet.GetBalance(60));
    }

    [Fact]
    public async Task TrackSuccessfulSend_PersistsChargedAmountAndParts()
    {
        var repo = new FakeDeliveryRepo();
        var service = CreateService(repo, new FakeSmsService(), new FakeWallet());

        await service.TrackSuccessfulSendAsync(new SmsDeliveryTrackRequestDto
        {
            UserId = 70, Sid = 900007, Mobile = "09126666666",
            SourceModule = SmsSourceModules.ReferralProgram,
            MessageText = "تست", ChargedAmount = 175, PartsCount = 1
        });

        var record = repo.All.Single();
        Assert.Equal(175m, record.ChargedAmount);
        Assert.Equal(1, record.PartsCount);
        Assert.Equal(SmsDeliveryCategories.PendingSync, record.DeliveryCategory);
    }

    [Fact]
    public async Task WalletReference_IdempotentOnDuplicateCreditAttempt()
    {
        var wallet = new FakeWallet();
        var first = await wallet.AddBalanceAsync(1, 100, WalletTransactionTypes.Refund, "t",
            referenceNumber: "SMS-DLR-REFUND-99", sendPushNotification: false);
        var second = await wallet.AddBalanceAsync(1, 100, WalletTransactionTypes.Refund, "t",
            referenceNumber: "SMS-DLR-REFUND-99", sendPushNotification: false);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(first.Data!.Id, second.Data!.Id);
        Assert.Equal(100m, wallet.GetBalance(1));
        Assert.Equal(1, wallet.RefundCount(1));
    }

    #region Fakes

    private sealed class FakeAudit : IAuditService
    {
        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task WriteRangeAsync(IEnumerable<AuditEntry> entries, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeWallet : IWalletService
    {
        private readonly Dictionary<int, decimal> _balances = new();
        private readonly List<(int TxId, int UserId, decimal Amount, string? Ref)> _txs = new();
        private int _nextTx = 1;

        public string? LastReference { get; private set; }
        public decimal GetBalance(int userId) => _balances.GetValueOrDefault(userId);
        public int RefundCount(int userId) => _txs.Count(t => t.UserId == userId);

        public Task<ApiResponse<WalletTransactionDto>> AddBalanceAsync(
            int userId, decimal amount, string transactionType, string title,
            string? description = null, int? paymentId = null, int? cashbackId = null,
            string? referenceNumber = null, bool sendPushNotification = true)
        {
            if (!string.IsNullOrWhiteSpace(referenceNumber))
            {
                var existing = _txs.FirstOrDefault(t => t.Ref == referenceNumber);
                if (existing.TxId != 0)
                {
                    return Task.FromResult(ApiResponse<WalletTransactionDto>.CreateSuccess(new WalletTransactionDto
                    {
                        Id = existing.TxId,
                        Amount = existing.Amount,
                        ReferenceNumber = existing.Ref
                    }));
                }
            }

            var id = _nextTx++;
            _balances[userId] = GetBalance(userId) + amount;
            _txs.Add((id, userId, amount, referenceNumber));
            LastReference = referenceNumber;
            return Task.FromResult(ApiResponse<WalletTransactionDto>.CreateSuccess(new WalletTransactionDto
            {
                Id = id, Amount = amount, ReferenceNumber = referenceNumber
            }));
        }

        public Task<ApiResponse<WalletInfoDto>> GetWalletInfoAsync(int userId) => throw new NotImplementedException();
        public Task<ApiResponse<WalletTransactionListDto>> GetTransactionsAsync(int userId, int pageNumber = 1, int pageSize = 10) => throw new NotImplementedException();
        public Task<ApiResponse<List<WalletTransactionDto>>> GetRecentTransactionsAsync(int userId, int count = 5) => throw new NotImplementedException();
        public Task<ApiResponse<ChargeWalletResponseDto>> ChargeWalletAsync(int userId, ChargeWalletRequestDto request) => throw new NotImplementedException();
        public Task<ApiResponse<WalletTransactionDto>> DeductBalanceAsync(int userId, decimal amount, string title, string? description = null) => throw new NotImplementedException();
        public Task<bool> HasSufficientBalanceAsync(int userId, decimal amount) => Task.FromResult(true);
        public Task<decimal> GetBalanceAsync(int userId) => Task.FromResult(GetBalance(userId));
        public Task<ApiResponse<WalletPageDto>> GetWalletPageAsync(int userId, int recentTransactionsCount = 10) => throw new NotImplementedException();
    }

    private sealed class FakeSmsService : ISmsService
    {
        private readonly Dictionary<long, List<(string Mobile, int Status, string Message)>> _map = new();
        public int DeliveryCallCount { get; private set; }

        public void SetDelivery(long sid, string mobile, int status, string message) =>
            SetDeliveries(sid, [(mobile, status, message)]);

        public void SetDeliveries(long sid, List<(string Mobile, int Status, string Message)> items) =>
            _map[sid] = items;

        public Task<ApiResponse<DeliveryResponseDto>> GetDeliveryStatusAsync(long sid)
        {
            DeliveryCallCount++;
            if (!_map.TryGetValue(sid, out var items))
            {
                return Task.FromResult(ApiResponse<DeliveryResponseDto>.CreateSuccess(new DeliveryResponseDto
                {
                    Status = 0, Deliveries = []
                }));
            }

            return Task.FromResult(ApiResponse<DeliveryResponseDto>.CreateSuccess(new DeliveryResponseDto
            {
                Status = 0,
                Deliveries = items.Select(i => new DeliveryItemDto
                {
                    Mobile = SmsDeliveryStatusMapper.NormalizeMobile(i.Mobile),
                    Status = i.Status,
                    StatusMessage = i.Message
                }).ToList()
            }));
        }

        public Task<string> GenerateOtpAsync() => Task.FromResult("1234");
        public Task<bool> SendOtpAsync(string phoneNumber, string otpCode, string templateType = "VerifyOtp") => Task.FromResult(true);
        public Task<ApiResponse<SendSmsResponseDto>> SendSmsAsync(SendSmsRequestDto request) => throw new NotImplementedException();
        public Task<ApiResponse<SendBulkResponseDto>> SendBulkSmsAsync(SendBulkRequestDto request) => throw new NotImplementedException();
        public Task<ApiResponse<SendArrayResponseDto>> SendArraySmsAsync(SendArrayRequestDto request) => throw new NotImplementedException();
        public Task<ApiResponse<InboxResponseDto>> GetInboxAsync(InboxRequestDto request) => throw new NotImplementedException();
        public Task<ApiResponse<InfoResponseDto>> GetWalletInfoAsync() => throw new NotImplementedException();
    }

    private sealed class FakeDeliveryRepo : ISmsDeliveryRecordRepository
    {
        private readonly List<SmsDeliveryRecord> _records = new();
        private int _nextId = 1;

        public IReadOnlyList<SmsDeliveryRecord> All => _records;
        public SmsDeliveryRecord? Get(int id) => _records.FirstOrDefault(r => r.Id == id);

        public void Seed(SmsDeliveryRecord r)
        {
            if (r.Id <= 0) r.Id = _nextId++;
            _nextId = Math.Max(_nextId, r.Id + 1);
            _records.Add(r);
        }

        public Task SaveChangesAsync() => Task.CompletedTask;

        public Task<SmsDeliveryRecord?> GetByIdAsync(int id, int userId) =>
            Task.FromResult(_records.FirstOrDefault(r => r.Id == id && r.UserId == userId && !r.IsDeleted));

        public Task<(List<SmsDeliveryRecord> Items, int TotalCount)> GetByUserAsync(int userId, SmsDeliveryReportFilterDto filter) =>
            throw new NotImplementedException();

        public Task<SmsDeliverySummaryDto> GetSummaryAsync(int userId, SmsDeliveryReportFilterDto filter) =>
            throw new NotImplementedException();

        public Task<List<long>> GetDistinctPendingSidsAsync(DateTime sentBeforeUtc, int maxAttempts, int take)
        {
            var cats = SmsDeliveryCategories.WalletRefundEligibleCategories;
            return Task.FromResult(_records
                .Where(r => !r.IsDeleted && r.SendStatus == SmsSendStatuses.Sent && r.Sid > 0 && (
                    (!r.IsDeliveryFinal && r.SentAt <= sentBeforeUtc && r.CheckAttempts < maxAttempts)
                    || (r.IsDeliveryFinal && r.ChargedAmount > 0 && r.WalletRefundTransactionId == null && cats.Contains(r.DeliveryCategory))))
                .Select(r => r.Sid).Distinct().Take(take).ToList());
        }

        public Task<List<SmsDeliveryRecord>> GetActivePendingBySidAsync(long sid, int maxAttempts)
        {
            var cats = SmsDeliveryCategories.WalletRefundEligibleCategories;
            return Task.FromResult(_records.Where(r => !r.IsDeleted && r.Sid == sid && r.SendStatus == SmsSendStatuses.Sent && (
                (!r.IsDeliveryFinal && r.CheckAttempts < maxAttempts)
                || (r.IsDeliveryFinal && r.ChargedAmount > 0 && r.WalletRefundTransactionId == null && cats.Contains(r.DeliveryCategory))
            )).ToList());
        }

        public Task<bool> TryClaimWalletRefundAsync(int recordId, DateTime claimedAtUtc)
        {
            var r = _records.FirstOrDefault(x => x.Id == recordId);
            if (r == null || r.ChargedAmount <= 0 || r.WalletRefundTransactionId.HasValue || !r.IsDeliveryFinal)
                return Task.FromResult(false);
            if (r.WalletRefundedAt.HasValue && r.WalletRefundedAt >= claimedAtUtc.AddMinutes(-2))
                return Task.FromResult(false);
            r.WalletRefundedAt = claimedAtUtc;
            return Task.FromResult(true);
        }

        public Task ClearWalletRefundClaimAsync(int recordId)
        {
            var r = _records.FirstOrDefault(x => x.Id == recordId);
            if (r != null && r.WalletRefundTransactionId == null)
                r.WalletRefundedAt = null;
            return Task.CompletedTask;
        }

        public Task SetWalletRefundTransactionAsync(int recordId, int walletTransactionId, DateTime refundedAtUtc)
        {
            var r = _records.FirstOrDefault(x => x.Id == recordId);
            if (r != null)
            {
                r.WalletRefundTransactionId = walletTransactionId;
                r.WalletRefundedAt = refundedAtUtc;
            }
            return Task.CompletedTask;
        }

        public Task<(List<SmsSendBatchProjection> Items, int TotalCount)> GetSendBatchesAsync(int userId, SmsSendListFilterDto filter) => throw new NotImplementedException();
        public Task<SmsSendBatchProjection?> GetSendBatchBySidAsync(int userId, long sid) => throw new NotImplementedException();
        public Task<SmsSendBatchProjection?> GetSendBatchByCampaignAsync(int userId, int campaignId) => throw new NotImplementedException();
        public Task<(List<SmsDeliveryRecord> Items, int TotalCount)> GetRecipientsBySidAsync(int userId, long sid, SmsSendRecipientFilterDto filter) => throw new NotImplementedException();
        public Task<(List<SmsDeliveryRecord> Items, int TotalCount)> GetRecipientsByCampaignAsync(int userId, int campaignId, SmsSendRecipientFilterDto filter) => throw new NotImplementedException();
        public Task<List<SmsDeliveryRecord>> GetAllRecipientsBySidForExportAsync(int userId, long sid, SmsSendRecipientFilterDto filter, int maxRows) => throw new NotImplementedException();
        public Task<List<SmsDeliveryRecord>> GetAllRecipientsByCampaignForExportAsync(int userId, int campaignId, SmsSendRecipientFilterDto filter, int maxRows) => throw new NotImplementedException();
        public Task<SmsDeliverySummaryDto> GetSummaryBySidAsync(int userId, long sid, SmsSendRecipientFilterDto? filter = null) =>
            Task.FromResult(new SmsDeliverySummaryDto { Total = _records.Count(r => r.UserId == userId && r.Sid == sid) });
        public Task<SmsDeliverySummaryDto> GetSummaryByCampaignAsync(int userId, int campaignId, SmsSendRecipientFilterDto? filter = null) => throw new NotImplementedException();
        public Task<bool> UserOwnsSidAsync(int userId, long sid) =>
            Task.FromResult(_records.Any(r => r.UserId == userId && r.Sid == sid && !r.IsDeleted));
        public Task<bool> UserOwnsCampaignAsync(int userId, int campaignId) => throw new NotImplementedException();
        public Task<int?> TryResolveCampaignIdBySidAsync(int userId, long sid) => Task.FromResult<int?>(null);
        public Task<(string SourceModule, int EntityId)?> TryResolveGroupedBatchBySidAsync(int userId, long sid) => Task.FromResult<(string, int)?>(null);
        public Task<List<long>> GetDistinctSidsByCampaignAsync(int userId, int campaignId) => throw new NotImplementedException();
        public Task<List<long>> GetDistinctSidsByModuleEntityAsync(int userId, string sourceModule, int entityId) => throw new NotImplementedException();
        public Task<List<SmsDeliveryRecord>> GetSentRecordsBySidForUserAsync(int userId, long sid) =>
            Task.FromResult(_records.Where(r => r.UserId == userId && r.Sid == sid && r.SendStatus == SmsSendStatuses.Sent && !r.IsDeleted).ToList());
        public Task<string?> GetSampleMessageTextBySidAsync(int userId, long sid) => Task.FromResult<string?>(null);
        public Task<string?> GetSampleMessageTextByCampaignAsync(int userId, int campaignId) => Task.FromResult<string?>(null);
        public Task<Dictionary<long, string?>> GetSampleMessageTextsBySidsAsync(int userId, IEnumerable<long> sids) => Task.FromResult(new Dictionary<long, string?>());
        public Task<Dictionary<int, string?>> GetSampleMessageTextsByCampaignIdsAsync(int userId, IEnumerable<int> campaignIds) => Task.FromResult(new Dictionary<int, string?>());
        public Task<Dictionary<int, int>> GetCampaignPartsCountsAsync(IEnumerable<int> campaignIds) => Task.FromResult(new Dictionary<int, int>());
        public Task<string?> ResolveCampaignMessageTextAsync(int campaignId, string mobile) => Task.FromResult<string?>(null);
        public Task<string?> ResolveDirectMessageTextAsync(int messageId) => Task.FromResult<string?>(null);

        public Task<SmsDeliveryRecord?> GetByIdAsync(int id) => Task.FromResult(_records.FirstOrDefault(r => r.Id == id));
        public Task<IEnumerable<SmsDeliveryRecord>> GetAllAsync() => Task.FromResult<IEnumerable<SmsDeliveryRecord>>(_records);
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
        public Task DeleteAsync(SmsDeliveryRecord entity) { _records.Remove(entity); return Task.CompletedTask; }
        public Task DeleteAsync(int id) { _records.RemoveAll(r => r.Id == id); return Task.CompletedTask; }
        public Task<int> CountAsync(Expression<Func<SmsDeliveryRecord, bool>>? predicate = null) =>
            Task.FromResult(predicate == null ? _records.Count : _records.AsQueryable().Count(predicate));
    }

    #endregion
}
