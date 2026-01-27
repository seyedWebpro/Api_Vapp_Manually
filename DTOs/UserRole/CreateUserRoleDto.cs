using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.UserRole
{
    /// <summary>
    /// DTO برای ایجاد رابطه کاربر-نقش
    /// </summary>
    public class CreateUserRoleDto
    {
        [Required(ErrorMessage = "شناسه کاربر الزامی است")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "شناسه نقش الزامی است")]
        public int RoleId { get; set; }

        public bool IsActive { get; set; } = true;
    }
}

