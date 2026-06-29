namespace Api_Vapp.Constants
{
    /// <summary>
    /// کدهای ثابت امکانات اشتراک — هر ماژول جدید باید اینجا تعریف شود.
    /// </summary>
    public static class SubscriptionFeatureCodes
    {
        public const string NumberSeeker = "number_seeker";
        public const string Phonebook = "phonebook";
        public const string Messaging = "messaging";
        public const string FormBuilder = "form_builder";
        public const string OnlineBooking = "online_booking";

        public const string FreeQuickSend = "free_quick_send";
        public const string BusinessCard = "business_card";
        public const string MessageAutomation = "message_automation";
        public const string BulkCampaign = "bulk_campaign";
        public const string CashbackWallet = "cashback_wallet";
        public const string PrioritySupport = "priority_support";
        public const string AdvancedAnalytics = "advanced_analytics";

        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            NumberSeeker,
            Phonebook,
            Messaging,
            FormBuilder,
            OnlineBooking,
            FreeQuickSend,
            BusinessCard,
            MessageAutomation,
            BulkCampaign,
            CashbackWallet,
            PrioritySupport,
            AdvancedAnalytics
        };

        public static bool IsKnown(string code) =>
            !string.IsNullOrWhiteSpace(code) && All.Contains(code.Trim());
    }
}
