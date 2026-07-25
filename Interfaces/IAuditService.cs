using Api_Vapp.Services.Audit;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// سرویس ثبت audit — fail-safe: خطا در نوشتن، business را throw نمی‌کند.
    /// </summary>
    public interface IAuditService
    {
        /// <summary>ثبت یک ردیف audit با اسنپ‌شات before/after.</summary>
        Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default);

        /// <summary>ثبت چند ردیف در یک تراکنش مستقل.</summary>
        Task WriteRangeAsync(IEnumerable<AuditEntry> entries, CancellationToken cancellationToken = default);
    }
}
