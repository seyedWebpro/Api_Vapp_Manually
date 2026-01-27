using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// DTO برای تنظیم قالب پیش‌فرض کاربر
    /// </summary>
    public class SetDefaultTemplateDto
    {
        /// <summary>
        /// شناسه قالب که باید به عنوان قالب پیش‌فرض تنظیم شود
        /// </summary>
        [Required(ErrorMessage = "شناسه قالب الزامی است")]
        public int TemplateId { get; set; }
    }
}

