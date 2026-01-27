using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Automation
{
    /// <summary>
    /// DTO برای انتخاب گیرندگان پیام خودکار (مرحله 2 - انتخاب مخاطبین)
    /// </summary>
    public class SelectRecipientsForAutomatedMessageDto
    {
        /// <summary>
        /// اعمال برای همه مخاطبین (از همه دفترچه‌ها)
        /// </summary>
        public bool ApplyToAllContacts { get; set; } = false;

        /// <summary>
        /// شناسه دفترچه تلفن (فقط در صورت ApplyToAllContacts = false)
        /// </summary>
        public int? ContactNotebookId { get; set; }

        /// <summary>
        /// لیست شناسه مخاطبینی که نباید پیام برود (اختیاری)
        /// اگر null باشد، همه مخاطبین انتخاب می‌شوند
        /// اگر ارسال شود، آن مخاطبین از لیست حذف می‌شوند
        /// </summary>
        public List<int>? ExcludedContactIds { get; set; }
    }
}

