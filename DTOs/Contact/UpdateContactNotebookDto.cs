using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Contact
{
    /// <summary>
    /// DTO برای به‌روزرسانی دفترچه
    /// </summary>
    public class UpdateContactNotebookDto
    {
        [StringLength(200, ErrorMessage = "نام دفترچه نمی‌تواند بیشتر از 200 کاراکتر باشد")]
        public string? Name { get; set; }

        [StringLength(1000, ErrorMessage = "توضیحات نمی‌تواند بیشتر از 1000 کاراکتر باشد")]
        public string? Description { get; set; }

        [StringLength(500, ErrorMessage = "آیکون نمی‌تواند بیشتر از 500 کاراکتر باشد")]
        public string? Icon { get; set; }

        public bool? IsActive { get; set; }
    }
}


