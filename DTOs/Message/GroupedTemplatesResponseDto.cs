namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// DTO برای هر دسته‌بندی و قالب‌های آن
    /// </summary>
    public class CategoryGroupDto
    {
        /// <summary>
        /// نام دسته‌بندی (مثلاً: "قالب های شخصی سازی شده", "قالب های مناسبتی", "قالب های فروشگاهی")
        /// اگر null باشد، یعنی قالب‌های بدون دسته‌بندی
        /// </summary>
        public string? CategoryName { get; set; }

        /// <summary>
        /// لیست قالب‌های این دسته‌بندی
        /// </summary>
        public List<TemplateResponseDto> Templates { get; set; } = new List<TemplateResponseDto>();

        /// <summary>
        /// تعداد قالب‌های این دسته‌بندی
        /// </summary>
        public int Count => Templates.Count;
    }

    /// <summary>
    /// DTO برای هر گروه قالب و قالب‌های آن
    /// </summary>
    public class TemplateGroupGroupDto
    {
        /// <summary>
        /// شناسه گروه قالب
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// نام گروه قالب
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// لیست قالب‌های این گروه
        /// </summary>
        public List<TemplateResponseDto> Templates { get; set; } = new List<TemplateResponseDto>();

        /// <summary>
        /// تعداد قالب‌های این گروه
        /// </summary>
        public int Count => Templates.Count;
    }

    /// <summary>
    /// DTO برای نمایش فقط اطلاعات گروه‌ها بدون قالب‌ها
    /// </summary>
    public class TemplateGroupSummaryDto
    {
        /// <summary>
        /// شناسه گروه قالب
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// نام گروه قالب
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// تعداد قالب‌های این گروه
        /// </summary>
        public int Count { get; set; }
    }
}

