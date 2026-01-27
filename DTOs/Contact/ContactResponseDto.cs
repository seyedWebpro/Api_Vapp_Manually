using Api_Vapp.DTOs.Message;

namespace Api_Vapp.DTOs.Contact
{
    /// <summary>
    /// DTO برای پاسخ مخاطب
    /// </summary>
    public class ContactResponseDto
    {
        public int Id { get; set; }
        public int ContactNotebookId { get; set; }
        public string ContactNotebookName { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? Brand { get; set; }
        public List<MessageTagResponseDto> ContactTags { get; set; } = new(); // تگ‌های واقعی
        public string? ProfileImagePath { get; set; }
        public string? ProfileImageUrl { get; set; }

        /// <summary>
        /// تاریخ تولد مخاطب از ContactAdditionalInfo
        /// </summary>
        public DateTime? DateOfBirth { get; set; }

        /// <summary>
        /// تاریخ ازدواج مخاطب از ContactAdditionalInfo
        /// </summary>
        public DateTime? MarriageDate { get; set; }

        public List<ContactOccasionDto> Occasions { get; set; } = new();

        public string? CustomFields { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}


