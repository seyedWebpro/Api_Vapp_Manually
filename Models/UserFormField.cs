namespace Api_Vapp.Models
{
    public class UserFormField
    {
        public int Id { get; set; }

        public int UserFormId { get; set; }

        public string FieldKey { get; set; } = string.Empty;

        public string FieldType { get; set; } = "text";

        public string Label { get; set; } = string.Empty;

        public string? Placeholder { get; set; }

        public string? HelpText { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsRequired { get; set; }

        public int DisplayOrder { get; set; }

        /// <summary>
        /// کلید فیلد در قالب مبدا — برای upgrade به مرحله ۳
        /// </summary>
        public string? SourceFieldKey { get; set; }

        public virtual UserForm UserForm { get; set; } = null!;
    }
}
