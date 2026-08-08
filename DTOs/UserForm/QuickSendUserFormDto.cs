using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.UserForm
{
    /// <summary>
    /// DTO برای ارسال سریع لینک فرم به یک مخاطب
    /// </summary>
    public class QuickSendUserFormDto
    {
        [Required(ErrorMessage = "شناسه مخاطب الزامی است")]
        public int ContactId { get; set; }

        [Required(ErrorMessage = "شناسه فرم الزامی است")]
        public int UserFormId { get; set; }
    }
}
