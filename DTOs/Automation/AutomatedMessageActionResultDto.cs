namespace Api_Vapp.DTOs.Automation
{
    /// <summary>
    /// نتیجه عملیات چرخه عمر پیام خودکار (لغو / حذف / تغییر وضعیت)
    /// </summary>
    public class AutomatedMessageActionResultDto
    {
        public int Id { get; set; }
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
        public string Status { get; set; } = string.Empty;
        public int CancelledCampaignsCount { get; set; }
    }
}
