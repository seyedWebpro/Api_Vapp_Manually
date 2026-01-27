using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Role
{
    /// <summary>
    /// DTO برای به‌روزرسانی نقش
    /// تمام فیلدها اختیاری هستند - اگر null یا empty باشند، مقدار قبلی تغییر نمی‌کند
    /// </summary>
    public class UpdateRoleDto
    {
        [StringLength(100, ErrorMessage = "نام نقش نمی‌تواند بیشتر از 100 کاراکتر باشد")]
        public string? Name { get; set; }

        [StringLength(200, ErrorMessage = "نام نمایشی نقش نمی‌تواند بیشتر از 200 کاراکتر باشد")]
        public string? DisplayName { get; set; }

        [StringLength(500, ErrorMessage = "توضیحات نمی‌تواند بیشتر از 500 کاراکتر باشد")]
        public string? Description { get; set; }

        public bool? IsActive { get; set; }
    }
}

