using System.ComponentModel.DataAnnotations;
using Api_Vapp.Constants;

namespace Api_Vapp.DTOs.Admin
{
    public class ForbiddenWordResponseDto
    {
        public int Id { get; set; }
        public string Word { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateForbiddenWordDto
    {
        /// <summary>یک کلمه، یا چند کلمه با جداکننده خط جدید / ویرگول.</summary>
        [Required(ErrorMessage = "کلمه فیلتر الزامی است")]
        [MaxLength(2000, ErrorMessage = "متن ورودی بیش از حد طولانی است")]
        public string? Word { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class UpdateForbiddenWordDto
    {
        [Required(ErrorMessage = "کلمه فیلتر الزامی است")]
        [MaxLength(ForbiddenWordLimits.MaxWordLength, ErrorMessage = "کلمه فیلتر نمی‌تواند بیشتر از ۱۰۰ کاراکتر باشد")]
        public string? Word { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class ForbiddenWordValidateRequestDto
    {
        [Required(ErrorMessage = "متن الزامی است")]
        [MaxLength(10000, ErrorMessage = "متن بیش از حد طولانی است")]
        public string? Text { get; set; }
    }

    public class ForbiddenWordValidateResultDto
    {
        public bool IsClean { get; set; }
        public List<string> MatchedWords { get; set; } = new();
        public string? Message { get; set; }
    }

    public class ForbiddenWordBulkCreateResultDto
    {
        public int CreatedCount { get; set; }
        public int SkippedDuplicateCount { get; set; }
        public List<ForbiddenWordResponseDto> Created { get; set; } = new();
        public List<string> SkippedDuplicates { get; set; } = new();
    }
}
