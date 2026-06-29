namespace Api_Vapp.Constants
{
    /// <summary>
    /// پیام‌های کنترل‌شده ماژول اشتراک — فقط فارسی و ثابت
    /// </summary>
    public static class SubscriptionMessages
    {
        public const string PlanNotFound = "پلن اشتراک یافت نشد";
        public const string FreePlanNotPurchasable = "پلن رایگان قابل خرید نیست";
        public const string PlanAlreadyActive = "این پلن در حال حاضر برای شما فعال است";
        public const string WalletGatewayComingSoon = "پرداخت درون برنامه‌ای به‌زودی فعال می‌شود";
        public const string UnsupportedGateway = "درگاه پرداخت انتخاب‌شده پشتیبانی نمی‌شود";
        public const string FeatureNotAvailable = "این امکان در اشتراک فعال شما موجود نیست";
        public const string ActivationSuccess = "اشتراک با موفقیت فعال شد";
        public const string RedirectToGateway = "در حال انتقال به درگاه پرداخت";
        public const string FreePlanNotConfigured = "پلن رایگان در سیستم پیکربندی نشده است. لطفاً با پشتیبانی تماس بگیرید.";
        public const string ActivationFailed = "مشکلی در فعال‌سازی اشتراک پیش آمد. لطفاً با پشتیبانی تماس بگیرید.";
        public const string PaymentCreateFailed = "مشکلی در ایجاد پرداخت پیش آمد. لطفاً دوباره تلاش کنید.";
        public const string DiscountInvalid = "کد تخفیف معتبر نیست";
        public const string DiscountNotStarted = "کد تخفیف هنوز فعال نشده است";
        public const string DiscountExpired = "کد تخفیف منقضی شده است";
        public const string DiscountWrongPlan = "این کد تخفیف برای پلن انتخاب‌شده قابل استفاده نیست";
        public const string DiscountMinAmount = "حداقل مبلغ سفارش برای این کد تخفیف رعایت نشده است";
        public const string DiscountLimitReached = "ظرفیت استفاده از این کد تخفیف تکمیل شده است";
        public const string DiscountAlreadyUsed = "شما قبلاً از این کد تخفیف استفاده کرده‌اید";
        public const string DiscountNotApplicable = "کد تخفیف برای این سفارش قابل اعمال نیست";
    }
}
