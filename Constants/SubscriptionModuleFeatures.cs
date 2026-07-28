namespace Api_Vapp.Constants
{
    /// <summary>
    /// نگاشت ماژول‌های محصول به کد امکان اشتراک.
    /// منبع مرجع برای Attribute روی کنترلرها — امکانات واقعی هر پلن را ادمین تعیین می‌کند.
    /// </summary>
    public static class SubscriptionModuleFeatures
    {
        public const string NumberSeeker = SubscriptionFeatureCodes.NumberSeeker;
        public const string Phonebook = SubscriptionFeatureCodes.Phonebook;
        public const string Messaging = SubscriptionFeatureCodes.Messaging;
        public const string FormBuilder = SubscriptionFeatureCodes.FormBuilder;
        public const string OnlineBooking = SubscriptionFeatureCodes.OnlineBooking;
        public const string FreeQuickSend = SubscriptionFeatureCodes.FreeQuickSend;
        public const string BusinessCard = SubscriptionFeatureCodes.BusinessCard;
        public const string MessageAutomation = SubscriptionFeatureCodes.MessageAutomation;
        public const string BulkCampaign = SubscriptionFeatureCodes.BulkCampaign;
        public const string CashbackWallet = SubscriptionFeatureCodes.CashbackWallet;
        public const string AdvancedAnalytics = SubscriptionFeatureCodes.AdvancedAnalytics;

        /// <summary>
        /// priority_support برای اولویت صف پشتیبانی ادمین است، نه قفل API تیکت کاربر.
        /// </summary>
        public const string PrioritySupport = SubscriptionFeatureCodes.PrioritySupport;
    }
}
