namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// DTO برای گزارش امروز (مطابق صفحه Messages)
    /// </summary>
    public class TodayReportDto
    {
        public int SentTodayCount { get; set; }
        public int ScheduledTomorrowCount { get; set; }
    }
}


