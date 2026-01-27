namespace Api_Vapp.DTOs.UserRole
{
    /// <summary>
    /// DTO برای پاسخ لیست روابط کاربر-نقش با pagination
    /// </summary>
    public class UserRoleListResponseDto
    {
        public List<UserRoleResponseDto> UserRoles { get; set; } = new List<UserRoleResponseDto>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}

