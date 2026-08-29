using Api_Vapp.Models;
using Api_Vapp.Services;
using Xunit;

namespace Api_Vapp.Tests.QuickSend
{
    public class BankAccountSmsDescriptionTests
    {
        [Fact]
        public void BuildSmsContent_WithoutDescription_StartsWithTitle()
        {
            var entity = new BankAccount
            {
                Title = "حساب ملت",
                CardNumber = "6037991234567890"
            };

            var sms = BankAccountService.BuildSmsContent(entity);

            Assert.Equal(
                "حساب ملت\nشماره کارت: 6037991234567890",
                sms);
        }

        [Fact]
        public void BuildSmsContent_WithDescription_PrependsBeforeTitle()
        {
            var entity = new BankAccount
            {
                Title = "حساب ملت",
                SmsCaption = "برای واریز به شماره کارت زیر اقدام کنید",
                AccountNumber = "1234567890",
                CardNumber = "6037991234567890",
                ShebaNumber = "IR120170000000123456789001"
            };

            var sms = BankAccountService.BuildSmsContent(entity);

            Assert.Equal(
                "برای واریز به شماره کارت زیر اقدام کنید\n" +
                "حساب ملت\n" +
                "شماره حساب: 1234567890\n" +
                "شماره کارت: 6037991234567890\n" +
                "شماره شبا: IR120170000000123456789001",
                sms);
        }

        [Fact]
        public void BuildSmsContent_WhitespaceDescription_TreatedAsEmpty()
        {
            var entity = new BankAccount
            {
                Title = "حساب",
                SmsCaption = "   \n\t  ",
                CardNumber = "6037991234567890"
            };

            var sms = BankAccountService.BuildSmsContent(entity);

            Assert.StartsWith("حساب\n", sms);
            Assert.DoesNotContain("\n\n", sms);
        }

        [Fact]
        public void BuildContentPreview_MatchesSmsContent()
        {
            var entity = new BankAccount
            {
                Title = "بانک",
                SmsCaption = "توضیح",
                CardNumber = "6037991234567890"
            };

            Assert.Equal(
                BankAccountService.BuildSmsContent(entity),
                BankAccountService.BuildContentPreview(entity));
        }
    }
}
