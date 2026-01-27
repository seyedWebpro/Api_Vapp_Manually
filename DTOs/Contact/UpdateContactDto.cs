using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Contact
{
    /// <summary>
    /// DTO برای به‌روزرسانی مخاطب
    /// از JSON Body استفاده می‌شود
    /// </summary>
    public class UpdateContactDto
    {
        [RegularExpression(@"^09\d{9}$", ErrorMessage = "فرمت شماره موبایل صحیح نیست (باید با 09 شروع شود و 11 رقم باشد)")]
        [StringLength(20, ErrorMessage = "شماره موبایل نمی‌تواند بیشتر از 20 کاراکتر باشد")]
        public string? MobileNumber { get; set; }

        [StringLength(200, ErrorMessage = "نام کامل نمی‌تواند بیشتر از 200 کاراکتر باشد")]
        public string? FullName { get; set; }

        [StringLength(200, ErrorMessage = "برند نمی‌تواند بیشتر از 200 کاراکتر باشد")]
        public string? Brand { get; set; }

        public string? Tags { get; set; } // فیلد قدیمی - برای سازگاری

        /// <summary>
        /// لیست نام تگ‌ها برای ایجاد یا استفاده خودکار
        /// اگر ارسال شود، جایگزین تگ‌های قبلی می‌شود
        /// </summary>
        public List<string>? TagNames { get; set; }

        /// <summary>
        /// لیست مناسبت‌های مخاطب (تولد، ازدواج، وفات و ...)
        /// اگر ارسال شود، جایگزین لیست قبلی می‌شود
        /// </summary>
        public List<ContactOccasionDto>? Occasions { get; set; }

        /// <summary>
        /// تاریخ تولد مخاطب (اختیاری)
        /// اگر پر شود، در ContactAdditionalInfo ذخیره می‌شود
        /// اگر null ارسال شود، تاریخ تولد پاک می‌شود
        /// </summary>
        public DateTime? DateOfBirth { get; set; }

        public string? CustomFields { get; set; }
    }
}


