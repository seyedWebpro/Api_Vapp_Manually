namespace Api_Vapp.Constants
{
    /// <summary>
    /// ماژول‌های مبدأ ارسال پیامک — برای گزارش‌گیری یکپارچه
    /// </summary>
    public static class SmsSourceModules
    {
        public const string MessageCampaign = "MessageCampaign";
        public const string MessageDirect = "MessageDirect";
        public const string Cashback = "Cashback";
        public const string CashbackScheduled = "CashbackScheduled";
        public const string ReferralProgram = "ReferralProgram";
        public const string AutomatedMessage = "AutomatedMessage";
        public const string BookingReminder = "BookingReminder";
        public const string BookingStatus = "BookingStatus";
        public const string PublicParticipantOtp = "PublicParticipantOtp";
        public const string Manual = "Manual";

        public static readonly IReadOnlyDictionary<string, string> PersianLabels = new Dictionary<string, string>
        {
            [MessageCampaign] = "کمپین پیامکی",
            [MessageDirect] = "ارسال مستقیم پیام",
            [Cashback] = "کش‌بک",
            [CashbackScheduled] = "کش‌بک زمان‌بندی‌شده",
            [ReferralProgram] = "برنامه پاداش",
            [AutomatedMessage] = "پیام خودکار",
            [BookingReminder] = "یادآوری نوبت",
            [BookingStatus] = "وضعیت نوبت",
            [PublicParticipantOtp] = "کد تأیید شرکت‌کننده",
            [Manual] = "ارسال دستی"
        };

        public static string GetPersianLabel(string module) =>
            PersianLabels.TryGetValue(module, out var label) ? label : module;

        /// <summary>
        /// ارسال‌های چندگیرنده (قالب/متن به دفترچه، کمپین، پیام خودکار) باید در گزارش یک ردیف شوند.
        /// ایران‌نوین برای هر شماره Sid جدا می‌دهد؛ گروه‌بندی با SourceEntityId است.
        /// </summary>
        public static bool IsGroupedReportModule(string? module) =>
            module is MessageCampaign or MessageDirect or AutomatedMessage;
    }
}
