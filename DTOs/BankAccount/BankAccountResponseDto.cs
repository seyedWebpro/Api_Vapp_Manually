namespace Api_Vapp.DTOs.BankAccount
{
    /// <summary>
    /// DTO نمایش شماره حساب
    /// </summary>
    public class BankAccountResponseDto
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? AccountNumber { get; set; }

        public string? CardNumber { get; set; }

        public string? ShebaNumber { get; set; }

        public bool IsActive { get; set; }

        public bool IsDefault { get; set; }

        public DateTime CreatedAt { get; set; }

        /// <summary>Pending / Approved / Rejected — تأیید یک‌باره ارسال سریع</summary>
        public string ApprovalStatus { get; set; } = "Pending";

        public string? RejectionReason { get; set; }

        public DateTime? ApprovedAt { get; set; }
    }
}
