namespace Api_Vapp.Models
{
    /// <summary>
    /// موجودیت‌هایی که محتوای ارسال سریع‌شان یک‌بار توسط ادمین تأیید می‌شود
    /// (مشابه MessageTemplate).
    /// </summary>
    public interface IQuickSendApprovable
    {
        string ApprovalStatus { get; set; }

        DateTime? ApprovedAt { get; set; }

        int? ApprovedByUserId { get; set; }

        string? RejectionReason { get; set; }

        DateTime? UpdatedAt { get; set; }
    }
}
