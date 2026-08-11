using Api_Vapp.Data;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Wallet;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Repositories;
using Api_Vapp.Services;
using Api_Vapp.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api_Vapp.Tests.WalletReferral;

public class WalletReferralServiceTests
{
    [Fact]
    public async Task FulfillWalletChargeWithReferral_ValidMeta_CreditsBeneficiaryAndReferrerExactlyOnce()
    {
        await using var db = await WalletReferralTestDb.CreateAsync();

        var referrer = new User
        {
            PhoneNumber = "09120000001",
            PasswordHash = "hash",
            FullName = "Referrer",
            IsActive = true,
            IsPhoneVerified = true,
            ReferralCode = "@ref100",
            CreatedAt = DateTime.UtcNow
        };
        var beneficiary = new User
        {
            PhoneNumber = "09120000002",
            PasswordHash = "hash",
            FullName = "Beneficiary",
            IsActive = true,
            IsPhoneVerified = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Context.Users.AddRange(referrer, beneficiary);
        await db.Context.SaveChangesAsync();

        var meta = new WalletReferralPaymentMetaDto
        {
            ReferralCode = "@ref100",
            ReferrerUserId = referrer.Id,
            RequestedAmount = 100_000m,
            PayableAmount = 90_000m,
            DiscountAmount = 10_000m,
            DiscountPercent = 10m,
            BonusAmount = 5_000m,
            BonusPercent = 5m
        };

        var payment = new Payment
        {
            UserId = beneficiary.Id,
            Amount = 90_000m,
            PaymentType = PaymentTypes.WalletCharge,
            Gateway = PaymentGateways.Behpardakht,
            OrderId = "VWTEST1",
            Status = PaymentStatuses.Verified,
            MetaData = WalletReferralService.SerializeChargeMeta(meta),
            CreatedAt = DateTime.UtcNow
        };
        db.Context.Payments.Add(payment);
        await db.Context.SaveChangesAsync();

        await db.Service.FulfillWalletChargeWithReferralAsync(payment);
        await db.Context.Entry(beneficiary).ReloadAsync();
        await db.Context.Entry(referrer).ReloadAsync();

        Assert.Equal(100_000m, beneficiary.WalletBalance);
        Assert.Equal(5_000m, referrer.WalletBalance);

        var depositTxCount = await db.Context.WalletTransactions.CountAsync(t =>
            t.PaymentId == payment.Id &&
            t.UserId == beneficiary.Id &&
            t.TransactionType == WalletTransactionTypes.Deposit &&
            t.Status == TransactionStatuses.Completed);
        var bonusTxCount = await db.Context.WalletTransactions.CountAsync(t =>
            t.PaymentId == payment.Id &&
            t.UserId == referrer.Id &&
            t.TransactionType == WalletTransactionTypes.ReferralBonus &&
            t.Status == TransactionStatuses.Completed);
        Assert.Equal(1, depositTxCount);
        Assert.Equal(1, bonusTxCount);

        // اجرای دوباره همان پرداخت باید idempotent باشد.
        await db.Service.FulfillWalletChargeWithReferralAsync(payment);
        await db.Context.Entry(beneficiary).ReloadAsync();
        await db.Context.Entry(referrer).ReloadAsync();

        Assert.Equal(100_000m, beneficiary.WalletBalance);
        Assert.Equal(5_000m, referrer.WalletBalance);

        var rewardRows = await db.Context.WalletReferralRewards.CountAsync(r => r.PaymentId == payment.Id && !r.IsDeleted);
        Assert.Equal(1, rewardRows);
    }

    private sealed class WalletReferralTestDb : IAsyncDisposable
    {
        private WalletReferralTestDb(
            Api_Context context,
            WalletReferralService service)
        {
            Context = context;
            Service = service;
        }

        public Api_Context Context { get; }
        public WalletReferralService Service { get; }

        public static async Task<WalletReferralTestDb> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<Api_Context>()
                .UseInMemoryDatabase($"wallet-referral-{Guid.NewGuid():N}")
                .Options;

            var context = new Api_Context(options);
            await context.Database.EnsureCreatedAsync();

            var fakeWalletService = new FakeWalletService(context);
            var serviceProvider = new ServiceCollection()
                .AddSingleton<IWalletService>(fakeWalletService)
                .BuildServiceProvider();

            var service = new WalletReferralService(
                context,
                new UserRepository(context),
                serviceProvider,
                new MemoryCache(new MemoryCacheOptions()),
                new NoOpAuditService(),
                NullLogger<WalletReferralService>.Instance);

            return new WalletReferralTestDb(context, service);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
        }
    }

    private sealed class FakeWalletService : IWalletService
    {
        private readonly Api_Context _context;

        public FakeWalletService(Api_Context context)
        {
            _context = context;
        }

        public async Task<ApiResponse<WalletTransactionDto>> AddBalanceAsync(
            int userId,
            decimal amount,
            string transactionType,
            string title,
            string? description = null,
            int? paymentId = null,
            int? cashbackId = null,
            string? referenceNumber = null,
            bool sendPushNotification = true)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
            if (user == null)
                return ApiResponse<WalletTransactionDto>.NotFound("کاربر یافت نشد");

            var before = user.WalletBalance;
            var after = before + amount;
            user.WalletBalance = after;
            user.UpdatedAt = DateTime.UtcNow;

            var tx = new WalletTransaction
            {
                UserId = userId,
                TransactionType = transactionType,
                Amount = amount,
                BalanceBefore = before,
                BalanceAfter = after,
                Title = title,
                Description = description,
                PaymentId = paymentId,
                CashbackId = cashbackId,
                ReferenceNumber = referenceNumber,
                Status = TransactionStatuses.Completed,
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };

            await _context.WalletTransactions.AddAsync(tx);
            await _context.SaveChangesAsync();

            return ApiResponse<WalletTransactionDto>.CreateSuccess(new WalletTransactionDto { Id = tx.Id });
        }

        public Task<ApiResponse<WalletInfoDto>> GetWalletInfoAsync(int userId) => throw new NotImplementedException();
        public Task<ApiResponse<WalletTransactionListDto>> GetTransactionsAsync(int userId, int pageNumber = 1, int pageSize = 10) => throw new NotImplementedException();
        public Task<ApiResponse<List<WalletTransactionDto>>> GetRecentTransactionsAsync(int userId, int count = 5) => throw new NotImplementedException();
        public Task<ApiResponse<ChargeWalletResponseDto>> ChargeWalletAsync(int userId, ChargeWalletRequestDto request) => throw new NotImplementedException();
        public Task<ApiResponse<WalletTransactionDto>> DeductBalanceAsync(int userId, decimal amount, string title, string? description = null) => throw new NotImplementedException();
        public Task<bool> HasSufficientBalanceAsync(int userId, decimal amount) => throw new NotImplementedException();
        public Task<decimal> GetBalanceAsync(int userId) => throw new NotImplementedException();
        public Task<ApiResponse<WalletPageDto>> GetWalletPageAsync(int userId, int recentTransactionsCount = 10) => throw new NotImplementedException();
    }
}
