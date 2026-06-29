namespace Api_Vapp.DTOs.Sms
{
    public class SmsDeliveryRecordDto
    {
        public int Id { get; set; }
        public string SourceModule { get; set; } = string.Empty;
        public string SourceModuleLabel { get; set; } = string.Empty;
        public int? SourceEntityId { get; set; }
        public string? SourceEntityLabel { get; set; }
        public string Mobile { get; set; } = string.Empty;
        public long Sid { get; set; }
        public string SendStatus { get; set; } = string.Empty;
        public string DeliveryCategory { get; set; } = string.Empty;
        public string DeliveryCategoryLabel { get; set; } = string.Empty;
        public int? ProviderStatusCode { get; set; }
        public string? ProviderStatusMessage { get; set; }
        public bool IsDeliveryFinal { get; set; }
        public DateTime SentAt { get; set; }
        public DateTime? LastCheckedAt { get; set; }
    }

    public class SmsDeliveryReportListDto
    {
        public List<SmsDeliveryRecordDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }

    public class SmsDeliverySummaryDto
    {
        public int Total { get; set; }
        public int DeliveredToPhone { get; set; }
        public int SentToOperator { get; set; }
        public int NotDelivered { get; set; }
        public int PendingApproval { get; set; }
        public int Rejected { get; set; }
        public int PendingSync { get; set; }
        public int SendFailed { get; set; }
    }

    public class SmsDeliveryReportFilterDto
    {
        public string? SourceModule { get; set; }
        public int? SourceEntityId { get; set; }
        public string? DeliveryCategory { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
