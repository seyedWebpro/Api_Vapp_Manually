using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.UserForm
{
    public class PublishUserFormDto
    {
        [MaxLength(100)]
        public string? Slug { get; set; }
    }
}
