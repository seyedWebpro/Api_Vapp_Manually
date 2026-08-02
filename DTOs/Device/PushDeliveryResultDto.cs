namespace Api_Vapp.DTOs.Device
{
    /// <summary>
    /// نتیجه ارسال push برای مانیتورینگ و پاسخ API
    /// </summary>
    public class PushDeliveryResultDto
    {
        public bool FirebaseReady { get; set; }
        public bool PreferenceAllowed { get; set; } = true;
        public bool SkippedByPreference { get; set; }
        public string? Category { get; set; }
        public int DeviceCount { get; set; }
        public int SentCount { get; set; }
        public int FailedCount { get; set; }
    }
}
