namespace Api_Vapp.Models
{
    /// <summary>
    /// تعریف نوع پیام خودکار قابل انتخاب در اپ و قابل مدیریت در پنل ادمین
    /// </summary>
    public class AutomationTypeDefinition
    {
        public int Id { get; set; }

        /// <summary>کد پایدار (مثلاً Birthday) — منطق اجرا به این کد وابسته است.</summary>
        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
