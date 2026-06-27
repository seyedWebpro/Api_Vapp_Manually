using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.UserForm
{
    public class CreateUserFormDto
    {
        [MaxLength(100, ErrorMessage = "templateKey نمی‌تواند بیشتر از 100 کاراکتر باشد")]
        public string? TemplateKey { get; set; }

        [MaxLength(200, ErrorMessage = "عنوان فرم نمی‌تواند بیشتر از 200 کاراکتر باشد")]
        public string? Title { get; set; }

        [MaxLength(2000, ErrorMessage = "توضیحات نمی‌تواند بیشتر از 2000 کاراکتر باشد")]
        public string? Description { get; set; }

        [MaxLength(100, ErrorMessage = "slug نمی‌تواند بیشتر از 100 کاراکتر باشد")]
        public string? Slug { get; set; }

        public bool SaveToPhonebook { get; set; }

        public List<int> NotebookIds { get; set; } = new();

        public List<UserFormFieldDto> Fields { get; set; } = new();
    }
}
