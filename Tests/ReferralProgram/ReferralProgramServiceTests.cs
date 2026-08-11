using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.ReferralProgram;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api_Vapp.Tests.ReferralProgram;

public class ReferralProgramServiceTests : IAsyncLifetime
{
    private ReferralProgramTestContext _ctx = null!;

    public async Task InitializeAsync()
    {
        _ctx = await ReferralProgramTestContext.CreateAsync();
    }

    public Task DisposeAsync()
    {
        _ctx.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ValidateStep1_ValidRequest_Returns200WithDraftId()
    {
        var result = await _ctx.Service.ValidateStep1Async(_ctx.OwnerUserId, _ctx.BuildStep1Dto());

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Data?.DraftId);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task ValidateStep1_EmptyTitle_Returns400()
    {
        var result = await _ctx.Service.ValidateStep1Async(
            _ctx.OwnerUserId,
            _ctx.BuildStep1Dto(s => s.Title = "   "));

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task ValidateStep2_MissingDraftId_Returns400()
    {
        var result = await _ctx.Service.ValidateStep2Async(_ctx.OwnerUserId, new ReferralStep2Dto
        {
            TargetAudience = ReferralTargetAudience.All
        });

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task ValidateStep2_TagEnabledWithoutIds_Returns400()
    {
        var step1Result = await _ctx.Service.ValidateStep1Async(_ctx.OwnerUserId, _ctx.BuildStep1Dto());
        var draftId = step1Result.Data!.DraftId!;

        var result = await _ctx.Service.ValidateStep2Async(_ctx.OwnerUserId, new ReferralStep2Dto
        {
            DraftId = draftId,
            TargetAudience = ReferralTargetAudience.All,
            SendToSpecificTags = true,
            TargetTagIds = null
        });

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task SaveStep3Settings_ReturnsContactsCountFromStep2()
    {
        var step1 = _ctx.BuildStep1Dto();
        var step1Result = await _ctx.Service.ValidateStep1Async(_ctx.OwnerUserId, step1);
        var draftId = step1Result.Data!.DraftId!;

        var step2Result = await _ctx.Service.ValidateStep2Async(_ctx.OwnerUserId, new ReferralStep2Dto
        {
            DraftId = draftId,
            TargetAudience = ReferralTargetAudience.All
        });
        Assert.True(step2Result.Success);
        Assert.Equal(3, step2Result.Data!.TotalContactsCount);

        var result = await _ctx.Service.SaveStep3SettingsAsync(_ctx.OwnerUserId, new SaveReferralStep3RequestDto
        {
            DraftId = draftId,
            Settings = _ctx.BuildStep3Settings()
        });

        Assert.True(result.Success);
        Assert.Equal(3, result.Data!.ContactsCount);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Confirm_FullWizard_Returns201()
    {
        var (programId, publicCode) = await _ctx.CreateConfirmedProgramAsync();

        Assert.True(programId > 0);
        Assert.StartsWith("REF", publicCode);

        var getResult = await _ctx.Service.GetByIdAsync(programId, _ctx.OwnerUserId);
        Assert.True(getResult.Success);
        Assert.Equal(publicCode, getResult.Data!.PublicCode);
        AssertNoServerError(getResult);
    }

    [Fact]
    public async Task Confirm_InvalidDraftId_Returns400()
    {
        var result = await _ctx.Service.ConfirmAsync(_ctx.OwnerUserId, new ConfirmReferralProgramDto
        {
            DraftId = "invalid-draft-id"
        });

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
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
    public async Task GetPrograms_Returns200()
    {
        await _ctx.CreateConfirmedProgramAsync();

        var result = await _ctx.Service.GetProgramsAsync(_ctx.OwnerUserId, 1, 10);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.NotEmpty(result.Data!.Programs);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task GetDashboardStats_Returns200()
    {
        var result = await _ctx.Service.GetDashboardStatsAsync(_ctx.OwnerUserId);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task GetNotebooks_Returns200()
    {
        var result = await _ctx.Service.GetNotebooksAsync(_ctx.OwnerUserId);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Contains(result.Data!, n => n.Id == _ctx.NotebookId);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task GetContacts_ReturnsPagedContactsForOwner()
    {
        var result = await _ctx.Service.GetContactsAsync(_ctx.OwnerUserId, pageNumber: 1, pageSize: 20);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.True(result.Data!.TotalCount >= 3);
        Assert.Contains(result.Data.Contacts, c => c.Id == _ctx.ContactId);
        Assert.Contains(result.Data.Contacts, c => c.Id == _ctx.ContactId2);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task GetContacts_FilterByNotebook_ReturnsOnlyNotebookMembers()
    {
        var result = await _ctx.Service.GetContactsAsync(
            _ctx.OwnerUserId, pageNumber: 1, pageSize: 20, notebookId: _ctx.NotebookId);

        Assert.True(result.Success);
        Assert.All(result.Data!.Contacts, c => Assert.Equal(_ctx.NotebookId, c.ContactNotebookId));
        AssertNoServerError(result);
    }

    [Fact]
    public async Task GetContacts_InvalidNotebook_Returns404()
    {
        var result = await _ctx.Service.GetContactsAsync(
            _ctx.OwnerUserId, notebookId: 99999999);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task ValidateStep2_Individual_EmptyContactIds_Returns400()
    {
        var step1Result = await _ctx.Service.ValidateStep1Async(_ctx.OwnerUserId, _ctx.BuildStep1Dto());
        var draftId = step1Result.Data!.DraftId!;

        var result = await _ctx.Service.ValidateStep2Async(_ctx.OwnerUserId, new ReferralStep2Dto
        {
            DraftId = draftId,
            TargetAudience = ReferralTargetAudience.Individual,
            TargetContactIds = new List<int>()
        });

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains(result.Errors!, e => e.Contains("حداقل یک مخاطب"));
        AssertNoServerError(result);
    }

    [Fact]
    public async Task ValidateStep2_Individual_InvalidContactId_Returns400()
    {
        var step1Result = await _ctx.Service.ValidateStep1Async(_ctx.OwnerUserId, _ctx.BuildStep1Dto());
        var draftId = step1Result.Data!.DraftId!;

        var result = await _ctx.Service.ValidateStep2Async(_ctx.OwnerUserId, new ReferralStep2Dto
        {
            DraftId = draftId,
            TargetAudience = ReferralTargetAudience.Individual,
            TargetContactIds = new List<int> { 99999999 }
        });

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains(result.Errors!, e => e.Contains("نامعتبر"));
        AssertNoServerError(result);
    }

    [Fact]
    public async Task ValidateStep2_Individual_ValidContacts_ReturnsCount()
    {
        var step1Result = await _ctx.Service.ValidateStep1Async(_ctx.OwnerUserId, _ctx.BuildStep1Dto());
        var draftId = step1Result.Data!.DraftId!;

        var result = await _ctx.Service.ValidateStep2Async(_ctx.OwnerUserId, new ReferralStep2Dto
        {
            DraftId = draftId,
            TargetAudience = ReferralTargetAudience.Individual,
            TargetContactIds = new List<int> { _ctx.ContactId, _ctx.ContactId2, _ctx.ContactId2 }
        });

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(2, result.Data!.TotalContactsCount);
        Assert.Contains("انتخاب دستی", result.Data.TargetAudienceDescription);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Confirm_IndividualAudience_PersistsTargetContactIds()
    {
        var step1 = _ctx.BuildStep1Dto();
        var step1Result = await _ctx.Service.ValidateStep1Async(_ctx.OwnerUserId, step1);
        var draftId = step1Result.Data!.DraftId!;

        var selected = new List<int> { _ctx.ContactId, _ctx.ContactId3 };
        var step2Result = await _ctx.Service.ValidateStep2Async(_ctx.OwnerUserId, new ReferralStep2Dto
        {
            DraftId = draftId,
            TargetAudience = ReferralTargetAudience.Individual,
            TargetContactIds = selected
        });
        Assert.True(step2Result.Success);
        Assert.Equal(2, step2Result.Data!.TotalContactsCount);

        var step3Result = await _ctx.Service.SaveStep3SettingsAsync(_ctx.OwnerUserId, new SaveReferralStep3RequestDto
        {
            DraftId = draftId,
            Settings = _ctx.BuildStep3Settings()
        });
        Assert.True(step3Result.Success);
        Assert.Equal(2, step3Result.Data!.ContactsCount);

        var confirmResult = await _ctx.Service.ConfirmAsync(_ctx.OwnerUserId, new ConfirmReferralProgramDto
        {
            DraftId = draftId
        });

        Assert.True(confirmResult.Success);
        Assert.Equal(201, confirmResult.StatusCode);
        Assert.Equal(ReferralTargetAudience.Individual, confirmResult.Data!.Program.TargetAudience);
        Assert.NotNull(confirmResult.Data.Program.TargetContactIds);
        Assert.Equal(2, confirmResult.Data.Program.TargetContactIds!.Count);
        Assert.Contains(_ctx.ContactId, confirmResult.Data.Program.TargetContactIds);
        Assert.Contains(_ctx.ContactId3, confirmResult.Data.Program.TargetContactIds);
        Assert.Equal(2, confirmResult.Data.SmsSentCount);
        AssertNoServerError(confirmResult);
    }

    [Fact]
    public async Task ToggleStatus_Returns200()
    {
        var (programId, _) = await _ctx.CreateConfirmedProgramAsync();

        var result = await _ctx.Service.ToggleStatusAsync(programId, _ctx.OwnerUserId);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.False(result.Data!.IsActive);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Delete_Returns200()
    {
        var (programId, _) = await _ctx.CreateConfirmedProgramAsync();

        var result = await _ctx.Service.DeleteAsync(programId, _ctx.OwnerUserId);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Inquire_ValidCode_Returns200()
    {
        var (_, publicCode) = await _ctx.CreateConfirmedProgramAsync();

        var result = await _ctx.Service.InquireCodeAsync(_ctx.OwnerUserId, new InquireReferralCodeDto
        {
            Code = publicCode
        });

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.True(result.Data!.IsValid);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Inquire_UnknownCode_Returns200WithInvalidFlag()
    {
        var result = await _ctx.Service.InquireCodeAsync(_ctx.OwnerUserId, new InquireReferralCodeDto
        {
            Code = "REF000000"
        });

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.False(result.Data!.IsValid);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Inquire_ExpiredProgram_Returns200WithInvalidFlag()
    {
        var (programId, publicCode) = await _ctx.CreateConfirmedProgramAsync();

        var program = await _ctx.Context.ReferralPrograms.FirstAsync(p => p.Id == programId);
        program.EndDate = DateTime.UtcNow.AddDays(-1);
        program.StartDate = DateTime.UtcNow.AddDays(-10);
        await _ctx.Context.SaveChangesAsync();

        var result = await _ctx.Service.InquireCodeAsync(_ctx.OwnerUserId, new InquireReferralCodeDto
        {
            Code = publicCode
        });

        Assert.True(result.Success);
        Assert.False(result.Data!.IsValid);
        Assert.True(result.Data.IsExpired);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Redeem_ValidFixedAmountCode_Returns201()
    {
        var (_, publicCode) = await _ctx.CreateConfirmedProgramAsync();

        var result = await _ctx.Service.RedeemCodeAsync(_ctx.OwnerUserId, new RedeemReferralCodeDto
        {
            Code = publicCode,
            CustomerContactId = _ctx.ContactId,
            ReferrerContactId = _ctx.ContactId
        });

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.True(result.Data!.CustomerDiscountAmount > 0);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Redeem_UnknownCode_Returns404()
    {
        var result = await _ctx.Service.RedeemCodeAsync(_ctx.OwnerUserId, new RedeemReferralCodeDto
        {
            Code = "REF000000"
        });

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Redeem_PercentageWithoutPurchase_Returns400()
    {
        var (_, publicCode) = await _ctx.CreateConfirmedProgramAsync(s =>
        {
            s.RewardType = ReferralRewardTypes.Percentage;
            s.ReferrerRewardValue = 10m;
            s.CustomerRewardValue = 5m;
        });

        var result = await _ctx.Service.RedeemCodeAsync(_ctx.OwnerUserId, new RedeemReferralCodeDto
        {
            Code = publicCode
        });

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task GetHistory_AfterRedeem_Returns200()
    {
        var (programId, publicCode) = await _ctx.CreateConfirmedProgramAsync();

        await _ctx.Service.RedeemCodeAsync(_ctx.OwnerUserId, new RedeemReferralCodeDto
        {
            Code = publicCode
        });

        var result = await _ctx.Service.GetHistoryAsync(programId, _ctx.OwnerUserId, 1, 10);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.NotEmpty(result.Data!.Usages);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Update_OnlyTitle_Returns200()
    {
        var (programId, _) = await _ctx.CreateConfirmedProgramAsync();

        var result = await _ctx.Service.UpdateProgramAsync(programId, _ctx.OwnerUserId, new UpdateReferralProgramDto
        {
            Title = $"عنوان جدید {Guid.NewGuid():N}"[..20]
        });

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Update_EmptyPayload_Returns400()
    {
        var (programId, _) = await _ctx.CreateConfirmedProgramAsync();

        var result = await _ctx.Service.UpdateProgramAsync(programId, _ctx.OwnerUserId, new UpdateReferralProgramDto());

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        AssertNoServerError(result);
    }

    [Fact]
    public async Task Update_NotFound_Returns404()
    {
        var result = await _ctx.Service.UpdateProgramAsync(99999999, _ctx.OwnerUserId, new UpdateReferralProgramDto
        {
            Title = "test"
        });

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
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
