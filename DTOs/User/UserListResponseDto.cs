namespace Api_Vapp.DTOs.User
{
    /// <summary>
    /// DTO برای لیست کاربران با pagination
    /// </summary>
    public class UserListResponseDto
    {
        public List<UserResponseDto> Users { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}

