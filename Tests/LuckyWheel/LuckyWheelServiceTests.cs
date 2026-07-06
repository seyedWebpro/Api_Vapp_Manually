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

    [Fact]
    public async Task Update_OnlyTitle_KeepsItems()
    {
        var wheelId = await _ctx.CreateWheelWithItemsAsync();

        var result = await _ctx.Service.UpdateAsync(wheelId, _ctx.OwnerUserId, new UpdateLuckyWheelDto
        {
            Title = "فقط عنوان عوض شد"
        });

        Assert.True(result.Success);
        Assert.Equal("فقط عنوان عوض شد", result.Data!.Title);
        Assert.Equal(3, result.Data.Items.Count);
        Assert.Equal(100m, result.Data.Items.Sum(i => i.Probability));
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Update_EmptyTitle_Returns400()
    {
        var wheelId = await _ctx.CreateDraftAsync();

        var result = await _ctx.Service.UpdateAsync(wheelId, _ctx.OwnerUserId, new UpdateLuckyWheelDto
        {
            Title = "   "
        });

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Update_MainInfo_Returns200()
    {
        var wheelId = await _ctx.CreateDraftAsync();

        var result = await _ctx.Service.UpdateAsync(wheelId, _ctx.OwnerUserId, new UpdateLuckyWheelDto
        {
            Title = "گردونه جشن تابستانه",
            Description = "توضیح جدید",
            Slug = "summer-festival",
            SaveToPhonebook = true,
            NotebookIds = [_ctx.NotebookId]
        });

        Assert.True(result.Success);
        Assert.Equal("گردونه جشن تابستانه", result.Data!.Title);
        Assert.Equal("summer-festival", result.Data.Slug);
        Assert.True(result.Data.SaveToPhonebook);
        Assert.Contains(_ctx.NotebookId, result.Data.NotebookIds);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task SetActiveStatus_PublishedWheel_Returns200()
    {
        var wheelId = await _ctx.CreateWheelWithItemsAsync();
        await _ctx.Service.PublishAsync(wheelId, _ctx.OwnerUserId, new PublishLuckyWheelDto
        {
            Slug = $"active-{Guid.NewGuid():N}"[..12]
        });

        var result = await _ctx.Service.SetActiveStatusAsync(wheelId, _ctx.OwnerUserId, false);

        Assert.True(result.Success);
        Assert.False(result.Data!.IsActive);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Delete_ValidWheel_Returns200AndClearsSlug()
    {
        var wheelId = await _ctx.CreateWheelWithItemsAsync();
        var slug = $"del-{Guid.NewGuid():N}"[..10];
        await _ctx.Service.PublishAsync(wheelId, _ctx.OwnerUserId, new PublishLuckyWheelDto { Slug = slug });

        var delete = await _ctx.Service.DeleteAsync(wheelId, _ctx.OwnerUserId);
        Assert.True(delete.Success);

        var get = await _ctx.Service.GetByIdAsync(wheelId, _ctx.OwnerUserId);
        Assert.False(get.Success);
        Assert.Equal(404, get.StatusCode);
        AssertNoServerError(delete);
    }

    private static void AssertNoServerError<T>(ApiResponse<T> result)
    {
        Assert.NotEqual(500, result.StatusCode);
        Assert.NotEqual(ErrorCodes.DatabaseError, result.ErrorCode);
    }
}
