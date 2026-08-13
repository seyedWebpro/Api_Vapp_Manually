namespace Api_Vapp.DTOs.BusinessCard
{
    public class BusinessCardResponseDto
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? LogoUrl { get; set; }

        public string? Slug { get; set; }

        public string? TemplateKey { get; set; }

        public int? TemplateId { get; set; }

        public string Status { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public string? PublicUrl { get; set; }

        public bool SliderEnabled { get; set; }

        public bool DescriptionEnabled { get; set; }

        public bool ServicesEnabled { get; set; }

        public bool MapEnabled { get; set; }

        public bool ContactEnabled { get; set; }

        public bool BankingEnabled { get; set; }

        public string? DescriptionTitle { get; set; }

        public string? DescriptionText { get; set; }

        public double? MapLatitude { get; set; }

        public double? MapLongitude { get; set; }

        public string? MapAddress { get; set; }

        public string? ContactPhone { get; set; }

        public string? ContactEmail { get; set; }

        public string? ContactInstagram { get; set; }

        public string? BankAccountNumber { get; set; }

        public string? BankCardNumber { get; set; }

        public string? BankShebaNumber { get; set; }

        public List<BusinessCardSliderImageDto> SliderImages { get; set; } = new();

        public List<BusinessCardServiceItemDto> ServiceItems { get; set; } = new();

        public List<BusinessCardSocialLinkDto> SocialLinks { get; set; } = new();

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime? PublishedAt { get; set; }

        /// <summary>Pending / Approved / Rejected — تأیید یک‌باره ارسال سریع</summary>
        public string ApprovalStatus { get; set; } = "Pending";

        public string? RejectionReason { get; set; }

        public DateTime? ApprovedAt { get; set; }
    }
}
