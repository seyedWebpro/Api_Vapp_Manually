using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api_Vapp.Tests.Subscription;

internal sealed class SubscriptionTestContext : IDisposable
{
    private static readonly SemaphoreSlim MigrationLock = new(1, 1);
    private static bool _migrationsApplied;

    private readonly Api_Context _context;
    private IDbContextTransaction? _transaction;

    private SubscriptionTestContext(Api_Context context)
    {
        _context = context;
    }

    public IUserSubscriptionService CatalogService { get; private set; } = null!;
    public ISubscriptionPurchaseService PurchaseService { get; private set; } = null!;
    public ISubscriptionEntitlementService EntitlementService { get; private set; } = null!;
    public ISubscriptionActivationService ActivationService { get; private set; } = null!;
    public ISubscriptionDiscountService DiscountService { get; private set; } = null!;

    public int UserId { get; private set; }
    public int PlusPlanId { get; private set; }
    public int FreePlanId { get; private set; }
    public int GoldPlanId { get; private set; }

    public static async Task<SubscriptionTestContext> CreateAsync()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("VAPP_TEST_CONNECTION")
            ?? "Server=localhost,1436;Database=DbVapp_SubscriptionTests;User Id=sa;Password=Vapp@Secure2025!;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=true";

        var options = new DbContextOptionsBuilder<Api_Context>()
            .UseSqlServer(connectionString)
            .Options;

        var context = new Api_Context(options);

        await MigrationLock.WaitAsync();
        try
        {
            if (!_migrationsApplied)
            {
                await context.Database.MigrateAsync();
                _migrationsApplied = true;
            }
        }
        finally
        {
            MigrationLock.Release();
        }

        var testContext = new SubscriptionTestContext(context);
        await testContext.InitializeAsync();
        return testContext;
    }

    public async Task BeginTestTransactionAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task RollbackTestTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task<string> CreateDiscountCodeAsync(
        decimal value,
        string discountType = SubscriptionDiscountTypes.Fixed,
        int? planId = null,
        string? code = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var dto = new CreateSubscriptionDiscountCodeDto
        {
            Code = code ?? $"SUB{suffix}",
            Title = "تخفیف تست",
            DiscountType = discountType,
            Value = value,
            SubscriptionPlanId = planId,
            MaxUsesPerUser = 5,
            IsActive = true
        };

        var result = await DiscountService.CreateAsync(dto);
        if (!result.Success || result.Data == null)
            throw new InvalidOperationException($"Discount seed failed: {result.Message}");

        return result.Data.Code;
    }

    public async Task<decimal> GetPlanPriceAsync(int planId) =>
        await _context.SubscriptionPlans
            .Where(p => p.Id == planId)
            .Select(p => p.Price)
            .FirstAsync();

    public async Task<SubscriptionPlan> GetPlanAsync(int planId) =>
        await _context.SubscriptionPlans.FirstAsync(p => p.Id == planId);

    public async Task<Payment> GetPaymentAsync(int paymentId) =>
        await _context.Payments.FirstAsync(p => p.Id == paymentId);

    public async Task SavePaymentAsync(Payment payment)
    {
        _context.Payments.Update(payment);
        await _context.SaveChangesAsync();
    }

    public async Task<int> CountSubscriptionsForPaymentAsync(int paymentId) =>
        await _context.UserSubscriptions.CountAsync(us =>
            us.SourcePaymentId == paymentId && !us.IsDeleted);

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }

    private async Task InitializeAsync()
    {
        EntitlementService = new SubscriptionEntitlementService(_context);
        DiscountService = new SubscriptionDiscountService(_context);
        ActivationService = new SubscriptionActivationService(_context, NullLogger<SubscriptionActivationService>.Instance);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Payment:ApiBaseUrl"] = "http://localhost:5054",
                ["Payment:Behpardakht:FrontendCallbackUrl"] = "/payment/result"
            })
            .Build();

        var paymentService = new FakePaymentService(_context);
        PurchaseService = new SubscriptionPurchaseService(
            _context,
            DiscountService,
            EntitlementService,
            ActivationService,
            paymentService,
            config,
            NullLogger<SubscriptionPurchaseService>.Instance);

        CatalogService = new UserSubscriptionService(
            _context,
            EntitlementService,
            NullLogger<UserSubscriptionService>.Instance);

        await SeedAsync();
    }

    private async Task SeedAsync()
    {
        await DatabaseSeeder.SeedAsync(_context, NullLoggerFactory.Instance.CreateLogger("SubscriptionTests"));

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = new User
        {
            PhoneNumber = $"0917{suffix[..7]}",
            PasswordHash = "hash-subscriber",
            FullName = "کاربر اشتراک",
            IsActive = true,
            IsPhoneVerified = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        UserId = user.Id;

        FreePlanId = await _context.SubscriptionPlans
            .Where(p => p.TierCode == SubscriptionPlanTierCodes.Free && !p.IsDeleted)
            .Select(p => p.Id)
            .FirstAsync();

        PlusPlanId = await _context.SubscriptionPlans
            .Where(p => p.TierCode == SubscriptionPlanTierCodes.Plus && !p.IsDeleted)
            .Select(p => p.Id)
            .FirstAsync();

        GoldPlanId = await _context.SubscriptionPlans
            .Where(p => p.TierCode == SubscriptionPlanTierCodes.Gold && !p.IsDeleted)
            .Select(p => p.Id)
            .FirstAsync();
    }
}
