using Api_Vapp.Configuration;
using Api_Vapp.DTOs.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Api_Vapp.Attributes
{
    /// <summary>
    /// احراز هویت سرویس‌به‌سرویس با X-API-Key (ربات پایتون → .NET).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class ServiceApiKeyAttribute : Attribute, IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var settings = context.HttpContext.RequestServices
                .GetRequiredService<IOptions<NumberScraperApiSettings>>()
                .Value;

            var provided = context.HttpContext.Request.Headers["X-API-Key"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(settings.ApiKey) ||
                string.IsNullOrWhiteSpace(provided) ||
                !string.Equals(provided.Trim(), settings.ApiKey.Trim(), StringComparison.Ordinal))
            {
                context.Result = new ObjectResult(
                    ApiResponse<object>.Unauthorized("کلید سرویس نامعتبر است", ErrorCodes.Unauthorized))
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
                return;
            }

            await next();
        }
    }
}
