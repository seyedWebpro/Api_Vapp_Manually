using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.LuckyWheel
{
    public class UpdateLuckyWheelItemDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "شناسه آیتم نامعتبر است")]
        public int? Id { get; set; }

        [Required(ErrorMessage = "نام جایزه الزامی است")]
        [MaxLength(200, ErrorMessage = "نام جایزه نمی‌تواند بیشتر از 200 کاراکتر باشد")]
        public string Name { get; set; } = string.Empty;

        [Range(0.01, 100, ErrorMessage = "درصد شانس باید بین 0.01 تا 100 باشد")]
        public decimal Probability { get; set; }

        [Range(1, 20, ErrorMessage = "ترتیب نمایش باید بین 1 تا 20 باشد")]
        public int DisplayOrder { get; set; }
    }
}
