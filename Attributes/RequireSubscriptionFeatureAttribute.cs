using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Subscription;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

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
            var entitlementService = context.HttpContext.RequestServices
                .GetRequiredService<ISubscriptionEntitlementService>();

            var userIdClaim = context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
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
    }
}
