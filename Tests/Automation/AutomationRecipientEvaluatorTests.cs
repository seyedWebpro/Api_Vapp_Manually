using Api_Vapp.Interfaces;
using Api_Vapp.Services;
using Api_Vapp.Utilities;
using Xunit;

namespace Api_Vapp.Tests.Automation;

public class AutomationRecipientEvaluatorTests
{
    private readonly AutomationRecipientEvaluator _evaluator = new(null!);

    [Fact]
    public void IsCashbackExpiryEligible_MatchesExactExpiryDate()
    {
        var today = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc);
        var metrics = new AutomationContactMetrics
        {
            ContactId = 1,
            CashbackBalance = 10_000m,
            CashbackExpiryDate = today.AddDays(2)
        };

        Assert.True(_evaluator.IsCashbackExpiryEligible(metrics, today, daysBeforeExpiry: 2));
        Assert.False(_evaluator.IsCashbackExpiryEligible(metrics, today, daysBeforeExpiry: 3));
    }

    [Fact]
    public void IsCashbackExpiryEligible_RequiresPositiveBalance()
    {
        var today = DateTime.UtcNow.Date;
        var metrics = new AutomationContactMetrics
        {
            ContactId = 1,
            CashbackBalance = 0,
            CashbackExpiryDate = today.AddDays(2)
        };

        Assert.False(_evaluator.IsCashbackExpiryEligible(metrics, today, daysBeforeExpiry: 2));
    }

    [Fact]
    public void IsPurchaseReminderEligible_UsesLastPurchaseDate()
    {
        var today = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc);
        var metrics = new AutomationContactMetrics
        {
            ContactId = 1,
            CreatedAt = today.AddDays(-100),
            LastPurchaseAt = today.AddDays(-31)
        };

        Assert.True(_evaluator.IsPurchaseReminderEligible(metrics, today, daysWithoutPurchase: 30));
        Assert.False(_evaluator.IsPurchaseReminderEligible(metrics, today, daysWithoutPurchase: 32));
    }

    [Fact]
    public void IsPurchaseReminderEligible_UsesCreatedAtWhenNoPurchase()
    {
        var today = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc);
        var metrics = new AutomationContactMetrics
        {
            ContactId = 1,
            CreatedAt = today.AddDays(-40),
            LastPurchaseAt = null
        };

        Assert.True(_evaluator.IsPurchaseReminderEligible(metrics, today, daysWithoutPurchase: 30));
        Assert.False(_evaluator.IsPurchaseReminderEligible(metrics, today, daysWithoutPurchase: 45));
    }

    [Fact]
    public void IsCustomEligible_AppliesAndLogicForMultipleConditions()
    {
        var today = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc);
        var conditions = new CustomAutomationConditions
        {
            MinCashbackBalance = 5_000m,
            HasDateOfBirth = true,
            DaysWithoutPurchase = 30
        };

        var eligible = new AutomationContactMetrics
        {
            ContactId = 1,
            CreatedAt = today.AddDays(-100),
            HasDateOfBirth = true,
            CashbackBalance = 8_000m,
            LastPurchaseAt = today.AddDays(-35)
        };

        var ineligible = new AutomationContactMetrics
        {
            ContactId = 1,
            CreatedAt = today.AddDays(-100),
            HasDateOfBirth = true,
            CashbackBalance = 1_000m,
            LastPurchaseAt = today.AddDays(-35)
        };

        Assert.True(_evaluator.IsCustomEligible(eligible, today, conditions));
        Assert.False(_evaluator.IsCustomEligible(ineligible, today, conditions));
    }

    [Fact]
    public void TryParseCustomConditions_RecognizesSupportedKeys()
    {
        const string json = """
            {
              "executionMode": "Multiple",
              "daysWithoutPurchase": 30,
              "minCashbackBalance": 1000,
              "hasDateOfBirth": true
            }
            """;

        var parsed = AutomationActivationConditionsHelper.TryParseCustomConditions(json, out var conditions);

        Assert.True(parsed);
        Assert.Equal("Multiple", conditions.ExecutionMode);
        Assert.Equal(30, conditions.DaysWithoutPurchase);
        Assert.Equal(1000m, conditions.MinCashbackBalance);
        Assert.True(conditions.HasDateOfBirth);
    }

    [Fact]
    public void TryParseCustomConditions_RejectsUnknownOnlyJson()
    {
        const string json = """{"condition":"value"}""";

        var parsed = AutomationActivationConditionsHelper.TryParseCustomConditions(json, out _);

        Assert.False(parsed);
        Assert.True(AutomationActivationConditionsHelper.ContainsOnlyUnrecognizedCustomKeys(json));
    }
}
