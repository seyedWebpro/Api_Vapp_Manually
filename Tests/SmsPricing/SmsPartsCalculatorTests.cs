using Api_Vapp.Services;
using Xunit;

namespace Api_Vapp.Tests.SmsPricing;

public class SmsPartsCalculatorTests
{
    [Fact]
    public void PrepareForSend_AlwaysAppendsSuffix_EvenIfRuleFlagFalse()
    {
        var rules = new SmsPartsRules { IncludeOptOutSuffixInCalculation = false, OptOutSuffix = "لغو11" };
        var prepared = SmsPartsCalculator.PrepareForSend("سلام مشتری", rules);

        Assert.EndsWith("لغو11", prepared);
    }

    [Fact]
    public void PrepareForSend_WhenAlreadyHasSuffix_DoesNotDuplicate()
    {
        var rules = new SmsPartsRules { IncludeOptOutSuffixInCalculation = true, OptOutSuffix = "لغو11" };
        var prepared = SmsPartsCalculator.PrepareForSend("سلام مشتری", rules);

        Assert.EndsWith("لغو11", prepared);
        Assert.DoesNotContain("لغو11\nلغو11", prepared);

        var again = SmsPartsCalculator.PrepareForSend(prepared, rules);
        Assert.Equal(prepared, again);
    }

    [Fact]
    public void CalculateParts_PersianSinglePage_ReturnsOne()
    {
        var rules = SmsPartsRules.Defaults;
        var parts = SmsPartsCalculator.CalculateParts("سلام", rules);
        Assert.Equal(1, parts);
    }

    [Fact]
    public void TryCalculateParts_WhenExceedsMax_ReturnsFalse()
    {
        var rules = new SmsPartsRules
        {
            MaxPages = 1,
            PersianFirstPageChars = 5,
            IncludeOptOutSuffixInCalculation = false
        };

        var ok = SmsPartsCalculator.TryCalculateParts(
            "این متن طولانی‌تر از پنج کاراکتر است",
            rules,
            out var parts,
            out var analysis);

        Assert.False(ok);
        Assert.True(analysis.ExceedsMaxPages);
        Assert.True(parts > 1);
    }

    [Fact]
    public void EstimateBulkCost_Personalized_UsesExpandedPlaceholders()
    {
        var pricing = new SmsPricingRuntime
        {
            CostPerPart = 100m,
            IsBillingEnabled = true,
            IsBillingEffectivelyEnabled = true,
            Rules = new SmsPartsRules { IncludeOptOutSuffixInCalculation = false }
        };

        var shortTemplate = "سلام {{نام}}";
        var (partsPlain, costPlain, _) = SmsPartsCalculator.EstimateBulkCost(
            shortTemplate, isPersonalized: false, recipientsCount: 2, pricing);
        var (partsPersonal, costPersonal, _) = SmsPartsCalculator.EstimateBulkCost(
            shortTemplate, isPersonalized: true, recipientsCount: 2, pricing);

        Assert.True(partsPersonal >= partsPlain);
        Assert.Equal(SmsPartsCalculator.CalculateCost(partsPersonal, 100m, 2), costPersonal);
        Assert.Equal(SmsPartsCalculator.CalculateCost(partsPlain, 100m, 2), costPlain);
    }

    [Fact]
    public void CalculateCost_RoundsAwayFromZero()
    {
        var cost = SmsPartsCalculator.CalculateCost(partsCount: 3, costPerPart: 10.005m, recipientsCount: 1);
        Assert.Equal(30.02m, cost);
    }
}
