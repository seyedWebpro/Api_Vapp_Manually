using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.LuckyWheel
{
    /// <summary>
    /// به‌روزرسانی جزئی گردونه — فقط فیلدهای ارسال‌شده تغییر می‌کنند.
    /// </summary>
    public class UpdateLuckyWheelDto
    {
        [MaxLength(200, ErrorMessage = "عنوان گردونه نمی‌تواند بیشتر از 200 کاراکتر باشد")]
        public string? Title { get; set; }

        [MaxLength(2000, ErrorMessage = "توضیحات نمی‌تواند بیشتر از 2000 کاراکتر باشد")]
        public string? Description { get; set; }

        [MaxLength(100, ErrorMessage = "slug نمی‌تواند بیشتر از 100 کاراکتر باشد")]
        public string? Slug { get; set; }

        public bool? SaveToPhonebook { get; set; }

        public List<int>? NotebookIds { get; set; }

        public List<LuckyWheelItemDto>? Items { get; set; }
    }
}
