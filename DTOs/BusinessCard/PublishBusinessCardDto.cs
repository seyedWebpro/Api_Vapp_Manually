using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.BusinessCard
{
    public class PublishBusinessCardDto
    {
        [MaxLength(100, ErrorMessage = "slug نمی‌تواند بیشتر از 100 کاراکتر باشد")]
        public string? Slug { get; set; }
    }
}
