using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Admin
{
    public class EducationalVideoResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string VideoUrl { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateEducationalVideoDto
    {
        [Required]
        [MaxLength(300)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required]
        [MaxLength(1000)]
        public string VideoUrl { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? ThumbnailUrl { get; set; }

        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdateEducationalVideoDto : CreateEducationalVideoDto
    {
    }
}
