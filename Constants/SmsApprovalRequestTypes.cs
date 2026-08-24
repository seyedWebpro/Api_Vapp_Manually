namespace Api_Vapp.Constants
{
    public static class SmsApprovalRequestTypes
    {
        public const string Campaign = "Campaign";
        public const string DirectMessage = "DirectMessage";
        public const string ReferralInvite = "ReferralInvite";

        public static string ToPersian(string? requestType) => requestType switch
        {
            Campaign => "کمپین",
            DirectMessage => "مستقیم",
            ReferralInvite => "پاداش و معرفی",
            _ => requestType ?? string.Empty
        };
    }
}
