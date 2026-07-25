using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Public;
using Api_Vapp.Models;

namespace Api_Vapp.Interfaces
{
    public interface IPublicParticipantOtpService
    {
        Task<ApiResponse<PublicParticipantOtpResponseDto>> SendAsync(
            PublicParticipantSession session,
            string purpose);

        Task<ApiResponse<PublicParticipantOtpResponseDto>> VerifyAsync(
            PublicParticipantSession session,
            string otpCode);

        Task<ApiResponse<PublicParticipantOtpResponseDto>> ResendAsync(
            PublicParticipantSession session,
            string purpose);
    }
}
