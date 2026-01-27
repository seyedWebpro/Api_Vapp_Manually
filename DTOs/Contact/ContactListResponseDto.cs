namespace Api_Vapp.DTOs.Contact
{
    /// <summary>
    /// DTO برای لیست مخاطبین با pagination
    /// </summary>
    public class ContactListResponseDto
    {
        public List<ContactResponseDto> Contacts { get; set; } = new List<ContactResponseDto>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
        public int ImportedFileCount { get; set; }
    }
}


