using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Admin
{
    /// <summary>
    /// درخواست ارسال Push به‌روزرسانی اپ به کاربران (دسته Updates)
    /// </summary>
    public class BroadcastAppUpdateDto
    {
        /// <summary>
        /// نسخهٔ منتشرشده — مثال: 2.4.0
        /// </summary>
        [Required(ErrorMessage = "نسخه الزامی است")]
        [MaxLength(32, ErrorMessage = "نسخه حداکثر ۳۲ کاراکتر است")]
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// توضیح اختیاری برای بدنهٔ نوتیفیکیشن
        /// </summary>
        [MaxLength(280, ErrorMessage = "توضیحات حداکثر ۲۸۰ کاراکتر است")]
        public string? Notes { get; set; }
    }
}
