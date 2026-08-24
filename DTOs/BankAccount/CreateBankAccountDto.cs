using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.BankAccount
{
    /// <summary>
    /// DTO ایجاد شماره حساب برای ارسال سریع
    /// </summary>
    public class CreateBankAccountDto
    {
        [Required(ErrorMessage = "عنوان الزامی است")]
        [MaxLength(100, ErrorMessage = "عنوان نمی‌تواند بیشتر از 100 کاراکتر باشد")]
        public string Title { get; set; } = string.Empty;

        [MaxLength(30, ErrorMessage = "شماره حساب نمی‌تواند بیشتر از 30 کاراکتر باشد")]
        public string? AccountNumber { get; set; }

        /// <summary>۱۶ رقم؛ جداکننده خط تیره/فاصله مجاز است</summary>
        [MaxLength(19, ErrorMessage = "شماره کارت نامعتبر است")]
        public string? CardNumber { get; set; }

        /// <summary>IR + ۲۴ رقم؛ فاصله مجاز است (قبل از نرمال‌سازی تا حدود ۴۰ کاراکتر)</summary>
        [MaxLength(40, ErrorMessage = "شماره شبا نامعتبر است")]
        public string? ShebaNumber { get; set; }

        /// <summary>اگر true باشد، به‌عنوان پیش‌فرض تنظیم می‌شود</summary>
        public bool? IsDefault { get; set; }
    }
}
