using System.Security.Claims;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.AspNetCore.Http;

namespace Api_Vapp.Services.Audit
{
    public sealed class HttpAuditContext : IAuditContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HttpAuditContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string? CorrelationId => ControlledErrorHelper.GetTraceId(_httpContextAccessor.HttpContext);

        public int? ActorUserId => TryParseUserId(_httpContextAccessor.HttpContext?.User);

        public string? IpAddress
        {
            get
            {
                var http = _httpContextAccessor.HttpContext;
                if (http == null)
                    return null;

                var forwarded = http.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(forwarded))
                {
                    var first = forwarded.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(first))
                        return Truncate(first, 45);
                }

                return Truncate(http.Connection.RemoteIpAddress?.ToString(), 45);
            }
        }

        public string? UserAgent
        {
            get
            {
                var ua = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();
                return Truncate(ua, 512);
            }
        }

        public string? RequestPath =>
            Truncate(_httpContextAccessor.HttpContext?.Request.Path.Value, 500);

        public string? HttpMethod =>
            Truncate(_httpContextAccessor.HttpContext?.Request.Method, 16);

        public string Source =>
            _httpContextAccessor.HttpContext == null ? AuditSources.System : AuditSources.Http;

        private static int? TryParseUserId(ClaimsPrincipal? user)
        {
            if (user == null)
                return null;

            var raw = user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue("sub")
                ?? user.FindFirstValue("id");

            return int.TryParse(raw, out var id) && id > 0 ? id : null;
        }

        private static string? Truncate(string? value, int max)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            return value.Length <= max ? value : value[..max];
        }
    }

    public sealed class NullAuditContext : IAuditContext
    {
        public static readonly NullAuditContext Instance = new();

        public string? CorrelationId => null;
        public int? ActorUserId => null;
        public string? IpAddress => null;
        public string? UserAgent => null;
        public string? RequestPath => null;
        public string? HttpMethod => null;
        public string Source => AuditSources.System;
    }
}
