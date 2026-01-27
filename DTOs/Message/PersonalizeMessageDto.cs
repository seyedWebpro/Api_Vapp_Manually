namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// DTO برای شخصی‌سازی پیام
    /// </summary>
    public class PersonalizeMessageDto
    {
        public Dictionary<string, string> PlaceholderValues { get; set; } = new Dictionary<string, string>();
        
        /// <summary>
        /// آیا متن شخصی‌سازی شده در پیام ذخیره شود؟ (پیش‌فرض: true)
        /// </summary>
        public bool SaveToMessage { get; set; } = true;
    }
}


