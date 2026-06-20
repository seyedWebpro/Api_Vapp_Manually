using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Api_Vapp.Utilities;
using System.Security.Claims;
using System.Text.Json;

namespace Api_Vapp.Middleware
{
    /// <summary>
    /// When a Bearer token is sent, enforce a valid authenticated session.
    /// Prevents Development:DisableAuth from silently falling back to the default user
    /// for deactivated or invalid tokens.
    /// </summary>
    public class BearerAuthenticationEnforcementMiddleware
    {
        private static readonly string[] AnonymousAuthPathPrefixes =
        [
            "/api/auth/login",
            "/api/auth/verify-login",
            "/api/auth/resend-login-otp",
            "/api/auth/admin/login",
            "/api/auth/admin/verify-login",
            "/api/auth/admin/resend-login-otp",
            "/api/auth/register",
            "/api/auth/verify-registration",
            "/api/auth/resend-registration-otp",
            "/api/auth/forgot-password",
            "/api/auth/reset-password",
            "/api/auth/resend-forgot-password-otp",
            "/api/auth/refresh-token",
        ];

        private readonly RequestDelegate _next;

        public BearerAuthenticationEnforcementMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IUserRepository userRepository)
        {
            if (!HasBearerToken(context.Request) || IsAnonymousAuthPath(context.Request.Path))
            {
                await _next(context);
                return;
            }

            if (context.User?.Identity?.IsAuthenticated != true)
            {
                var isInactive = context.Items.ContainsKey("InactiveUser");
                await WriteJsonResponseAsync(
                    context,
                    isInactive ? 403 : 401,
                    isInactive
                        ? ApiResponse<object>.Forbidden(ControlledErrorHelper.InactiveUserAccount)
                        : ApiResponse<object>.Unauthorized(ControlledErrorHelper.InvalidToken));
                return;
            }

            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out var userId))
            {
                var user = await userRepository.GetByIdAsync(userId);
                if (user == null || user.IsDeleted || !user.IsActive)
                {
                    await WriteJsonResponseAsync(
                        context,
                        403,
                        ApiResponse<object>.Forbidden(ControlledErrorHelper.InactiveUserAccount));
                    return;
                }
            }

            await _next(context);
        }

        private static bool HasBearerToken(HttpRequest request)
        {
            var authorization = request.Headers.Authorization.ToString();
            return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                && authorization.Length > "Bearer ".Length;
        }

        private static bool IsAnonymousAuthPath(PathString path)
        {
            var normalized = path.Value?.TrimEnd('/').ToLowerInvariant() ?? string.Empty;
            return AnonymousAuthPathPrefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.Ordinal));
        }

        private static Task WriteJsonResponseAsync(HttpContext context, int statusCode, ApiResponse<object> payload)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            // Ensure CORS headers exist when this middleware short-circuits the pipeline.
            var origin = context.Request.Headers.Origin.ToString();
            if (!string.IsNullOrEmpty(origin))
            {
                context.Response.Headers.Append("Access-Control-Allow-Origin", origin);
                context.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
            }
            else
            {
                context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
            }

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            return context.Response.WriteAsync(json);
        }
    }
}
