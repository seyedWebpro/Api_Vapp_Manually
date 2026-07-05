using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.UserForm;
using Api_Vapp.Utilities;
using Xunit;

namespace Api_Vapp.Tests.UserForm;

/// <summary>
/// پوشش endpointهای update-info و update-fields — بدون 500 و با پیام کنترل‌شده.
/// </summary>
public class UserFormEndpointTests : IAsyncLifetime
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
    public async Task Endpoint_UpdateInfo_HappyPath_Returns200()
    {
        var formId = await _ctx.CreateDraftAsync();

        var result = await _ctx.Service.UpdateInfoAsync(formId, _ctx.OwnerUserId, new UpdateUserFormInfoDto
        {
            Title = "عنوان endpoint",
            Description = "توضیح endpoint"
        });

        AssertSuccess(result, 200);
        Assert.Equal("عنوان endpoint", result.Data!.Title);
    }

    [Fact]
    public async Task Endpoint_UpdateInfo_EmptyBody_Returns400()
    {
        var formId = await _ctx.CreateDraftAsync();

        var result = await _ctx.Service.UpdateInfoAsync(formId, _ctx.OwnerUserId, new UpdateUserFormInfoDto());

        AssertFailure(result, 400);
    }

    [Fact]
    public async Task Endpoint_UpdateInfo_NotFound_Returns404()
    {
        var result = await _ctx.Service.UpdateInfoAsync(99999999, _ctx.OwnerUserId, new UpdateUserFormInfoDto
        {
            Title = "test"
        });

        AssertFailure(result, 404);
    }

    [Fact]
    public async Task Endpoint_UpdateInfo_OtherUser_Returns403()
    {
        var formId = await _ctx.CreateDraftAsync();

        var result = await _ctx.Service.UpdateInfoAsync(formId, _ctx.OtherUserId, new UpdateUserFormInfoDto
        {
            Title = "نباید مجاز باشد"
        });

        AssertFailure(result, 403);
    }

    [Fact]
    public async Task Endpoint_UpdateFields_HappyPath_Returns200()
    {
        var formId = await _ctx.CreateDraftAsync();

        var result = await _ctx.Service.UpdateFieldsAsync(formId, _ctx.OwnerUserId, new UpdateUserFormFieldsDto
        {
            Fields =
            [
                new UpdateUserFormFieldDto
                {
                    FieldKey = "mobile",
                    Label = "موبایل endpoint"
                }
            ]
        });

        AssertSuccess(result, 200);
        Assert.Contains(result.Data!.Fields, f => f.FieldKey == "mobile" && f.Label == "موبایل endpoint");
    }

    [Fact]
    public async Task Endpoint_UpdateFields_EmptyFields_Returns400()
    {
        var formId = await _ctx.CreateDraftAsync();

        var result = await _ctx.Service.UpdateFieldsAsync(formId, _ctx.OwnerUserId, new UpdateUserFormFieldsDto());

        AssertFailure(result, 400);
    }

    [Fact]
    public async Task Endpoint_UpdateFields_NullDto_Returns400()
    {
        var formId = await _ctx.CreateDraftAsync();

        var result = await _ctx.Service.UpdateFieldsAsync(formId, _ctx.OwnerUserId, null);

        AssertFailure(result, 400);
    }

    [Fact]
    public async Task Endpoint_UpdateFields_NewFieldWithoutType_Returns400()
    {
        var formId = await _ctx.CreateDraftAsync();

        var result = await _ctx.Service.UpdateFieldsAsync(formId, _ctx.OwnerUserId, new UpdateUserFormFieldsDto
        {
            Fields =
            [
                new UpdateUserFormFieldDto
                {
                    FieldKey = "email",
                    Label = "ایمیل"
                }
            ]
        });

        AssertFailure(result, 400);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    private static void AssertSuccess(ApiResponse<UserFormResponseDto> result, int expectedStatusCode)
    {
        Assert.True(result.Success);
        Assert.Equal(expectedStatusCode, result.StatusCode);
        Assert.NotEqual(500, result.StatusCode);
        Assert.True(ControlledErrorHelper.IsSafeUserMessage(result.Message));
    }

    private static void AssertFailure(ApiResponse<UserFormResponseDto> result, int expectedStatusCode)
    {
        Assert.False(result.Success);
        Assert.Equal(expectedStatusCode, result.StatusCode);
        Assert.NotEqual(500, result.StatusCode);
        Assert.True(ControlledErrorHelper.IsSafeUserMessage(result.Message));
    }
}
