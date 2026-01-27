using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Contact
{
    public class ContactOccasionDto
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "عنوان مناسبت الزامی است")]
        [StringLength(100, ErrorMessage = "عنوان مناسبت نمی‌تواند بیشتر از 100 کاراکتر باشد")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "تاریخ مناسبت الزامی است")]
        public DateTime Date { get; set; }

        public bool HasTime { get; set; } = false;
    }
}


























