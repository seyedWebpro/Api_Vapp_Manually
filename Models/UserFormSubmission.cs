namespace Api_Vapp.Models
{
    /// <summary>
    /// پاسخ ارسال‌شده توسط بازدیدکننده در صفحه عمومی فرم
    /// </summary>
    public class UserFormSubmission
    {
        public int Id { get; set; }

        public int UserFormId { get; set; }

        public string ParticipantFullName { get; set; } = string.Empty;

        public string ParticipantMobile { get; set; } = string.Empty;

        /// <summary>
        /// مخاطب ذخیره‌شده در دفترچه — در صورت فعال بودن SaveToPhonebook
        /// </summary>
        public int? ContactId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual UserForm UserForm { get; set; } = null!;

        public virtual Contact? Contact { get; set; }

        public virtual ICollection<UserFormFieldValue> FieldValues { get; set; } = new List<UserFormFieldValue>();
    }
}
