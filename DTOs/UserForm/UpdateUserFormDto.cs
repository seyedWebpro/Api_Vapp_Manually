using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.UserForm
{
    /// <summary>
    /// به‌روزرسانی جزئی فرم — فقط فیلدهای ارسال‌شده تغییر می‌کنند.
    /// </summary>
    public class UpdateUserFormDto
    {
        [MaxLength(200, ErrorMessage = "عنوان فرم نمی‌تواند بیشتر از 200 کاراکتر باشد")]
        public string? Title { get; set; }

        [MaxLength(2000, ErrorMessage = "توضیحات نمی‌تواند بیشتر از 2000 کاراکتر باشد")]
        public string? Description { get; set; }

        [MaxLength(100, ErrorMessage = "slug نمی‌تواند بیشتر از 100 کاراکتر باشد")]
        public string? Slug { get; set; }

        public bool? SaveToPhonebook { get; set; }

        /// <summary>
        /// فقط برای فرم‌های منتشرشده — فعال/غیرفعال کردن لینک عمومی
        /// </summary>
        public bool? IsActive { get; set; }

        public List<int>? NotebookIds { get; set; }

        public List<UpdateUserFormFieldDto>? Fields { get; set; }
    }
}
