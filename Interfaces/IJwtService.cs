using Api_Vapp.DTOs.Auth;
using Api_Vapp.Models;
using System.Security.Claims;

namespace Api_Vapp.Interfaces
{
    public interface IJwtService
    {
        string GenerateAccessToken(User user, IEnumerable<string>? roleNames = null);
        string GenerateRefreshToken();
        ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
        
        /// <summary>
        /// استخراج اطلاعات کاربر از ClaimsPrincipal (JWT Token)
        /// </summary>
        /// <param name="principal">ClaimsPrincipal که از HttpContext.User دریافت می‌شود</param>
        /// <returns>اطلاعات توکن شامل UserId، PhoneNumber و TokenId</returns>
        TokenInfoDto GetTokenInfo(ClaimsPrincipal? principal);
        
        /// <summary>
        /// دریافت زمان انقضای Access Token به دقیقه
        /// </summary>
        /// <returns>زمان انقضای Access Token به دقیقه</returns>
        int GetAccessTokenExpirationMinutes();
    }
}



