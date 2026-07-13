using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.LuckyWheel;

namespace Api_Vapp.Interfaces
{
    public interface ILuckyWheelPublicService
    {
        Task<ApiResponse<LuckyWheelPublicDto>> GetPublicWheelAsync(string slug);

        Task<ApiResponse<SpinLuckyWheelPublicResponseDto>> SpinAsync(string slug, SpinLuckyWheelPublicDto dto);
    }
}
