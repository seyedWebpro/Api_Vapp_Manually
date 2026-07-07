using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.LuckyWheel
{
    public class AddLuckyWheelItemsDto
    {
        [Required(ErrorMessage = "لیست آیتم‌ها الزامی است")]
        [MinLength(1, ErrorMessage = "حداقل یک آیتم برای افزودن لازم است")]
        public List<LuckyWheelItemDto> Items { get; set; } = new();
    }
}
