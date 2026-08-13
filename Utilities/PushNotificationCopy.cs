using Api_Vapp.Constants;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// متن‌های استاندارد و حرفه‌ای Push برای اپ وپ
    /// </summary>
    public static class PushNotificationCopy
    {
        private static string FormatAmount(decimal amount) =>
            $"{amount:N0} تومان";

        public static (string Title, string Body) WalletCredited(decimal amount, decimal newBalance, string? title = null)
        {
            var t = "شارژ کیف پول";
            var b = string.IsNullOrWhiteSpace(title)
                ? $"{FormatAmount(amount)} به کیف پول شما واریز شد. موجودی فعلی: {FormatAmount(newBalance)}."
                : $"{title.Trim()}: {FormatAmount(amount)} واریز شد. موجودی فعلی: {FormatAmount(newBalance)}.";
            return (t, b);
        }

        public static (string Title, string Body) WalletDebited(decimal amount, decimal newBalance, string? title = null)
        {
            var reason = string.IsNullOrWhiteSpace(title) ? "هزینه سرویس" : title.Trim();
            return (
                "برداشت از کیف پول",
                $"{FormatAmount(amount)} بابت «{reason}» از کیف پول کسر شد. موجودی فعلی: {FormatAmount(newBalance)}.");
        }

        public static (string Title, string Body) InsufficientWallet(decimal required, decimal current)
        {
            return (
                "موجودی ناکافی",
                $"برای تکمیل این عملیات به {FormatAmount(required)} نیاز دارید. موجودی فعلی: {FormatAmount(current)}.");
        }

        public static (string Title, string Body) NewContact(string? contactName)
        {
            var name = string.IsNullOrWhiteSpace(contactName) ? "یک مخاطب جدید" : contactName.Trim();
            return (
                "مخاطب جدید",
                $"«{name}» به دفترچه مخاطبین شما اضافه شد.");
        }

        public static (string Title, string Body) CashbackApplied(int recipientCount, decimal? totalAmount = null)
        {
            var amountPart = totalAmount.HasValue ? $" به مبلغ {FormatAmount(totalAmount.Value)}" : "";
            return (
                "کش‌بک ثبت شد",
                $"کش‌بک برای {recipientCount} مشتری{amountPart} با موفقیت اعمال شد.");
        }

        public static (string Title, string Body) CashbackManual(string? contactName, decimal amount)
        {
            var name = string.IsNullOrWhiteSpace(contactName) ? "مشتری" : contactName.Trim();
            return (
                "کش‌بک جدید",
                $"{FormatAmount(amount)} به‌عنوان کش‌بک برای «{name}» ثبت شد.");
        }

        public static (string Title, string Body) CashbackWithdrawn(string? contactName, decimal amount)
        {
            var name = string.IsNullOrWhiteSpace(contactName) ? "مشتری" : contactName.Trim();
            return (
                "مصرف کش‌بک",
                $"{FormatAmount(amount)} از کش‌بک «{name}» کسر شد.");
        }

        public static (string Title, string Body) AccountImportant(string message)
        {
            return ("اعلان مهم حساب", message);
        }

        public static (string Title, string Body) RoleChanged(string roleName, bool assigned)
        {
            return assigned
                ? ("به‌روزرسانی دسترسی", $"نقش «{roleName}» به حساب شما اختصاص داده شد.")
                : ("به‌روزرسانی دسترسی", $"نقش «{roleName}» از حساب شما حذف شد.");
        }

        public static (string Title, string Body) AccountStatusChanged(bool isActive)
        {
            return isActive
                ? ("فعال‌سازی حساب", "حساب کاربری شما دوباره فعال شد. خوش آمدید.")
                : ("غیرفعال‌سازی حساب", "حساب کاربری شما غیرفعال شد. برای پیگیری با پشتیبانی وپ در ارتباط باشید.");
        }

        public static (string Title, string Body) SubscriptionActivated(string? planName, DateTime expiresAt)
        {
            var plan = string.IsNullOrWhiteSpace(planName) ? "اشتراک" : planName.Trim();
            return (
                "اشتراک فعال شد",
                $"پلن «{plan}» برای شما فعال شد و تا {expiresAt:yyyy/MM/dd} معتبر است.");
        }

        public static (string Title, string Body) SubscriptionCancelled(string? planName)
        {
            var plan = string.IsNullOrWhiteSpace(planName) ? "اشتراک" : planName.Trim();
            return (
                "اشتراک لغو شد",
                $"پلن «{plan}» لغو شد. برای ادامه امکانات، می‌توانید اشتراک جدید تهیه کنید.");
        }

        public static (string Title, string Body) PaymentFailed()
        {
            return (
                "پرداخت ناموفق",
                "پرداخت شما تکمیل نشد. اگر مبلغی کسر شده، معمولاً تا ۲۴ ساعت به حساب بازمی‌گردد؛ در غیر این صورت با پشتیبانی تماس بگیرید.");
        }

        public static (string Title, string Body) CampaignCompleted(string? campaignTitle, int successCount, int failCount)
        {
            var name = string.IsNullOrWhiteSpace(campaignTitle) ? "کمپین پیامکی" : campaignTitle.Trim();
            var failPart = failCount > 0 ? $" و {failCount} ناموفق" : "";
            return (
                "نتیجه کمپین",
                $"ارسال «{name}» انجام شد: {successCount} موفق{failPart}.");
        }

        public static (string Title, string Body) CampaignRejected(string? reason)
        {
            var detail = string.IsNullOrWhiteSpace(reason)
                ? "لطفاً محتوا را بازبینی و دوباره ارسال کنید."
                : reason.Trim();
            return (
                "کمپین تأیید نشد",
                $"درخواست کمپین شما رد شد. {detail}");
        }

        public static (string Title, string Body) MessageApproved(string? titlePreview = null, bool scheduled = false)
        {
            var name = string.IsNullOrWhiteSpace(titlePreview) ? "پیام شما" : $"«{titlePreview.Trim()}»";
            return scheduled
                ? ("پیام تأیید شد", $"{name} تأیید شد و در زمان مقرر ارسال می‌شود.")
                : ("پیام تأیید شد", $"{name} تأیید شد و ارسال انجام شد.");
        }

        public static (string Title, string Body) MessageRejected(string? reason, string? titlePreview = null)
        {
            var name = string.IsNullOrWhiteSpace(titlePreview) ? "پیام شما" : $"«{titlePreview.Trim()}»";
            var detail = string.IsNullOrWhiteSpace(reason)
                ? "لطفاً محتوا را بازبینی و دوباره ارسال کنید."
                : reason.Trim();
            return (
                "پیام تأیید نشد",
                $"{name} رد شد. دلیل: {detail}");
        }

        public static (string Title, string Body) TemplateApproved(string? templateName)
        {
            var name = string.IsNullOrWhiteSpace(templateName) ? "قالب شما" : $"«{templateName.Trim()}»";
            return (
                "قالب تأیید شد",
                $"{name} تأیید شد و می‌توانید از آن برای ارسال پیامک استفاده کنید.");
        }

        public static (string Title, string Body) TemplateRejected(string? templateName, string? reason)
        {
            var name = string.IsNullOrWhiteSpace(templateName) ? "قالب شما" : $"«{templateName.Trim()}»";
            var detail = string.IsNullOrWhiteSpace(reason)
                ? "لطفاً محتوا را بازبینی و دوباره ارسال کنید."
                : reason.Trim();
            return (
                "قالب تأیید نشد",
                $"{name} رد شد. دلیل: {detail}");
        }

        public static (string Title, string Body) QuickSendApproved(string itemTypePersian, string? title)
        {
            var name = string.IsNullOrWhiteSpace(title)
                ? itemTypePersian
                : $"«{title.Trim()}»";
            return (
                "ارسال سریع تأیید شد",
                $"{name} ({itemTypePersian}) تأیید شد. از این به بعد می‌توانید بدون تأیید مجدد ارسال کنید.");
        }

        public static (string Title, string Body) QuickSendRejected(string itemTypePersian, string? title, string? reason)
        {
            var name = string.IsNullOrWhiteSpace(title)
                ? itemTypePersian
                : $"«{title.Trim()}»";
            var detail = string.IsNullOrWhiteSpace(reason)
                ? "لطفاً محتوا را بازبینی و دوباره ارسال کنید."
                : reason.Trim();
            return (
                "ارسال سریع تأیید نشد",
                $"{name} ({itemTypePersian}) رد شد. دلیل: {detail}");
        }

        public static (string Title, string Body) FinancialDailyReport(
            decimal balance,
            decimal credited,
            decimal debited,
            int transactionCount)
        {
            return (
                "خلاصه مالی روزانه",
                $"موجودی کیف پول: {FormatAmount(balance)}. امروز {FormatAmount(credited)} واریز و {FormatAmount(debited)} برداشت در {transactionCount} تراکنش.");
        }

        public static (string Title, string Body) EducationTip(string videoTitle)
        {
            var title = string.IsNullOrWhiteSpace(videoTitle) ? "محتوای آموزشی جدید" : videoTitle.Trim();
            return (
                "آموزش جدید در وپ",
                $"«{title}» در بخش آموزش منتشر شد. همین حالا مشاهده کنید.");
        }

        public static (string Title, string Body) AppUpdate(string version, string? notes = null)
        {
            var body = string.IsNullOrWhiteSpace(notes)
                ? $"نسخه {version} وپ منتشر شد. برای تجربه بهتر، اپلیکیشن را به‌روزرسانی کنید."
                : $"نسخه {version} منتشر شد. {notes.Trim()}";
            return ("به‌روزرسانی وپ", body);
        }

        public static (string Title, string Body) Suggestion(string title, string body) =>
            (string.IsNullOrWhiteSpace(title) ? "پیشنهاد وپ" : title.Trim(), body.Trim());
    }
}
