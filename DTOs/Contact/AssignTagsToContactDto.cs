using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Contact
{
    /// <summary>
    /// DTO برای اختصاص تگ‌ها به مخاطب
    /// </summary>
    public class AssignTagsToContactDto
    {
        [Required(ErrorMessage = "لیست شناسه تگ‌ها الزامی است")]
        [MinLength(1, ErrorMessage = "حداقل باید یک تگ انتخاب شود")]
        public List<int> TagIds { get; set; } = new List<int>();
    }
}

