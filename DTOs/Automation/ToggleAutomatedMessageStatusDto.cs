using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Automation
{
    /// <summary>
    /// DTO تغییر وضعیت فعال/غیرفعال پیام خودکار
    /// </summary>
    public class ToggleAutomatedMessageStatusDto
    {
        [Required(ErrorMessage = "وضعیت فعال بودن الزامی است")]
        public bool IsActive { get; set; }
    }
}
