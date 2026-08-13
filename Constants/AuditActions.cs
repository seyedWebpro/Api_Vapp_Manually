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
        public const string QuickSendApprovalApproved = "QuickSendApproval.Approved";
        public const string QuickSendApprovalRejected = "QuickSendApproval.Rejected";

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
        public const string PaymentCancelled = "Payment.Cancelled";
        public const string PaymentCancelDenied = "Payment.CancelDenied";
        public const string PaymentGatewayAuthorityIssued = "Payment.GatewayAuthorityIssued";
        public const string PaymentGatewayAuthorityFailed = "Payment.GatewayAuthorityFailed";

        // —— Wallet / Cashback (فاز ۲ اولویت ۵)
        public const string WalletCredited = "Wallet.Credited";
        public const string WalletDebited = "Wallet.Debited";
        public const string CashbackApplied = "Cashback.Applied";
        public const string CashbackDraftApproved = "CashbackDraft.Approved";
        public const string CashbackDraftRejected = "CashbackDraft.Rejected";
        public const string WalletReferralRewardPaid = "WalletReferral.RewardPaid";
        public const string WalletReferralSettingUpdated = "WalletReferralSetting.Updated";
        public const string SmsPricingSettingUpdated = "SmsPricingSetting.Updated";

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
        public const string BusinessCardCreated = "BusinessCard.Created";
        public const string BusinessCardUpdated = "BusinessCard.Updated";
        public const string BusinessCardDeleted = "BusinessCard.Deleted";
        public const string UserFormStatusChanged = "UserForm.StatusChanged";
        public const string SupportTicketReplied = "SupportTicket.Replied";
        public const string SupportTicketStatusChanged = "SupportTicket.StatusChanged";
        public const string EducationalVideoCreated = "EducationalVideo.Created";
        public const string EducationalVideoUpdated = "EducationalVideo.Updated";
        public const string EducationalVideoDeleted = "EducationalVideo.Deleted";
        public const string AutomationTypeUpdated = "AutomationType.Updated";
        public const string AutomationTypeDeleted = "AutomationType.Deleted";
        public const string AppBannerUpdated = "AppBanner.Updated";
        public const string LuckyWheelStatusChanged = "LuckyWheel.StatusChanged";
        public const string LuckyWheelCreated = "LuckyWheel.Created";
        public const string LuckyWheelUpdated = "LuckyWheel.Updated";
        public const string LuckyWheelDeleted = "LuckyWheel.Deleted";
        public const string ReferralProgramUpdated = "ReferralProgram.Updated";
        public const string ReferralProgramCreated = "ReferralProgram.Created";
        public const string ReferralProgramDeleted = "ReferralProgram.Deleted";
        public const string ReferralProgramActivated = "ReferralProgram.Activated";

        // —— Auth (فاز ۲ اولویت ۹)
        public const string AdminLoginSucceeded = "Auth.AdminLoginSucceeded";
        public const string AdminLoginFailed = "Auth.AdminLoginFailed";
        public const string UserLoginSucceeded = "Auth.UserLoginSucceeded";
        public const string UserLoginFailed = "Auth.UserLoginFailed";

        // —— NumberSeeker (فاز ۳)
        public const string NumberSeekerTaskCreated = "NumberSeeker.TaskCreated";
        public const string NumberSeekerTaskCompleted = "NumberSeeker.TaskCompleted";
        public const string NumberSeekerTaskFailed = "NumberSeeker.TaskFailed";
        public const string NumberSeekerTaskCancelled = "NumberSeeker.TaskCancelled";
        public const string NumberSeekerTaskImported = "NumberSeeker.TaskImported";

        // —— Subscription feature / discount (فاز ۳)
        public const string SubscriptionFeatureCreated = "SubscriptionFeature.Created";
        public const string SubscriptionFeatureDeleted = "SubscriptionFeature.Deleted";
        public const string DiscountCodeDeleted = "SubscriptionDiscountCode.Deleted";

        // —— Booking appointment (فاز ۳)
        public const string BookingAppointmentCreated = "BookingAppointment.Created";
        public const string BookingAppointmentUpdated = "BookingAppointment.Updated";
        public const string BookingAppointmentStatusChanged = "BookingAppointment.StatusChanged";
        public const string BookingAppointmentCancelled = "BookingAppointment.Cancelled";

        // —— Role (فاز ۳)
        public const string RoleCreated = "Role.Created";
        public const string RoleUpdated = "Role.Updated";
        public const string RoleDeleted = "Role.Deleted";

        // —— QuickAction (فاز ۳)
        public const string QuickActionCreated = "QuickAction.Created";
        public const string QuickActionUpdated = "QuickAction.Updated";
        public const string QuickActionDeleted = "QuickAction.Deleted";

        // —— SocialMediaLink
        public const string SocialMediaLinkCreated = "SocialMediaLink.Created";
        public const string SocialMediaLinkUpdated = "SocialMediaLink.Updated";
        public const string SocialMediaLinkDeleted = "SocialMediaLink.Deleted";
        public const string SocialMediaLinkSetDefault = "SocialMediaLink.SetDefault";

        // —— SpecialOccasion (فاز ۳)
        public const string SpecialOccasionCreated = "SpecialOccasion.Created";
        public const string SpecialOccasionUpdated = "SpecialOccasion.Updated";
        public const string SpecialOccasionDeleted = "SpecialOccasion.Deleted";

        // —— ContactNotebook (فاز ۳)
        public const string ContactNotebookCreated = "ContactNotebook.Created";
        public const string ContactNotebookUpdated = "ContactNotebook.Updated";
        public const string ContactNotebookDeleted = "ContactNotebook.Deleted";

        // —— Contact (فاز ۳)
        public const string ContactCreated = "Contact.Created";
        public const string ContactUpdated = "Contact.Updated";
        public const string ContactDeleted = "Contact.Deleted";

        // —— Support ticket (کاربر) (فاز ۳)
        public const string SupportTicketCreated = "SupportTicket.Created";
        public const string SupportTicketUserReplied = "SupportTicket.UserReplied";
        public const string SupportTicketUserDeleted = "SupportTicket.UserDeleted";

        // —— Background jobs (فاز ۳)
        public const string CampaignAutoSent = "MessageCampaign.AutoSent";
        public const string MessageAutoSent = "Message.AutoSent";
        public const string AutomatedMessageQueued = "AutomatedMessage.Queued";
        public const string AutomatedMessageCancelled = "AutomatedMessage.Cancelled";
        public const string AutomatedMessageDeleted = "AutomatedMessage.Deleted";
        public const string AutomatedMessageStatusChanged = "AutomatedMessage.StatusChanged";
    }
}
