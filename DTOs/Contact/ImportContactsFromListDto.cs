using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Contact
{
    public class ImportContactsFromListDto
    {
        [Required(ErrorMessage = "شناسه دفترچه الزامی است")]
        public int ContactNotebookId { get; set; }

        [Required(ErrorMessage = "لیست مخاطبین الزامی است")]
        public List<ImportContactItemDto> Contacts { get; set; } = new();
    }

    public class ImportContactItemDto
    {
        [Required(ErrorMessage = "شماره موبایل الزامی است")]
        public string MobileNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "نام مخاطب الزامی است")]
        public string Name { get; set; } = string.Empty;
    }
}























