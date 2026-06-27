namespace Api_Vapp.Utilities
{
    public class FormBuilderOptions
    {
        public const string SectionName = "FormBuilder";

        /// <summary>
        /// پایه URL عمومی فرم — مثال: https://app.com/form
        /// </summary>
        public string PublicBaseUrl { get; set; } = string.Empty;
    }
}
