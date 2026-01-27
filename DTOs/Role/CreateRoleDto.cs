using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Role
{
    /// <summary>
    /// DTO برای ایجاد نقش جدید
    /// </summary>
    public class CreateRoleDto
    {
        [Required(ErrorMessage = "نام نقش الزامی است")]
        [StringLength(100, ErrorMessage = "نام نقش نمی‌تواند بیشتر از 100 کاراکتر باشد")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "نام نمایشی نقش الزامی است")]
        [StringLength(200, ErrorMessage = "نام نمایشی نقش نمی‌تواند بیشتر از 200 کاراکتر باشد")]
        public string DisplayName { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "توضیحات نمی‌تواند بیشتر از 500 کاراکتر باشد")]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
    }
}

