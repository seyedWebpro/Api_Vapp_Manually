using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace Api_Vapp.Attributes
{
    /// <summary>
    /// محدودیت دسترسی بر اساس امکان اشتراک فعال کاربر.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class RequireSubscriptionFeatureAttribute : Attribute, IAsyncActionFilter
    {
        private readonly string _featureCode;

        public RequireSubscriptionFeatureAttribute(string featureCode)
        {
            _featureCode = featureCode;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var services = context.HttpContext.RequestServices;
            var entitlementService = services.GetRequiredService<ISubscriptionEntitlementService>();
            var configuration = services.GetRequiredService<IConfiguration>();

            var (resolved, userId) = await ResolveUserIdAsync(context, configuration, services);
            if (!resolved)
            {
                context.Result = new ObjectResult(ApiResponse<object>.Unauthorized())
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
                return;
            }

            if (!await entitlementService.HasFeatureAsync(userId, _featureCode))
            {
                context.Result = new ObjectResult(
                    ApiResponse<object>.Forbidden(Constants.SubscriptionMessages.FeatureNotAvailable))
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }

            await next();
        }

        private static async Task<(bool Success, int UserId)> ResolveUserIdAsync(
            ActionExecutingContext context,
            IConfiguration configuration,
            IServiceProvider services)
        {
            var userIdClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var parsed))
                return (true, parsed);

            var disableAuth = configuration.GetValue<bool>("Development:DisableAuth", false);
            if (!disableAuth)
                return (false, 0);

            var userRepository = services.GetRequiredService<IUserRepository>();
            var defaultUser = await userRepository.GetOrCreateDefaultUserAsync();
            return (true, defaultUser.Id);
        }
    }
}
