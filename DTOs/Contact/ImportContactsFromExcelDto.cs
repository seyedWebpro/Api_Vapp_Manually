using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Api_Vapp.DTOs.Contact
{
    /// <summary>
    /// DTO برای ایمپورت مخاطبین از اکسل
    /// </summary>
    public class ImportContactsFromExcelDto
    {
        [Required(ErrorMessage = "شناسه دفترچه الزامی است")]
        public int ContactNotebookId { get; set; }

        [Required(ErrorMessage = "فایل اکسل الزامی است")]
        public IFormFile ExcelFile { get; set; } = null!;
    }
}

