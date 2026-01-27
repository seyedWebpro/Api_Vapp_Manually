using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Contact
{
    /// <summary>
    /// DTO برای انتقال مخاطب به دفترچه دیگر
    /// </summary>
    public class TransferContactDto
    {
        [Required(ErrorMessage = "شناسه دفترچه مبدا الزامی است")]
        public int FromNotebookId { get; set; }

        [Required(ErrorMessage = "شناسه دفترچه مقصد الزامی است")]
        public int ToNotebookId { get; set; }
    }
}


