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
    public async Task AddItems_ProbabilityNot100InDraft_Returns200AndNotReady()
    {
        var wheelId = await _ctx.CreateDraftAsync();

        var result = await _ctx.Service.AddItemsAsync(wheelId, _ctx.OwnerUserId, new AddLuckyWheelItemsDto
        {
            Items = LuckyWheelTestContext.SampleItems(thirdProbability: 30m)
        });

        Assert.True(result.Success);
        Assert.False(result.Data!.IsReadyToPublish);
        Assert.Contains(result.Data.PublishValidationErrors, e => e.Contains("مجموع درصد"));
        AssertNoServerError(result);
    }

    [Fact]
    public async Task AddItems_ValidItems_Returns200()
    {
        var wheelId = await _ctx.CreateDraftAsync();

        var result = await _ctx.Service.AddItemsAsync(wheelId, _ctx.OwnerUserId, new AddLuckyWheelItemsDto
        {
            Items = LuckyWheelTestContext.SampleItems()
        });

        Assert.True(result.Success);
        Assert.Equal(3, result.Data!.Items.Count);
        Assert.Equal(100m, result.Data.Items.Sum(i => i.Probability));
        AssertNoServerError(result);
    }

    [Fact]
    public async Task UpdateItems_ValidPartialUpdate_Returns200AndMerges()
    {
        var wheelId = await _ctx.CreateWheelWithItemsAsync();
        var firstItemId = _ctx.Context.LuckyWheelItems
            .Where(i => i.LuckyWheelId == wheelId)
            .OrderBy(i => i.DisplayOrder)
            .Select(i => i.Id)
            .First();

        var result = await _ctx.Service.UpdateItemsAsync(wheelId, _ctx.OwnerUserId, new UpdateLuckyWheelItemsDto
        {
            Items =
            [
                new UpdateLuckyWheelItemDto { Id = firstItemId, Name = "درصد جدید", Probability = 50, DisplayOrder = 1 }
            ]
        });

        Assert.True(result.Success);
        Assert.Equal(3, result.Data!.Items.Count);
        Assert.Equal("درصد جدید", result.Data.Items.First(i => i.Id == firstItemId).Name);
        Assert.Equal(50m, result.Data.Items.First(i => i.Id == firstItemId).Probability);
        Assert.Equal(120m, result.Data.Items.Sum(i => i.Probability));
        AssertNoServerError(result);
    }

    [Fact]
    public async Task UpdateItems_AddNewItem_Returns200()
    {
        var wheelId = await _ctx.CreateWheelWithItemsAsync();

        var result = await _ctx.Service.UpdateItemsAsync(wheelId, _ctx.OwnerUserId, new UpdateLuckyWheelItemsDto
        {
            Items =
            [
                new UpdateLuckyWheelItemDto { Name = "جایزه جدید", Probability = 20, DisplayOrder = 4 }
            ]
        });

        Assert.True(result.Success);
        Assert.Equal(4, result.Data!.Items.Count);
        Assert.Contains(result.Data.Items, i => i.Name == "جایزه جدید" && i.Probability == 20m);
        Assert.Equal(100m + 20m, result.Data.Items.Sum(i => i.Probability));
        AssertNoServerError(result);
    }

    [Fact]
    public async Task UpdateItems_InvalidProbability_Returns400()
    {
        var wheelId = await _ctx.CreateDraftAsync();

        var result = await _ctx.Service.UpdateItemsAsync(wheelId, _ctx.OwnerUserId, new UpdateLuckyWheelItemsDto
        {
            Items =
            [
                new UpdateLuckyWheelItemDto { Name = "غیرمعتبر", Probability = 150, DisplayOrder = 1 }
            ]
        });

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task UpdateItems_OtherUsersWheel_Returns403()
    {
        var wheelId = await _ctx.CreateWheelWithItemsAsync();

        var result = await _ctx.Service.UpdateItemsAsync(wheelId, _ctx.OtherUserId, new UpdateLuckyWheelItemsDto
        {
            Items =
            [
                new UpdateLuckyWheelItemDto { Name = "غیرمجاز", Probability = 50, DisplayOrder = 1 }
            ]
        });

        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task UpdateItems_PublishedWheelRequiresSum100_Returns400()
    {
        var wheelId = await _ctx.CreateWheelWithItemsAsync();
        await _ctx.Service.PublishAsync(wheelId, _ctx.OwnerUserId, new PublishLuckyWheelDto
        {
            Slug = $"pub-{Guid.NewGuid():N}"[..12]
        });

        var firstItemId = _ctx.Context.LuckyWheelItems
            .Where(i => i.LuckyWheelId == wheelId)
            .OrderBy(i => i.DisplayOrder)
            .Select(i => i.Id)
            .First();

        var result = await _ctx.Service.UpdateItemsAsync(wheelId, _ctx.OwnerUserId, new UpdateLuckyWheelItemsDto
        {
            Items =
            [
                new UpdateLuckyWheelItemDto { Id = firstItemId, Name = "A", Probability = 60, DisplayOrder = 1 }
            ]
        });

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains(result.Errors ?? new List<string>(), e => e.Contains("مجموع درصد"));
        AssertNoServerError(result);
    }

    [Fact]
    public async Task UpdateItems_EmptyList_KeepsItems()
    {
        var wheelId = await _ctx.CreateWheelWithItemsAsync();

        var result = await _ctx.Service.UpdateItemsAsync(wheelId, _ctx.OwnerUserId, new UpdateLuckyWheelItemsDto
        {
            Items = []
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
    public async Task CreateDraft_WithoutItems_ReturnsNotReadyToPublish()
    {
        var result = await _ctx.Service.CreateDraftAsync(_ctx.OwnerUserId, _ctx.BuildCreateDto());

        Assert.True(result.Success);
        Assert.False(result.Data!.IsReadyToPublish);
        Assert.NotEmpty(result.Data.PublishValidationErrors);
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

    [Fact]
    public async Task GetParticipants_WithSeededRows_ReturnsStatsAndPagedList()
    {
        var wheelId = await _ctx.CreateWheelWithItemsAsync();
        await _ctx.Service.PublishAsync(wheelId, _ctx.OwnerUserId, new PublishLuckyWheelDto
        {
            Slug = $"part-{Guid.NewGuid():N}"[..12]
        });

        await _ctx.SeedParticipantAsync(wheelId, "علی رضایی", "09121110001", "LW-AAAA01");
        await _ctx.SeedParticipantAsync(wheelId, "سارا محمدی", "09121110002", "LW-BBBB02");

        var result = await _ctx.Service.GetParticipantsAsync(wheelId, _ctx.OwnerUserId, pageNumber: 1, pageSize: 10);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(2, result.Data!.ParticipantCount);
        Assert.Equal(2, result.Data.PrizeAwardedCount);
        Assert.Equal(2, result.Data.Participants.TotalCount);
        Assert.Equal(2, result.Data.Participants.Items.Count);
        Assert.Contains(result.Data.Participants.Items, p => p.ParticipantMobile == "09121110001");
        Assert.Contains(result.Data.Participants.Items, p => p.PrizeCode == "LW-BBBB02");
        AssertNoServerError(result);
    }

    [Fact]
    public async Task GetParticipants_SearchByPrizeCode_FiltersRows()
    {
        var wheelId = await _ctx.CreateWheelWithItemsAsync();
        await _ctx.Service.PublishAsync(wheelId, _ctx.OwnerUserId, new PublishLuckyWheelDto
        {
            Slug = $"srch-{Guid.NewGuid():N}"[..12]
        });

        await _ctx.SeedParticipantAsync(wheelId, "علی رضایی", "09121110003", "LW-FIND01");
        await _ctx.SeedParticipantAsync(wheelId, "سارا محمدی", "09121110004", "LW-OTHER2");

        var result = await _ctx.Service.GetParticipantsAsync(
            wheelId,
            _ctx.OwnerUserId,
            searchTerm: "FIND01");

        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.Participants.TotalCount);
        Assert.Equal("LW-FIND01", result.Data.Participants.Items[0].PrizeCode);
        Assert.Equal(2, result.Data.ParticipantCount);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task GetParticipants_OtherUser_Returns404()
    {
        var wheelId = await _ctx.CreateWheelWithItemsAsync();

        var result = await _ctx.Service.GetParticipantsAsync(wheelId, _ctx.OtherUserId);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task VerifyParticipant_ByMobileAndPrizeCode_ReturnsDetails()
    {
        var wheelId = await _ctx.CreateWheelWithItemsAsync();
        await _ctx.Service.PublishAsync(wheelId, _ctx.OwnerUserId, new PublishLuckyWheelDto
        {
            Slug = $"vrfy-{Guid.NewGuid():N}"[..12]
        });

        await _ctx.SeedParticipantAsync(wheelId, "رضا کریمی", "09123334455", "LW-VRIFY1");

        var byMobile = await _ctx.Service.VerifyParticipantAsync(wheelId, _ctx.OwnerUserId, "09123334455");
        Assert.True(byMobile.Success);
        Assert.Equal("رضا کریمی", byMobile.Data!.ParticipantFullName);
        Assert.Equal("LW-VRIFY1", byMobile.Data.PrizeCode);
        Assert.False(string.IsNullOrWhiteSpace(byMobile.Data.WonItemName));

        var byCode = await _ctx.Service.VerifyParticipantAsync(wheelId, _ctx.OwnerUserId, "lw-vrify1");
        Assert.True(byCode.Success);
        Assert.Equal("09123334455", byCode.Data!.ParticipantMobile);

        var missing = await _ctx.Service.VerifyParticipantAsync(wheelId, _ctx.OwnerUserId, "09129999999");
        Assert.False(missing.Success);
        Assert.Equal(404, missing.StatusCode);
        AssertNoServerError(byMobile);
    }

    private static void AssertNoServerError<T>(ApiResponse<T> result)
    {
        Assert.NotEqual(500, result.StatusCode);
        Assert.NotEqual(ErrorCodes.DatabaseError, result.ErrorCode);
    }
}
