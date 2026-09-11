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

        /// <summary>true برای افزودن و false برای حذف از انتخاب‌های ارسال سریع.</summary>
        public bool IsSelected { get; set; } = true;
    }

    public class SetQuickSendDefaultTemplatesDto
    {
        [Required(ErrorMessage = "شناسه قالب‌ها الزامی است")]
        [MinLength(1, ErrorMessage = "حداقل یک قالب انتخاب کنید")]
        [MaxLength(3, ErrorMessage = "حداکثر سه قالب قابل انتخاب است")]
        public List<int> TemplateIds { get; set; } = new();
    }
}
