namespace Api_Vapp.DTOs.Automation
{
    /// <summary>
    /// DTO برای محاسبه خلاصه پیام خودکار (مرحله 5 - خلاصه و تنظیمات)
    /// </summary>
    public class CalculateAutomatedMessageSummaryDto
    {
        public bool PreventDuplicate { get; set; } = true;
        public int DuplicatePreventionHours { get; set; } = 24;
        public bool SendToSpecificTags { get; set; } = false;
        public List<int>? SelectedTagIds { get; set; }
    }
}

