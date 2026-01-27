namespace Api_Vapp.DTOs.Automation
{
    /// <summary>
    /// DTO برای انواع اتوماسیون (مطابق صفحه New Automated Message)
    /// </summary>
    public class AutomationTypeDto
    {
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }
}


