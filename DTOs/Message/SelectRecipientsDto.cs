using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Message
{
    /// <summary>
    /// DTO برای انتخاب گیرندگان (مطابق صفحه New SMS Message)
    /// </summary>
    public class SelectRecipientsDto
    {
        // شناسه پیام (الزامی - باید قبل از انتخاب گیرندگان ایجاد شده باشد)
        [Required(ErrorMessage = "شناسه پیام الزامی است")]
        public int MessageId { get; set; }

        // نوع انتخاب: MessageSelectionTypes (Notebook, Tag, ContactIds, Individual)
        [Required(ErrorMessage = "نوع انتخاب گیرندگان الزامی است")]
        public string SelectionType { get; set; } = string.Empty;

        // برای انتخاب از دفترچه (فقط در حالت Notebook)
        public List<int>? ContactNotebookIds { get; set; }

        // برای انتخاب بر اساس تگ (فقط در حالت Tag)
        public List<int>? TagIds { get; set; }

        // در حالت Notebook: لیست شناسه مخاطبینی که نباید پیام برود (اختیاری — exclude)
        // در حالت ContactIds: لیست شناسه مخاطبین انتخاب‌شده از UI (الزامی — include)
        public List<int>? ContactIds { get; set; }

        // برای انتخاب تکی با شماره موبایل مستقیم (فقط در حالت Individual - الزامی)
        public List<string>? MobileNumbers { get; set; }

        // برای انتخاب تکی با نام کامل (در حالت Individual - الزامی)
        // این لیست باید با MobileNumbers هم‌اندازه باشد (هر ایندکس مربوط به همان ایندکس در MobileNumbers است)
        public List<string>? FullNames { get; set; }
    }
}


