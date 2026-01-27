using System.Text.Json.Serialization;

namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// Enum برای نوع ارسال کمپین
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CampaignSendType
    {
        /// <summary>
        /// ارسال فوری
        /// </summary>
        Quick = 1,

        /// <summary>
        /// ارسال زمان‌بندی شده
        /// </summary>
        Scheduled = 2
    }
}



