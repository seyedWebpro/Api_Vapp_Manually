using Api_Vapp.Data;
using Api_Vapp.DTOs.Auth;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api_Vapp.Tests.Auth;

public class RefreshTokenRotationTests
{
    private static IConfiguration BuildConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "VappShop_SuperSecretKey_2024_MustBeAtLeast32CharactersLongForHMACSHA256",
                ["Jwt:Issuer"] = "VappShop",
                ["Jwt:Audience"] = "VappSiteUsers",
                ["Jwt:AccessTokenExpirationMinutes"] = "60",
                ["Jwt:RefreshTokenExpirationDays"] = "7",
                ["Jwt:RefreshTokenGraceSeconds"] = "30"
            })
            .Build();

    private static async Task<(string DbName, User User, string OriginalToken, IConfiguration Config)> SeedAsync()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var config = BuildConfig();
        await using var context = CreateContext(dbName);

        var user = new User
        {
            PhoneNumber = $"09{Guid.NewGuid().ToString("N")[..9]}",
            PasswordHash = "x",
            FullName = "Refresh Race Test",
            IsActive = true,
            IsPhoneVerified = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = CreateService(context, config);
        var original = await service.CreateRefreshTokenAsync(user.Id);
        return (dbName, user, original.Token, config);
    }

    private static Api_Context CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<Api_Context>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new Api_Context(options);
    }

    private static IRefreshTokenService CreateService(Api_Context context, IConfiguration config) =>
        new RefreshTokenService(
            context,
            config,
            new JwtService(config),
            NullLogger<RefreshTokenService>.Instance);

    [Fact]
    public async Task RotateOrReuse_SingleCall_CreatesReplacementAndRevokesOld()
    {
        var (dbName, _, originalToken, config) = await SeedAsync();
        await using var context = CreateContext(dbName);
        var service = CreateService(context, config);

        var result = await service.RotateOrReuseAsync(originalToken);

        Assert.Equal(RefreshTokenRotationStatus.Rotated, result.Status);
        Assert.NotNull(result.RefreshToken);
        Assert.NotEqual(originalToken, result.RefreshToken!.Token);

        var old = await context.RefreshTokens.AsNoTracking()
            .FirstAsync(rt => rt.Token == originalToken);
        Assert.True(old.IsRevoked);
        Assert.Equal(result.RefreshToken.Token, old.ReplacementToken);
        Assert.Equal(result.RefreshToken.Id, old.ReplacedByTokenId);
    }

    [Fact]
    public async Task RotateOrReuse_SecondCallWithinGrace_ReturnsSameReplacement_NotInvalid()
    {
        var (dbName, _, originalToken, config) = await SeedAsync();
        await using var context = CreateContext(dbName);
        var service = CreateService(context, config);

        var first = await service.RotateOrReuseAsync(originalToken);
        var second = await service.RotateOrReuseAsync(originalToken);

        Assert.Equal(RefreshTokenRotationStatus.Rotated, first.Status);
        Assert.Equal(RefreshTokenRotationStatus.GraceReuse, second.Status);
        Assert.Equal(first.RefreshToken!.Token, second.RefreshToken!.Token);
    }

    [Fact]
    public async Task RotateOrReuse_ConcurrentCalls_NeverReturnInvalid_AndShareOneActiveToken()
    {
        var (dbName, user, originalToken, config) = await SeedAsync();
        const int parallel = 8;

        var tasks = Enumerable.Range(0, parallel).Select(async _ =>
        {
            await using var ctx = CreateContext(dbName);
            var svc = CreateService(ctx, config);
            return await svc.RotateOrReuseAsync(originalToken);
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r =>
            Assert.True(
                r.Status is RefreshTokenRotationStatus.Rotated or RefreshTokenRotationStatus.GraceReuse,
                $"Unexpected status: {r.Status}"));
        Assert.DoesNotContain(results, r => r.Status == RefreshTokenRotationStatus.Invalid);

        var replacementTokens = results
            .Select(r => r.RefreshToken!.Token)
            .Distinct()
            .ToList();
        Assert.Single(replacementTokens);

        await using var verifyCtx = CreateContext(dbName);
        var activeCount = await verifyCtx.RefreshTokens.CountAsync(rt =>
            rt.UserId == user.Id && !rt.IsRevoked && rt.Token == replacementTokens[0]);
        Assert.Equal(1, activeCount);

        await using var followCtx = CreateContext(dbName);
        var followUp = await CreateService(followCtx, config).RotateOrReuseAsync(replacementTokens[0]);
        Assert.Equal(RefreshTokenRotationStatus.Rotated, followUp.Status);
        Assert.NotEqual(replacementTokens[0], followUp.RefreshToken!.Token);
    }

    [Fact]
    public async Task RotateOrReuse_UnknownToken_IsInvalid()
    {
        var (dbName, _, _, config) = await SeedAsync();
        await using var context = CreateContext(dbName);
        var result = await CreateService(context, config)
            .RotateOrReuseAsync("not-a-real-refresh-token");
        Assert.Equal(RefreshTokenRotationStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task RotateOrReuse_InactiveUser_IsInactive()
    {
        var (dbName, user, originalToken, config) = await SeedAsync();
        await using var context = CreateContext(dbName);
        var tracked = await context.Users.FirstAsync(u => u.Id == user.Id);
        tracked.IsActive = false;
        await context.SaveChangesAsync();

        var result = await CreateService(context, config).RotateOrReuseAsync(originalToken);
        Assert.Equal(RefreshTokenRotationStatus.InactiveUser, result.Status);
    }
}
