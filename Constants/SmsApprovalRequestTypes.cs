namespace Api_Vapp.Constants
{
    public static class SmsApprovalRequestTypes
    {
        public const string Campaign = "Campaign";
        public const string DirectMessage = "DirectMessage";
        public const string ReferralInvite = "ReferralInvite";
        public const string ProfessionalCampaignStep = "ProfessionalCampaignStep";

        public static string ToPersian(string? requestType) => requestType switch
        {
            Campaign => "کمپین",
            DirectMessage => "مستقیم",
            ReferralInvite => "پاداش و معرفی",
            ProfessionalCampaignStep => "پیام کمپین حرفه‌ای",
            _ => requestType ?? string.Empty
        };
    }
}
