using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services;
using Api_Vapp.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api_Vapp.Tests.Wallet;

/// <summary>
/// رگرسیون باگ production: DeductBalance داخل تراکنش بازِ فراخوان‌کننده
/// نباید InvalidOperationException بدهد.
/// </summary>
public class WalletDeductNestedTransactionTests
{
    private static readonly SemaphoreSlim MigrationLock = new(1, 1);
    private static bool _migrationsApplied;

    [Fact]
    public async Task DeductBalance_InsideAmbientTransaction_SucceedsWithoutNestedBegin()
    {
        await using var context = await CreateContextAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = new User
        {
            PhoneNumber = $"09{suffix}01",
            PasswordHash = "x",
            FullName = "wallet-nested-tx",
            WalletBalance = 10_000m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var wallet = CreateWalletService(context);

        await using var ambient = await context.Database.BeginTransactionAsync();
        var result = await wallet.DeductBalanceAsync(
            user.Id,
            160m,
            "تست کسر داخل تراکنش محیطی",
            description: "regression nested transaction",
            sendPushNotification: false);

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(-160m, result.Data!.Amount);

        await ambient.CommitAsync();

        await context.Entry(user).ReloadAsync();
        Assert.Equal(9_840m, user.WalletBalance);
    }

    private static WalletService CreateWalletService(Api_Context context)
    {
        return new WalletService(
            context,
            walletRepository: null!,
            userRepository: null!,
            cashbackRepository: null!,
            walletReferralService: null!,
            serviceProvider: null!,
            configuration: new ConfigurationBuilder().Build(),
            audit: new NoOpAuditService(),
            pushNotifier: new NoOpUserPushNotifier(),
            logger: NullLogger<WalletService>.Instance);
    }

    private static async Task<Api_Context> CreateContextAsync()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("VAPP_TEST_CONNECTION")
            ?? "Server=localhost,1436;Database=DbVapp_UserFormTests;User Id=sa;Password=Vapp@Secure2025!;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=true";

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

        return context;
    }
}
