using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.UserForm
{
    /// <summary>
    /// به‌روزرسانی فیلدهای فرم — merge جزئی بر اساس fieldKey.
    /// </summary>
    public class UpdateUserFormFieldsDto
    {
        [Required(ErrorMessage = "حداقل یک فیلد برای به‌روزرسانی الزامی است")]
        [MinLength(1, ErrorMessage = "حداقل یک فیلد برای به‌روزرسانی الزامی است")]
        public List<UpdateUserFormFieldDto> Fields { get; set; } = new();
    }
}
