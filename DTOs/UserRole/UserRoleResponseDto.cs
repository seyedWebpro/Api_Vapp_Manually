using Api_Vapp.DTOs.Role;

namespace Api_Vapp.DTOs.UserRole
{
    /// <summary>
    /// DTO برای پاسخ اطلاعات رابطه کاربر-نقش
    /// </summary>
    public class UserRoleResponseDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int RoleId { get; set; }
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // اطلاعات نقش
        public RoleResponseDto? Role { get; set; }
    }
}

