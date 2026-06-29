using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.LuckyWheel;
using Xunit;

namespace Api_Vapp.Tests.LuckyWheel;

public class LuckyWheelServiceTests : IAsyncLifetime
{
    private LuckyWheelTestContext _ctx = null!;

    public async Task InitializeAsync()
    {
        _ctx = await LuckyWheelTestContext.CreateAsync();
        await _ctx.BeginTestTransactionAsync();
    }

    public async Task DisposeAsync()
    {
        await _ctx.RollbackTestTransactionAsync();
        _ctx.Dispose();
    }

    [Fact]
    public async Task CreateDraft_ValidRequest_Returns201WithDraftStatus()
    {
        var result = await _ctx.Service.CreateDraftAsync(_ctx.OwnerUserId, _ctx.BuildCreateDto());

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("Draft", result.Data!.Status);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task CreateDraft_SaveToPhonebookWithoutNotebook_Returns400()
    {
        var result = await _ctx.Service.CreateDraftAsync(
            _ctx.OwnerUserId,
            _ctx.BuildCreateDto(d =>
            {
                d.SaveToPhonebook = true;
                d.NotebookIds = [];
            }));

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Update_ItemsProbabilityNot100_Returns400()
    {
        var wheelId = await _ctx.CreateDraftAsync();

        var result = await _ctx.Service.UpdateAsync(wheelId, _ctx.OwnerUserId, new UpdateLuckyWheelDto
        {
            Items = LuckyWheelTestContext.SampleItems(thirdProbability: 30m)
        });

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Update_ValidItems_Returns200()
    {
        var wheelId = await _ctx.CreateDraftAsync();

        var result = await _ctx.Service.UpdateAsync(wheelId, _ctx.OwnerUserId, new UpdateLuckyWheelDto
        {
            Items = LuckyWheelTestContext.SampleItems()
        });

        Assert.True(result.Success);
        Assert.Equal(3, result.Data!.Items.Count);
        Assert.Equal(100m, result.Data.Items.Sum(i => i.Probability));
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Publish_WithoutItems_Returns400()
    {
        var wheelId = await _ctx.CreateDraftAsync();

        var result = await _ctx.Service.PublishAsync(wheelId, _ctx.OwnerUserId);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Publish_ValidWheel_Returns200WithPublicUrl()
    {
        var wheelId = await _ctx.CreateWheelWithItemsAsync();
        var slug = $"wheel-{Guid.NewGuid():N}"[..14];

        var result = await _ctx.Service.PublishAsync(wheelId, _ctx.OwnerUserId, new PublishLuckyWheelDto
        {
            Slug = slug
        });

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("Published", result.Data!.Status);
        Assert.Equal($"https://app.com/wheel/{slug}", result.Data.PublicUrl);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Update_OtherUsersWheel_Returns403()
    {
        var wheelId = await _ctx.CreateDraftAsync();

        var result = await _ctx.Service.UpdateAsync(wheelId, _ctx.OtherUserId, new UpdateLuckyWheelDto
        {
            Title = "نباید مجاز باشد"
        });

        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
        AssertNoServerError(result);
    }

    private static void AssertNoServerError<T>(ApiResponse<T> result)
    {
        Assert.NotEqual(500, result.StatusCode);
        Assert.NotEqual(ErrorCodes.DatabaseError, result.ErrorCode);
    }
}
