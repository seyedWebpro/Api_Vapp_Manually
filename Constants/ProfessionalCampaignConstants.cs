namespace Api_Vapp.Constants
{
    public static class ProfessionalCampaignTargetTypes
    {
        public const string Notebooks = "Notebooks";
        public const string Tags = "Tags";
    }

    public static class ProfessionalCampaignStatuses
    {
        public const string PendingApproval = "PendingApproval";
        public const string Ready = "Ready";
        public const string Active = "Active";
        public const string Paused = "Paused";
        public const string Completed = "Completed";
        public const string Rejected = "Rejected";
        public const string Cancelled = "Cancelled";
    }

    public static class ProfessionalCampaignStepStatuses
    {
        public const string PendingApproval = "PendingApproval";
        public const string Pending = "Pending";
        public const string Processing = "Processing";
        public const string Sent = "Sent";
        public const string Failed = "Failed";
        public const string Rejected = "Rejected";
    }
}
