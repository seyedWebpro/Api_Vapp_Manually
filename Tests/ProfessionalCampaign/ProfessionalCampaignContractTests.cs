using System.ComponentModel.DataAnnotations;
using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Message;
using Microsoft.EntityFrameworkCore;
using Api_Vapp.Utilities;
using Xunit;

namespace Api_Vapp.Tests.ProfessionalCampaign;

public class ProfessionalCampaignContractTests
{
    [Fact]
    public void CreateDto_RequiresAtLeastTwoSteps()
    {
        var dto = new CreateProfessionalCampaignDto
        {
            Title = "کمپین تست",
            TargetType = ProfessionalCampaignTargetTypes.Notebooks,
            TargetIds = new List<int> { 1 },
            Steps = new List<ProfessionalCampaignStepInputDto>
            {
                new() { Content = "پیام اول" }
            }
        };

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(dto, new ValidationContext(dto), results, true);

        Assert.False(isValid);
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(dto.Steps)));
    }

    [Fact]
    public void Model_HasUniqueCampaignStepOrderAndRecipientMobileIndexes()
    {
        var options = new DbContextOptionsBuilder<Api_Context>()
            .UseInMemoryDatabase($"professional-campaign-model-{Guid.NewGuid():N}")
            .Options;
        using var context = new Api_Context(options);

        var stepIndex = context.Model.FindEntityType("Api_Vapp.Models.ProfessionalCampaignStep")!
            .GetIndexes()
            .Single(index => index.Properties.Select(p => p.Name)
                .SequenceEqual(new[] { "ProfessionalCampaignId", "StepOrder" }));
        var recipientIndex = context.Model.FindEntityType("Api_Vapp.Models.ProfessionalCampaignRecipient")!
            .GetIndexes()
            .Single(index => index.Properties.Select(p => p.Name)
                .SequenceEqual(new[] { "ProfessionalCampaignId", "MobileNumber" }));

        Assert.True(stepIndex.IsUnique);
        Assert.True(recipientIndex.IsUnique);
    }

    [Fact]
    public void ApprovalRequestType_HasPersianAdminTitle()
    {
        Assert.Equal(
            "پیام کمپین حرفه‌ای",
            SmsApprovalRequestTypes.ToPersian(SmsApprovalRequestTypes.ProfessionalCampaignStep));
    }

    [Fact]
    public void Schedule_ConvertsIranOffsetToUtcAndChainsFromActualSendTime()
    {
        var iranTime = new DateTimeOffset(2026, 9, 20, 12, 30, 0, TimeSpan.FromHours(3.5));
        var utc = ProfessionalCampaignSchedule.ToUtc(iranTime);

        Assert.Equal(DateTimeKind.Utc, utc.Kind);
        Assert.Equal(new DateTime(2026, 9, 20, 9, 0, 0, DateTimeKind.Utc), utc);

        var projected = ProfessionalCampaignSchedule.BuildProjectedUtc(utc, new[] { 0, 24 * 60, 7 * 24 * 60 });
        Assert.Equal(utc, projected[0]);
        Assert.Equal(utc.AddDays(1), projected[1]);
        Assert.Equal(utc.AddDays(8), projected[2]);

        var actualSecondSend = utc.AddDays(1).AddSeconds(2);
        Assert.Equal(actualSecondSend.AddDays(7), ProfessionalCampaignSchedule.GetNextUtc(actualSecondSend, 7 * 24 * 60));
    }
}
