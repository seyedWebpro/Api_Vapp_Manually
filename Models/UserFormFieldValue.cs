namespace Api_Vapp.Models
{
    /// <summary>
    /// مقدار یک فیلد در ارسال عمومی فرم
    /// </summary>
    public class UserFormFieldValue
    {
        public int Id { get; set; }

        public int UserFormSubmissionId { get; set; }

        public string FieldKey { get; set; } = string.Empty;

        public string? Value { get; set; }

        public virtual UserFormSubmission Submission { get; set; } = null!;
    }
}
