using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Contact
{
    /// <summary>
    /// DTO برای ایجاد مخاطب جدید
    /// از JSON Body استفاده می‌شود
    /// برای آپلود عکس پروفایل از endpoint جداگانه استفاده کنید: POST /api/Contact/{id}/upload-profile-image
    /// </summary>
    public class CreateContactDto
    {
        [Required(ErrorMessage = "شناسه دفترچه الزامی است")]
        public int ContactNotebookId { get; set; }

        [Required(ErrorMessage = "شماره موبایل الزامی است")]
        [RegularExpression(@"^09\d{9}$", ErrorMessage = "فرمت شماره موبایل صحیح نیست (باید با 09 شروع شود و 11 رقم باشد)")]
        [StringLength(20, ErrorMessage = "شماره موبایل نمی‌تواند بیشتر از 20 کاراکتر باشد")]
        public string MobileNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "نام کامل مخاطب الزامی است")]
        [StringLength(200, ErrorMessage = "نام کامل نمی‌تواند بیشتر از 200 کاراکتر باشد")]
        public string FullName { get; set; } = string.Empty;

        [StringLength(200, ErrorMessage = "برند نمی‌تواند بیشتر از 200 کاراکتر باشد")]
        public string? Brand { get; set; }

        /// <summary>
        /// لیست نام تگ‌ها برای ایجاد یا استفاده خودکار
        /// اگر تگ با این نام برای کاربر وجود نداشت، به صورت خودکار ایجاد می‌شود
        /// </summary>
        public List<string>? TagNames { get; set; }

        /// <summary>
        /// لیست مناسبت‌های مخاطب (تولد، ازدواج، وفات و ...)
        /// </summary>
        public List<ContactOccasionDto>? Occasions { get; set; }

        /// <summary>
        /// تاریخ تولد مخاطب (اختیاری)
        /// اگر پر شود، در ContactAdditionalInfo ذخیره می‌شود
        /// </summary>
        public DateTime? DateOfBirth { get; set; }

        public string? CustomFields { get; set; }
    }
}
