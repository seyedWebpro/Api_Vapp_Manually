using Api_Vapp.DTOs.Auth;
using Api_Vapp.Models;

namespace Api_Vapp.Interfaces
{
    public interface IRefreshTokenService
    {
        Task<RefreshToken> CreateRefreshTokenAsync(int userId, DateTime? originalExpiresAt = null);
        Task<RefreshToken?> GetRefreshTokenAsync(string token);
        Task RevokeRefreshTokenAsync(string token);
        Task RevokeAllUserTokensAsync(int userId);
        Task<bool> IsRefreshTokenValidAsync(string token);

        /// <summary>
        /// Rotation اتمیک + grace reuse برای درخواست‌های همزمان با همان refresh token.
        /// </summary>
        Task<RefreshTokenRotationResult> RotateOrReuseAsync(string presentedToken);
    }
}
