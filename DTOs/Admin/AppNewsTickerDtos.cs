using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Admin
{
    public class AppNewsTickerMessageResponseDto
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateAppNewsTickerMessageDto
    {
        [Required(ErrorMessage = "متن زیرنویس الزامی است")]
        public string? Text { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdateAppNewsTickerMessageDto
    {
        [Required(ErrorMessage = "متن زیرنویس الزامی است")]
        public string? Text { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
