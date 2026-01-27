namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// DTO برای پاسخ گروه قالب
    /// </summary>
    public class TemplateGroupResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
        public int TemplatesCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}

