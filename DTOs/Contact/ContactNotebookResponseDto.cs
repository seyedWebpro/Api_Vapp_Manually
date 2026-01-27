namespace Api_Vapp.DTOs.Contact
{
    /// <summary>
    /// DTO برای پاسخ دفترچه
    /// </summary>
    public class ContactNotebookResponseDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public string? IconUrl { get; set; }
        public bool IsActive { get; set; }
        public int ContactsCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}


