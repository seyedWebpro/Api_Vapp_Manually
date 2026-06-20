using Api_Vapp.DTOs.Common;
using Api_Vapp.Exceptions;
using Api_Vapp.Interfaces;
using Api_Vapp.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// کلاس پایه Controller — متدهای مشترک احراز هویت و validation
    /// </summary>
    public abstract class VappControllerBase : ControllerBase
    {
        protected readonly IConfiguration Configuration;
        protected readonly IUserRepository UserRepository;

        protected VappControllerBase(IConfiguration configuration, IUserRepository userRepository)
        {
            Configuration = configuration;
            UserRepository = userRepository;
        }

        protected List<string> ExtractModelStateErrors() =>
            ErrorTranslator.ExtractModelStateErrors(ModelState);

        protected ActionResult<ApiResponse<T>>? InvalidModelStateResponse<T>(string message = "داده‌های ورودی نامعتبر است")
        {
            if (ModelState.IsValid)
                return null;

            return StatusCode(400, ApiResponse<T>.BadRequest(message, ExtractModelStateErrors(), ErrorCodes.ValidationFailed));
        }

        protected async Task<int> GetCurrentUserIdAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId))
                return userId;

            if (HasBearerAuthorizationHeader())
                throw AppException.Unauthorized(ErrorCodes.TokenInvalid, ControlledErrorHelper.InvalidToken);

            var disableAuth = Configuration.GetValue<bool>("Development:DisableAuth", false);
            if (disableAuth)
            {
                var defaultUser = await UserRepository.GetOrCreateDefaultUserAsync();
                return defaultUser.Id;
            }

            throw AppException.Unauthorized(ErrorCodes.InvalidUserId, ControlledErrorHelper.Unauthorized);
        }

        private bool HasBearerAuthorizationHeader()
        {
            var authorization = Request.Headers.Authorization.ToString();
            return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                && authorization.Length > "Bearer ".Length;
        }
    }
}
