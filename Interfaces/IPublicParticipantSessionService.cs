using Api_Vapp.DTOs.Common;
using Api_Vapp.Models;

namespace Api_Vapp.Interfaces
{
    public class PublicParticipantSessionTokenResult
    {
        public PublicParticipantSession Session { get; set; } = null!;

        public string AccessToken { get; set; } = string.Empty;
    }

    public interface IPublicParticipantSessionService
    {
        Task<ApiResponse<PublicParticipantSessionTokenResult>> CreateOrRefreshAsync(
            PublicParticipantResourceType resourceType,
            int resourceId,
            string fullName,
            string mobile);

        Task<ApiResponse<PublicParticipantSession>> ValidateActiveAsync(
            string accessToken,
            PublicParticipantResourceType resourceType,
            int resourceId,
            bool requirePhoneVerified = false);

        Task MarkPhoneVerifiedAsync(PublicParticipantSession session);

        /// <summary>
        /// مصرف اتمیک جلسه — فقط اگر هنوز مصرف نشده باشد
        /// </summary>
        Task<bool> TryMarkConsumedAsync(int sessionId);
    }
}
