using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.BusinessCard
{
    /// <summary>
    /// به‌روزرسانی اطلاعات اصلی کارت — فقط فیلدهای ارسال‌شده تغییر می‌کنند.
    /// </summary>
    public class UpdateBusinessCardInfoDto
    {
        [MaxLength(200, ErrorMessage = "نام کسب‌وکار نمی‌تواند بیشتر از 200 کاراکتر باشد")]
        public string? Title { get; set; }

        [MaxLength(500, ErrorMessage = "آدرس لوگو نمی‌تواند بیشتر از 500 کاراکتر باشد")]
        public string? LogoUrl { get; set; }

        /// <summary>
        /// ارسال رشته خالی برای حذف لوگو
        /// </summary>
        public bool? ClearLogo { get; set; }

        [MaxLength(100, ErrorMessage = "slug نمی‌تواند بیشتر از 100 کاراکتر باشد")]
        public string? Slug { get; set; }
    }
}
