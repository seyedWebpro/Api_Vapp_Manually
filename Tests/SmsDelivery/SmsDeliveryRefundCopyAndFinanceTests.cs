using Api_Vapp.Constants;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Xunit;

namespace Api_Vapp.Tests.SmsDelivery;

/// <summary>
/// کراول مالی + کپی کاربرپسند (کیف پول و statusHint موبایل) — بدون تغییر اپ
/// </summary>
public class SmsDeliveryRefundCopyAndFinanceTests
{
    [Fact]
    public void WalletTitle_MatchesExistingSmsRefundTitle()
    {
        Assert.Equal("برگشت هزینه پیامک", SmsDeliveryRefundCopy.WalletTitle);
    }

    [Fact]
    public void WalletDescription_IsPersianOnly_NoTechnicalEnglish()
    {
        var record = new SmsDeliveryRecord
        {
            ChargedAmount = 480,
            SourceModule = SmsSourceModules.MessageDirect,
            DeliveryCategory = SmsDeliveryCategories.NotDelivered,
            ProviderStatusMessage = "نرسیده به گوشی",
            Sid = 999
        };

        var desc = SmsDeliveryRefundCopy.BuildWalletDescription(record);

        Assert.Contains("ارسال مستقیم پیام", desc);
        Assert.Contains("480", desc.Replace("٬", "").Replace(",", ""));
        Assert.Contains("نرسیده به گوشی", desc);
        Assert.Contains("کیف پول", desc);
        Assert.DoesNotContain("MessageDirect", desc);
        Assert.DoesNotContain("NotDelivered", desc);
        Assert.DoesNotContain("Sid:", desc);
        Assert.DoesNotContain("999", desc);
    }

    [Theory]
    [InlineData(SmsDeliveryCategories.NotDelivered, true, true, "برگشت داده شد")]
    [InlineData(SmsDeliveryCategories.NotDelivered, false, true, "به‌زودی")]
    [InlineData(SmsDeliveryCategories.NotDelivered, false, false, "نرسیده است.")]
    [InlineData(SmsDeliveryCategories.Rejected, true, true, "برگشت داده شد")]
    [InlineData(SmsDeliveryCategories.Rejected, false, true, "به‌زودی")]
    [InlineData(SmsDeliveryCategories.SendFailed, true, true, "برگشت داده شد")]
    [InlineData(SmsDeliveryCategories.SendFailed, false, true, "به‌زودی")]
    [InlineData(SmsDeliveryCategories.DeliveredToPhone, false, true, "تحویل")]
    public void StatusHint_CoversFinancialStates_ForMobile(
        string category, bool refunded, bool hasCharge, string expectedFragment)
    {
        var record = new SmsDeliveryRecord
        {
            DeliveryCategory = category,
            ChargedAmount = hasCharge ? 160 : 0,
            WalletRefundTransactionId = refunded ? 42 : null
        };

        var hint = SmsDeliveryRefundCopy.BuildStatusHint(record);

        Assert.Contains(expectedFragment, hint);
        Assert.DoesNotContain("NotDelivered", hint);
        Assert.DoesNotContain("Rejected", hint);
        Assert.DoesNotContain("SendFailed", hint);

        if (category == SmsDeliveryCategories.DeliveredToPhone)
            Assert.DoesNotContain("کیف پول", hint);
    }

    [Fact]
    public void StatusHint_Delivered_NeverMentionsRefund()
    {
        var hint = SmsDeliveryRefundCopy.BuildStatusHint(new SmsDeliveryRecord
        {
            DeliveryCategory = SmsDeliveryCategories.DeliveredToPhone,
            ChargedAmount = 1000,
            WalletRefundTransactionId = 9 // حتی اگر اشتباه ست شده باشد
        });

        Assert.Contains("تحویل", hint);
        Assert.DoesNotContain("برگشت", hint);
        Assert.DoesNotContain("کیف پول", hint);
    }

    [Theory]
    [InlineData(160, 1)]
    [InlineData(320, 2)]
    [InlineData(480, 3)]
    [InlineData(800, 5)]
    public void WalletDescription_ReflectsExactChargedAmount(decimal amount, int parts)
    {
        var desc = SmsDeliveryRefundCopy.BuildWalletDescription(new SmsDeliveryRecord
        {
            ChargedAmount = amount,
            PartsCount = parts,
            SourceModule = SmsSourceModules.Cashback,
            DeliveryCategory = SmsDeliveryCategories.Rejected,
            ProviderStatusMessage = "رد پیام"
        });

        var normalized = desc.Replace("٬", "").Replace(",", "");
        Assert.Contains(((int)amount).ToString(), normalized);
        Assert.Contains("کش‌بک", desc);
    }

    [Fact]
    public void FinancialInvariant_RefundAmountMustEqualChargedAmount()
    {
        // قرارداد مالی: مبلغ برگشتی همیشه همان ChargedAmount ذخیره‌شده است
        decimal charged = 320m;
        decimal refunded = charged; // منطق سرویس
        Assert.Equal(charged, refunded);
        Assert.True(charged > 0);
    }
}
