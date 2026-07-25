using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.LuckyWheel;
using Api_Vapp.DTOs.Public;

namespace Api_Vapp.Interfaces
{
    public interface ILuckyWheelPublicService
    {
        Task<ApiResponse<LuckyWheelPublicDto>> GetPublicWheelAsync(string slug);

        Task<ApiResponse<RegisterPublicParticipantResponseDto>> RegisterAsync(string slug, RegisterPublicParticipantDto dto);

        Task<ApiResponse<PublicParticipantOtpResponseDto>> VerifyOtpAsync(string slug, VerifyPublicParticipantOtpDto dto);

        Task<ApiResponse<PublicParticipantOtpResponseDto>> ResendOtpAsync(string slug, ResendPublicParticipantOtpDto dto);

        Task<ApiResponse<SpinLuckyWheelPublicResponseDto>> SpinAsync(string slug, SpinLuckyWheelPublicDto dto);
    }
}
