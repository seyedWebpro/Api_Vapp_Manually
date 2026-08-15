using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Api_Vapp.Utilities;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;

namespace Api_Vapp.Middleware
{
    /// <summary>
    /// When a Bearer token is sent, enforce a valid authenticated session.
    /// Prevents Development:DisableAuth from silently falling back to the default user
    /// for deactivated or invalid tokens.
    /// Also samples 401/403 denials to server logs for support (AUTH_DENY).
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

        /// <summary>شمارنده ساده برای نمونه‌برداری لاگ (هر دقیقه ریست می‌شود).</summary>
        private static readonly ConcurrentDictionary<string, long> DenyCounters = new();
        private static long _windowMinute = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute;

        private readonly RequestDelegate _next;
        private readonly ILogger<BearerAuthenticationEnforcementMiddleware> _logger;

        public BearerAuthenticationEnforcementMiddleware(
            RequestDelegate next,
            ILogger<BearerAuthenticationEnforcementMiddleware> logger)
        {
            _next = next;
            _logger = logger;
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
                var status = isInactive ? 403 : 401;
                var reason = isInactive ? "InactiveUser" : "InvalidOrUnauthenticatedToken";
                MaybeLogAuthDeny(context, status, reason);
                await WriteJsonResponseAsync(
                    context,
                    status,
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
                    MaybeLogAuthDeny(context, 403, "UserMissingOrInactive", userId);
                    await WriteJsonResponseAsync(
                        context,
                        403,
                        ApiResponse<object>.Forbidden(ControlledErrorHelper.InactiveUserAccount));
                    return;
                }
            }

            await _next(context);
        }

        private void MaybeLogAuthDeny(HttpContext context, int statusCode, string reason, int? userId = null)
        {
            var minute = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute;
            var prev = Interlocked.Read(ref _windowMinute);
            if (minute != prev)
            {
                Interlocked.Exchange(ref _windowMinute, minute);
                DenyCounters.Clear();
            }

            var key = $"{statusCode}:{reason}";
            var count = DenyCounters.AddOrUpdate(key, 1, (_, c) => c + 1);

            // ۲۰ تای اول هر دقیقه + هر ۵۰م بعد از آن
            if (count > 20 && count % 50 != 0)
                return;

            var traceId = ControlledErrorHelper.GetTraceId(context);
            _logger.LogWarning(
                "AUTH_DENY status={Status} reason={Reason} path={Path} method={Method} userId={UserId} ip={Ip} sample={Sample} TraceId={TraceId}",
                statusCode,
                reason,
                context.Request.Path.Value,
                context.Request.Method,
                userId,
                context.Connection.RemoteIpAddress?.ToString(),
                count,
                traceId);
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
            payload.TraceId ??= ControlledErrorHelper.GetTraceId(context);

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
