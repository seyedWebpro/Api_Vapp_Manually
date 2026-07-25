namespace Api_Vapp.Constants
{
    /// <summary>
    /// اکشن‌های استاندارد audit.
    /// فاز ۱ فقط بیس را می‌چیند؛ ثابت‌ها برای فازهای بعدی آماده است.
    /// </summary>
    public static class AuditActions
    {
        // —— Subscription / Admin plans (فاز ۲ اولویت ۱)
        public const string SubscriptionPlanCreated = "SubscriptionPlan.Created";
        public const string SubscriptionPlanUpdated = "SubscriptionPlan.Updated";
        public const string SubscriptionPlanPriceUpdated = "SubscriptionPlan.PriceUpdated";
        public const string SubscriptionPlanStatusChanged = "SubscriptionPlan.StatusChanged";
        public const string SubscriptionPlanDeleted = "SubscriptionPlan.Deleted";
        public const string SubscriptionFeatureUpdated = "SubscriptionFeature.Updated";
        public const string UserSubscriptionAssigned = "UserSubscription.Assigned";
        public const string UserSubscriptionRevoked = "UserSubscription.Revoked";
        public const string UserSubscriptionExtended = "UserSubscription.Extended";
        public const string DiscountCodeCreated = "SubscriptionDiscountCode.Created";
        public const string DiscountCodeUpdated = "SubscriptionDiscountCode.Updated";

        // —— Approvals (فاز ۲ اولویت ۲)
        public const string SmsApprovalApproved = "SmsApproval.Approved";
        public const string SmsApprovalRejected = "SmsApproval.Rejected";
        public const string TemplateApprovalApproved = "TemplateApproval.Approved";
        public const string TemplateApprovalRejected = "TemplateApproval.Rejected";

        // —— User / Role (فاز ۲ اولویت ۳)
        public const string UserActivated = "User.Activated";
        public const string UserDeactivated = "User.Deactivated";
        public const string UserUpdated = "User.Updated";
        public const string UserRoleAssigned = "UserRole.Assigned";
        public const string UserRoleRemoved = "UserRole.Removed";

        // —— Payment (فاز ۲ اولویت ۴)
        public const string PaymentRequested = "Payment.Requested";
        public const string PaymentRequestFailed = "Payment.RequestFailed";
        public const string PaymentVerified = "Payment.Verified";
        public const string PaymentVerifyFailed = "Payment.VerifyFailed";
        public const string PaymentCallback = "Payment.Callback";

        // —— Wallet / Cashback (فاز ۲ اولویت ۵)
        public const string WalletCredited = "Wallet.Credited";
        public const string WalletDebited = "Wallet.Debited";
        public const string CashbackApplied = "Cashback.Applied";
        public const string CashbackDraftApproved = "CashbackDraft.Approved";
        public const string CashbackDraftRejected = "CashbackDraft.Rejected";

        // —— Subscription checkout (فاز ۲ اولویت ۶)
        public const string SubscriptionPurchased = "Subscription.Purchased";
        public const string SubscriptionActivated = "Subscription.Activated";
        public const string SubscriptionExpired = "Subscription.Expired";

        // —— Message / Campaign (فاز ۲ اولویت ۷)
        public const string CampaignCreated = "MessageCampaign.Created";
        public const string CampaignStatusChanged = "MessageCampaign.StatusChanged";
        public const string CampaignSent = "MessageCampaign.Sent";

        // —— Content modules (فاز ۲ اولویت ۸)
        public const string BookingSystemStatusChanged = "BookingSystem.StatusChanged";
        public const string BusinessCardStatusChanged = "BusinessCard.StatusChanged";
        public const string UserFormStatusChanged = "UserForm.StatusChanged";
        public const string SupportTicketReplied = "SupportTicket.Replied";
        public const string SupportTicketStatusChanged = "SupportTicket.StatusChanged";
        public const string EducationalVideoCreated = "EducationalVideo.Created";
        public const string EducationalVideoUpdated = "EducationalVideo.Updated";
        public const string EducationalVideoDeleted = "EducationalVideo.Deleted";
        public const string LuckyWheelStatusChanged = "LuckyWheel.StatusChanged";
        public const string ReferralProgramUpdated = "ReferralProgram.Updated";

        // —— Auth (فاز ۲ اولویت ۹)
        public const string AdminLoginSucceeded = "Auth.AdminLoginSucceeded";
        public const string AdminLoginFailed = "Auth.AdminLoginFailed";
        public const string UserLoginSucceeded = "Auth.UserLoginSucceeded";
        public const string UserLoginFailed = "Auth.UserLoginFailed";
    }
}
