namespace Api_Vapp.DTOs.Role
{
    /// <summary>
    /// DTO برای پاسخ لیست نقش‌ها با pagination
    /// </summary>
    public class RoleListResponseDto
    {
        public List<RoleResponseDto> Roles { get; set; } = new List<RoleResponseDto>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}

