using Api_Vapp.DTOs.Auth;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api_Vapp.Helpers
{
    /// <summary>
    /// Extension Methods برای استخراج اطلاعات از ClaimsPrincipal (JWT Token)
    /// </summary>
    public static class ClaimsPrincipalExtensions
    {
        /// <summary>
        /// استخراج اطلاعات کاربر از ClaimsPrincipal (JWT Token)
        /// </summary>
        /// <param name="principal">ClaimsPrincipal که از HttpContext.User دریافت می‌شود</param>
        /// <returns>اطلاعات توکن شامل UserId، PhoneNumber و TokenId</returns>
        public static TokenInfoDto GetTokenInfo(this ClaimsPrincipal? principal)
        {
            if (principal == null)
            {
                return new TokenInfoDto();
            }

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var phoneNumberClaim = principal.FindFirst(ClaimTypes.MobilePhone)?.Value;
            var tokenIdClaim = principal.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;

            int? userId = null;
            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int parsedUserId))
            {
                userId = parsedUserId;
            }

            return new TokenInfoDto
            {
                UserId = userId,
                PhoneNumber = phoneNumberClaim,
                TokenId = tokenIdClaim
            };
        }

        /// <summary>
        /// دریافت شناسه کاربر از ClaimsPrincipal
        /// </summary>
        /// <param name="principal">ClaimsPrincipal که از HttpContext.User دریافت می‌شود</param>
        /// <returns>شناسه کاربر یا null در صورت عدم وجود</returns>
        public static int? GetUserId(this ClaimsPrincipal? principal)
        {
            return principal?.GetTokenInfo().UserId;
        }

        /// <summary>
        /// دریافت شماره تلفن کاربر از ClaimsPrincipal
        /// </summary>
        /// <param name="principal">ClaimsPrincipal که از HttpContext.User دریافت می‌شود</param>
        /// <returns>شماره تلفن کاربر یا null در صورت عدم وجود</returns>
        public static string? GetPhoneNumber(this ClaimsPrincipal? principal)
        {
            return principal?.GetTokenInfo().PhoneNumber;
        }

        /// <summary>
        /// دریافت شناسه یکتای توکن (JTI) از ClaimsPrincipal
        /// </summary>
        /// <param name="principal">ClaimsPrincipal که از HttpContext.User دریافت می‌شود</param>
        /// <returns>شناسه یکتای توکن یا null در صورت عدم وجود</returns>
        public static string? GetTokenId(this ClaimsPrincipal? principal)
        {
            return principal?.GetTokenInfo().TokenId;
        }
    }

    /// <summary>
    /// Extension Methods برای ControllerBase برای دسترسی آسان به اطلاعات توکن
    /// </summary>
    public static class ControllerBaseExtensions
    {
        /// <summary>
        /// استخراج اطلاعات کاربر از JWT Token در Controller
        /// </summary>
        /// <param name="controller">Controller که از ControllerBase ارث‌بری می‌کند</param>
        /// <returns>اطلاعات توکن شامل UserId، PhoneNumber و TokenId</returns>
        public static TokenInfoDto GetTokenInfo(this ControllerBase controller)
        {
            return controller.User.GetTokenInfo();
        }

        /// <summary>
        /// دریافت شناسه کاربر از JWT Token در Controller
        /// </summary>
        /// <param name="controller">Controller که از ControllerBase ارث‌بری می‌کند</param>
        /// <returns>شناسه کاربر یا null در صورت عدم وجود</returns>
        public static int? GetUserId(this ControllerBase controller)
        {
            return controller.User.GetUserId();
        }

        /// <summary>
        /// دریافت شماره تلفن کاربر از JWT Token در Controller
        /// </summary>
        /// <param name="controller">Controller که از ControllerBase ارث‌بری می‌کند</param>
        /// <returns>شماره تلفن کاربر یا null در صورت عدم وجود</returns>
        public static string? GetPhoneNumber(this ControllerBase controller)
        {
            return controller.User.GetPhoneNumber();
        }

        /// <summary>
        /// دریافت شناسه یکتای توکن (JTI) از JWT Token در Controller
        /// </summary>
        /// <param name="controller">Controller که از ControllerBase ارث‌بری می‌کند</param>
        /// <returns>شناسه یکتای توکن یا null در صورت عدم وجود</returns>
        public static string? GetTokenId(this ControllerBase controller)
        {
            return controller.User.GetTokenId();
        }
    }
}

