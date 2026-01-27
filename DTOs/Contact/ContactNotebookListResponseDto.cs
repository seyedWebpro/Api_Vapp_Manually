namespace Api_Vapp.DTOs.Contact
{
    /// <summary>
    /// DTO برای لیست دفترچه‌ها با pagination
    /// </summary>
    public class ContactNotebookListResponseDto
    {
        public List<ContactNotebookResponseDto> Notebooks { get; set; } = new List<ContactNotebookResponseDto>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}


