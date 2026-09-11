using Api_Vapp.Data;
using Api_Vapp.Models;
using Api_Vapp.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api_Vapp.Tests.Cashback;

public class CashbackRepositoryTests : IAsyncLifetime
{
    private Api_Context _context = null!;
    private CashbackRepository _repository = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<Api_Context>()
            .UseInMemoryDatabase($"cashback-list-{Guid.NewGuid():N}")
            .Options;

        _context = new Api_Context(options);
        _repository = new CashbackRepository(_context);
        await _context.Database.EnsureCreatedAsync();

        _context.Cashbacks.AddRange(
            CreateCashback(userId: 10, "فعال جدید", isActive: true, createdAt: new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc)),
            CreateCashback(userId: 10, "غیرفعال", isActive: false, createdAt: new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc)),
            CreateCashback(userId: 10, "فعال قدیمی", isActive: true, createdAt: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)),
            CreateCashback(userId: 10, "حذف شده", isActive: true, isDeleted: true),
            CreateCashback(userId: 20, "کاربر دیگر", isActive: true));
        await _context.SaveChangesAsync();
    }

    [Theory]
    [InlineData(null, 3)]
    [InlineData(true, 2)]
    [InlineData(false, 1)]
    public async Task GetByUserIdAsync_FiltersByStatusAndExcludesDeletedAndOtherUsers(bool? isActive, int expectedCount)
    {
        var result = await _repository.GetByUserIdAsync(10, 1, 20, isActive);

        Assert.Equal(expectedCount, result.Count());
        Assert.All(result, cashback => Assert.Equal(10, cashback.UserId));
        Assert.All(result, cashback => Assert.False(cashback.IsDeleted));
        if (isActive.HasValue)
        {
            Assert.All(result, cashback => Assert.Equal(isActive.Value, cashback.IsActive));
        }
    }

    [Theory]
    [InlineData(null, 3)]
    [InlineData(true, 2)]
    [InlineData(false, 1)]
    public async Task GetCountByUserIdAsync_UsesTheSameStatusFilterAsTheList(bool? isActive, int expectedCount)
    {
        var count = await _repository.GetCountByUserIdAsync(10, isActive);

        Assert.Equal(expectedCount, count);
    }

    [Fact]
    public async Task GetByUserIdAsync_AppliesNewestFirstPagination()
    {
        var firstPage = await _repository.GetByUserIdAsync(10, 1, 2);
        var secondPage = await _repository.GetByUserIdAsync(10, 2, 2);

        Assert.Equal(new[] { "فعال جدید", "غیرفعال" }, firstPage.Select(item => item.Title));
        Assert.Equal(new[] { "فعال قدیمی" }, secondPage.Select(item => item.Title));
    }

    private static Models.Cashback CreateCashback(
        int userId,
        string title,
        bool isActive,
        DateTime? createdAt = null,
        bool isDeleted = false) => new()
    {
        UserId = userId,
        Title = title,
        IsActive = isActive,
        IsDeleted = isDeleted,
        CreatedAt = createdAt ?? DateTime.UtcNow,
        StartDate = DateTime.UtcNow,
        CashbackType = CashbackTypes.Percentage,
        Percentage = 10
    };

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
    }
}
