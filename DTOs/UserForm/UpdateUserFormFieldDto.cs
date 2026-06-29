using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.UserForm
{
    /// <summary>
    /// به‌روزرسانی جزئی یک فیلد — فقط propertyهای ارسال‌شده تغییر می‌کنند.
    /// </summary>
    public class UpdateUserFormFieldDto
    {
        [Required(ErrorMessage = "fieldKey الزامی است")]
        [MaxLength(100, ErrorMessage = "fieldKey نمی‌تواند بیشتر از 100 کاراکتر باشد")]
        public string FieldKey { get; set; } = string.Empty;

        [MaxLength(50, ErrorMessage = "نوع فیلد نمی‌تواند بیشتر از 50 کاراکتر باشد")]
        public string? FieldType { get; set; }

        [MaxLength(200, ErrorMessage = "عنوان فیلد نمی‌تواند بیشتر از 200 کاراکتر باشد")]
        public string? Label { get; set; }

        [MaxLength(300, ErrorMessage = "متن پیش‌فرض نمی‌تواند بیشتر از 300 کاراکتر باشد")]
        public string? Placeholder { get; set; }

        [MaxLength(1000, ErrorMessage = "متن راهنما نمی‌تواند بیشتر از 1000 کاراکتر باشد")]
        public string? HelpText { get; set; }

        public bool? IsActive { get; set; }

        public bool? IsRequired { get; set; }

        public int? DisplayOrder { get; set; }

        [MaxLength(100, ErrorMessage = "sourceFieldKey نمی‌تواند بیشتر از 100 کاراکتر باشد")]
        public string? SourceFieldKey { get; set; }
    }
}
