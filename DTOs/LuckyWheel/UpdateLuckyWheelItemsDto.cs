using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.LuckyWheel
{
    public class UpdateLuckyWheelItemsDto
    {
        public List<UpdateLuckyWheelItemDto> Items { get; set; } = new();
    }
}
