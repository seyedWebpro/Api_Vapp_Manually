namespace Api_Vapp.DTOs.Automation
{
    /// <summary>
    /// DTO برای ذخیره تنظیمات تکمیلی پیام خودکار
    /// </summary>
    public class SaveAutomatedMessageSettingsDto
    {
        public bool PreventDuplicate { get; set; } = true;
        public int DuplicatePreventionHours { get; set; } = 24;
        public bool SendToSpecificTags { get; set; } = false;
        public List<int>? SelectedTagIds { get; set; }
    }
}

