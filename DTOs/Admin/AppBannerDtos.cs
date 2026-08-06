namespace Api_Vapp.DTOs.Admin
{
    public class AppBannerResponseDto
    {
        public int Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public string? LinkUrl { get; set; }
        public string LinkType { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public bool IsSystemManaged { get; set; }
        public bool CanDelete { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>بدنه به‌روزرسانی بنر — فقط فیلدهای متنی/وضعیت (بدون فایل).</summary>
    public class UpdateAppBannerDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        /// <summary>none | app_route | external_url</summary>
        public string? LinkType { get; set; } = "none";
        public string? LinkUrl { get; set; }
        public int? SortOrder { get; set; }
        public bool? IsActive { get; set; } = true;
        public bool? ClearImage { get; set; }
    }
}
