using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services.Audit
{
    /// <summary>
    /// ثبت audit در DbContext مستقل از business (IDbContextFactory)
    /// تا fail نوشتن audit، عملیات اصلی را خراب نکند و برعکس.
    /// </summary>
    public sealed class AuditService : IAuditService
    {
        private readonly IDbContextFactory<Api_Context> _dbFactory;
        private readonly IAuditContext _auditContext;
        private readonly ILogger<AuditService> _logger;

        public AuditService(
            IDbContextFactory<Api_Context> dbFactory,
            IAuditContext auditContext,
            ILogger<AuditService> logger)
        {
            _dbFactory = dbFactory;
            _auditContext = auditContext;
            _logger = logger;
        }

        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entry);
            return WriteRangeAsync([entry], cancellationToken);
        }

        public async Task WriteRangeAsync(IEnumerable<AuditEntry> entries, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entries);

            // یک‌بار کانتکست محیطی را بخوان — بدون دسترسی تکراری به HttpContext
            var ambient = CaptureAmbient();

            var list = new List<AdminAuditLog>(4);
            foreach (var entry in entries)
            {
                if (entry == null
                    || string.IsNullOrWhiteSpace(entry.Category)
                    || string.IsNullOrWhiteSpace(entry.Action)
                    || string.IsNullOrWhiteSpace(entry.EntityType))
                    continue;

                list.Add(Map(entry, ambient));
            }

            if (list.Count == 0)
                return;

            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
                db.AdminAuditLogs.AddRange(list);
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Audit write failed. Count={Count} FirstAction={Action} EntityType={EntityType} EntityId={EntityId}",
                    list.Count,
                    list[0].Action,
                    list[0].EntityType,
                    list[0].EntityId);
            }
        }

        private AmbientAuditContext CaptureAmbient() => new(
            CorrelationId: Truncate(_auditContext.CorrelationId, 64),
            ActorUserId: _auditContext.ActorUserId,
            IpAddress: Truncate(_auditContext.IpAddress, 45),
            UserAgent: Truncate(_auditContext.UserAgent, 512),
            RequestPath: Truncate(_auditContext.RequestPath, 500),
            HttpMethod: Truncate(_auditContext.HttpMethod, 16),
            Source: string.IsNullOrWhiteSpace(_auditContext.Source) ? AuditSources.System : _auditContext.Source);

        private static AdminAuditLog Map(AuditEntry entry, AmbientAuditContext ambient)
        {
            return new AdminAuditLog
            {
                Category = entry.Category.Trim(),
                Action = entry.Action.Trim(),
                EntityType = entry.EntityType.Trim(),
                EntityId = Truncate(entry.EntityId, 64),
                ActorUserId = entry.ActorUserId ?? ambient.ActorUserId,
                TargetUserId = entry.TargetUserId,
                OldValue = AuditJson.Serialize(entry.Before),
                NewValue = AuditJson.Serialize(entry.After),
                Metadata = AuditJson.Serialize(entry.Metadata),
                CorrelationId = Truncate(entry.CorrelationId, 64) ?? ambient.CorrelationId,
                IpAddress = Truncate(entry.IpAddress, 45) ?? ambient.IpAddress,
                UserAgent = Truncate(entry.UserAgent, 512) ?? ambient.UserAgent,
                RequestPath = Truncate(entry.RequestPath, 500) ?? ambient.RequestPath,
                HttpMethod = Truncate(entry.HttpMethod, 16) ?? ambient.HttpMethod,
                Source = string.IsNullOrWhiteSpace(entry.Source) ? ambient.Source : entry.Source.Trim(),
                Succeeded = entry.Succeeded,
                ErrorMessage = Truncate(entry.ErrorMessage, 1000),
                CreatedAt = DateTime.UtcNow
            };
        }

        private static string? Truncate(string? value, int max)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            var trimmed = value.Trim();
            return trimmed.Length <= max ? trimmed : trimmed[..max];
        }

        private readonly record struct AmbientAuditContext(
            string? CorrelationId,
            int? ActorUserId,
            string? IpAddress,
            string? UserAgent,
            string? RequestPath,
            string? HttpMethod,
            string Source);
    }
}
