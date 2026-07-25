using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Public;
using Api_Vapp.DTOs.UserForm;

namespace Api_Vapp.Interfaces
{
    public interface IUserFormPublicService
    {
        Task<ApiResponse<FormPublicDto>> GetPublicFormAsync(string slug);

        Task<ApiResponse<RegisterPublicParticipantResponseDto>> RegisterAsync(string slug, RegisterPublicParticipantDto dto);

        Task<ApiResponse<PublicParticipantOtpResponseDto>> VerifyOtpAsync(string slug, VerifyPublicParticipantOtpDto dto);

        Task<ApiResponse<PublicParticipantOtpResponseDto>> ResendOtpAsync(string slug, ResendPublicParticipantOtpDto dto);

        Task<ApiResponse<SubmitFormPublicResponseDto>> SubmitFormAsync(string slug, SubmitFormPublicDto dto);
    }
}
