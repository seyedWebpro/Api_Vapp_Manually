namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// DTO برای پاسخ قالب پیام
    /// </summary>
    public class TemplateResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
        public int? GroupId { get; set; }
        public string? GroupName { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}


