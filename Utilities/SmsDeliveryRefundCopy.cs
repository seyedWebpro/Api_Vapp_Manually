using Api_Vapp.Constants;
using Api_Vapp.Models;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// متن‌های کاربرپسند برگشت هزینه پیامک نرسیده — برای کیف پول و گزارش دلیوری (بدون نیاز به تغییر موبایل)
    /// </summary>
    public static class SmsDeliveryRefundCopy
    {
        /// <summary>همان عنوان سایر برگشت‌های پیامک تا در لیست تراکنش موبایل یکدست باشد</summary>
        public const string WalletTitle = "برگشت هزینه پیامک";

        public static string BuildWalletDescription(SmsDeliveryRecord record)
        {
            var moduleLabel = SmsSourceModules.GetPersianLabel(record.SourceModule);
            var categoryLabel = SmsDeliveryCategories.GetPersianLabel(record.DeliveryCategory);
            var statusDetail = !string.IsNullOrWhiteSpace(record.ProviderStatusMessage)
                ? record.ProviderStatusMessage.Trim()
                : categoryLabel;

            return
                $"پیامک به گیرنده نرسید ({statusDetail}). مبلغ {record.ChargedAmount:N0} تومان بابت «{moduleLabel}» به کیف پول برگشت داده شد.";
        }

        /// <summary>
        /// متن statusHint که اپ موبایل از قبل نمایش می‌دهد
        /// </summary>
        public static string BuildStatusHint(SmsDeliveryRecord record, string? categoryLabel = null)
        {
            var label = string.IsNullOrWhiteSpace(categoryLabel)
                ? SmsDeliveryCategories.GetPersianLabel(record.DeliveryCategory)
                : categoryLabel;

            var refunded = record.WalletRefundTransactionId.HasValue;
            var willRefund = !refunded
                && record.ChargedAmount > 0
                && SmsDeliveryCategories.IsWalletRefundEligible(record.DeliveryCategory);

            return record.DeliveryCategory switch
            {
                SmsDeliveryCategories.DeliveredToPhone =>
                    "پیامک با موفقیت به گیرنده تحویل شده است.",

                SmsDeliveryCategories.SentToOperator =>
                    "پیامک به اپراتور ارسال شده و در مسیر تحویل است.",

                SmsDeliveryCategories.NotDelivered when refunded =>
                    "پیامک به گوشی گیرنده نرسیده است. هزینه به کیف پول شما برگشت داده شد.",
                SmsDeliveryCategories.NotDelivered when willRefund =>
                    "پیامک به گوشی گیرنده نرسیده است. هزینه به‌زودی به کیف پول شما برمی‌گردد.",
                SmsDeliveryCategories.NotDelivered =>
                    "پیامک به گوشی گیرنده نرسیده است.",

                SmsDeliveryCategories.PendingApproval =>
                    "پیامک در انتظار تایید است.",

                SmsDeliveryCategories.Rejected when refunded =>
                    "پیامک رد شده است. هزینه به کیف پول شما برگشت داده شد.",
                SmsDeliveryCategories.Rejected when willRefund =>
                    "پیامک رد شده است. هزینه به‌زودی به کیف پول شما برمی‌گردد.",
                SmsDeliveryCategories.Rejected =>
                    "پیامک رد شده است.",

                SmsDeliveryCategories.SendFailed when refunded =>
                    "ارسال پیامک انجام نشد. هزینه به کیف پول شما برگشت داده شد.",
                SmsDeliveryCategories.SendFailed when willRefund =>
                    "ارسال پیامک انجام نشد. هزینه به‌زودی به کیف پول شما برمی‌گردد.",
                SmsDeliveryCategories.SendFailed =>
                    "ارسال پیامک انجام نشد.",

                SmsDeliveryCategories.PendingSync =>
                    "وضعیت تحویل هنوز بررسی نشده است.",

                _ => $"وضعیت فعلی: {label}"
            };
        }
    }
}
