namespace Api_Vapp.Interfaces
{
    /// <summary>کانتکست محیطی درخواست برای پر کردن خودکار فیلدهای audit.</summary>
    public interface IAuditContext
    {
        string? CorrelationId { get; }
        int? ActorUserId { get; }
        string? IpAddress { get; }
        string? UserAgent { get; }
        string? RequestPath { get; }
        string? HttpMethod { get; }
        string Source { get; }
    }
}
