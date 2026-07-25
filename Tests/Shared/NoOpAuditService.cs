using Api_Vapp.Interfaces;
using Api_Vapp.Services.Audit;

namespace Api_Vapp.Tests.Shared;

/// <summary>پیاده‌سازی no-op برای تست‌های سرویس که نیازی به بررسی audit ندارند.</summary>
internal sealed class NoOpAuditService : IAuditService
{
    public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task WriteRangeAsync(IEnumerable<AuditEntry> entries, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
