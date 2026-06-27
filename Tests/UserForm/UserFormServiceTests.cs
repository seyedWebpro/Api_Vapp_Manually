using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.UserForm;
using Api_Vapp.Utilities;
using Xunit;

namespace Api_Vapp.Tests.UserForm;

public class UserFormServiceTests : IAsyncLifetime
{
    private UserFormTestContext _ctx = null!;

    public async Task InitializeAsync()
    {
        _ctx = await UserFormTestContext.CreateAsync();
        await _ctx.BeginTestTransactionAsync();
    }

    public async Task DisposeAsync()
    {
        await _ctx.RollbackTestTransactionAsync();
        _ctx.Dispose();
    }

    [Fact]
    public async Task CreateDraft_ValidRequest_Returns201WithData()
    {
        var result = await _ctx.Service.CreateDraftAsync(_ctx.OwnerUserId, _ctx.BuildCreateDto());

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("Draft", result.Data!.Status);
        Assert.Equal(2, result.Data.Fields.Count);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task CreateDraft_DuplicateFieldKey_Returns400()
    {
        var dto = _ctx.BuildCreateDto(d =>
        {
            d.Fields =
            [
                new UserFormFieldDto
                {
                    FieldKey = "mobile",
                    FieldType = "mobile",
                    Label = "موبایل ۱",
                    DisplayOrder = 1
                },
                new UserFormFieldDto
                {
                    FieldKey = "mobile",
                    FieldType = "text",
                    Label = "موبایل ۲",
                    DisplayOrder = 2
                }
            ];
        });

        var result = await _ctx.Service.CreateDraftAsync(_ctx.OwnerUserId, dto);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task CreateDraft_InvalidSlug_Returns400()
    {
        var result = await _ctx.Service.CreateDraftAsync(
            _ctx.OwnerUserId,
            _ctx.BuildCreateDto(d => d.Slug = "slug with spaces"));

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task CreateDraft_InvalidNotebook_Returns400()
    {
        var result = await _ctx.Service.CreateDraftAsync(
            _ctx.OwnerUserId,
            _ctx.BuildCreateDto(d => d.NotebookIds = [99999999]));

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task CreateDraft_SaveToPhonebookWithoutMobile_Returns400()
    {
        var result = await _ctx.Service.CreateDraftAsync(
            _ctx.OwnerUserId,
            _ctx.BuildCreateDto(d =>
            {
                d.SaveToPhonebook = true;
                d.NotebookIds = [_ctx.NotebookId];
                d.Fields = UserFormTestContext.SampleFields(includeMobile: false);
            }));

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Update_OnlyTitle_KeepsExistingFields()
    {
        var formId = await _ctx.CreateDraftAsync();

        var result = await _ctx.Service.UpdateAsync(formId, _ctx.OwnerUserId, new UpdateUserFormDto
        {
            Title = "فقط عنوان عوض شد"
        });

        Assert.True(result.Success);
        Assert.Equal("فقط عنوان عوض شد", result.Data!.Title);
        Assert.Equal(2, result.Data.Fields.Count);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Update_PartialFields_MergesByFieldKey()
    {
        var formId = await _ctx.CreateDraftAsync();

        var result = await _ctx.Service.UpdateAsync(formId, _ctx.OwnerUserId, new UpdateUserFormDto
        {
            Fields =
            [
                new UserFormFieldDto
                {
                    FieldKey = "mobile",
                    FieldType = "mobile",
                    Label = "موبایل ویرایش‌شده",
                    Placeholder = "0912...",
                    DisplayOrder = 2,
                    IsActive = true
                }
            ]
        });

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Fields.Count);
        Assert.Contains(result.Data.Fields, f => f.FieldKey == "mobile" && f.Label == "موبایل ویرایش‌شده");
        Assert.Contains(result.Data.Fields, f => f.FieldKey == "full_name");
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Update_EmptyPayload_Returns400()
    {
        var formId = await _ctx.CreateDraftAsync();

        var result = await _ctx.Service.UpdateAsync(formId, _ctx.OwnerUserId, new UpdateUserFormDto());

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Update_ValidRequest_Returns200()
    {
        var formId = await _ctx.CreateDraftAsync();

        var result = await _ctx.Service.UpdateAsync(formId, _ctx.OwnerUserId, new UpdateUserFormDto
        {
            Title = "عنوان جدید",
            Fields = UserFormTestContext.SampleFields()
        });

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("عنوان جدید", result.Data!.Title);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Update_NotFound_Returns404()
    {
        var result = await _ctx.Service.UpdateAsync(99999999, _ctx.OwnerUserId, new UpdateUserFormDto
        {
            Title = "test"
        });

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Update_OtherUsersForm_Returns403()
    {
        var formId = await _ctx.CreateDraftAsync();

        var result = await _ctx.Service.UpdateAsync(formId, _ctx.OtherUserId, new UpdateUserFormDto
        {
            Title = "نباید مجاز باشد"
        });

        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Publish_ValidDraft_Returns200WithPublicUrl()
    {
        var formId = await _ctx.CreateDraftAsync();
        var slug = $"job-{Guid.NewGuid():N}"[..12];

        var result = await _ctx.Service.PublishAsync(formId, _ctx.OwnerUserId, new PublishUserFormDto
        {
            Slug = slug
        });

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("Published", result.Data!.Status);
        Assert.Equal($"https://app.com/form/{slug}", result.Data.PublicUrl);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Publish_WithoutTitle_Returns400()
    {
        var formId = await _ctx.CreateDraftAsync(d => d.Title = "   ");

        var result = await _ctx.Service.PublishAsync(formId, _ctx.OwnerUserId, null);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Publish_WithoutActiveFields_Returns400()
    {
        var formId = await _ctx.CreateDraftAsync(d =>
        {
            d.Fields = UserFormTestContext.SampleFields().Select(f =>
            {
                f.IsActive = false;
                return f;
            }).ToList();
        });

        var result = await _ctx.Service.PublishAsync(formId, _ctx.OwnerUserId, null);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Publish_DuplicateSlug_Returns400()
    {
        var slug = $"dup-{Guid.NewGuid():N}"[..10];
        await _ctx.CreatePublishedFormAsync(slug);
        var secondFormId = await _ctx.CreateDraftAsync();

        var result = await _ctx.Service.PublishAsync(secondFormId, _ctx.OwnerUserId, new PublishUserFormDto
        {
            Slug = slug
        });

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task GetById_OwnForm_Returns200()
    {
        var formId = await _ctx.CreateDraftAsync();

        var result = await _ctx.Service.GetByIdAsync(formId, _ctx.OwnerUserId);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        var result = await _ctx.Service.GetByIdAsync(99999999, _ctx.OwnerUserId);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task GetForms_ValidPaging_Returns200()
    {
        await _ctx.CreateDraftAsync();

        var result = await _ctx.Service.GetFormsAsync(_ctx.OwnerUserId, 1, 10);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.NotEmpty(result.Data!.Forms.Items);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task GetForms_InvalidPageSize_Returns400()
    {
        var result = await _ctx.Service.GetFormsAsync(_ctx.OwnerUserId, 1, 200);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(ErrorCodes.InvalidInput, result.ErrorCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Delete_OwnForm_Returns200()
    {
        var formId = await _ctx.CreateDraftAsync();

        var result = await _ctx.Service.DeleteAsync(formId, _ctx.OwnerUserId);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task ToggleStatus_OnDraft_Returns400()
    {
        var formId = await _ctx.CreateDraftAsync();

        var result = await _ctx.Service.ToggleStatusAsync(formId, _ctx.OwnerUserId);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task ToggleStatus_OnPublishedForm_Returns200()
    {
        var slug = $"toggle-{Guid.NewGuid():N}"[..12];
        var formId = await _ctx.CreatePublishedFormAsync(slug);

        var result = await _ctx.Service.ToggleStatusAsync(formId, _ctx.OwnerUserId);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.False(result.Data!.IsActive);
        AssertNoServerError(result);
    }

    private static void AssertNoServerError<T>(ApiResponse<T> response)
    {
        Assert.NotEqual(500, response.StatusCode);
        Assert.True(
            ControlledErrorHelper.IsSafeUserMessage(response.Message),
            $"Unsafe error message returned: {response.Message}");
    }
}
