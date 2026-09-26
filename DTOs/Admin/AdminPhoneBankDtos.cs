using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.Admin
{
    public class AdminPhoneBankOverviewDto
    {
        public int TotalPhones { get; set; }
        public int AvailablePhones { get; set; }
        public bool ScraperEnabled { get; set; }
        public bool ScraperReachable { get; set; }
        public string? Hint { get; set; }
        public List<AdminPhoneBankStatDto> Stats { get; set; } = new();
    }

    public class AdminPhoneBankStatDto
    {
        public string City { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string SourceDisplayName { get; set; } = string.Empty;
        public int Total { get; set; }
        public int Available { get; set; }
    }

    public class AdminPhoneBankFillDto
    {
        [Required(ErrorMessage = "منبع اسکرپ الزامی است")]
        [RegularExpression(
            "^(all|sheypoor|divar|nshan|balad|googlemaps)$",
            ErrorMessage = "منبع نامعتبر است")]
        public string Source { get; set; } = "balad";

        [Required(ErrorMessage = "شهر الزامی است")]
        [StringLength(100, MinimumLength = 1)]
        public string City { get; set; } = "تهران";

        [Required(ErrorMessage = "دسته‌بندی الزامی است")]
        [MaxLength(200, ErrorMessage = "دسته‌بندی نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد")]
        public string Category { get; set; } = string.Empty;

        [Range(1, 1000, ErrorMessage = "تعداد باید بین ۱ تا ۱۰۰۰ باشد")]
        public int MaxPhones { get; set; } = 100;
    }

    public class AdminPhoneBankFillResultDto
    {
        public string TaskId { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class AdminPhoneBankImportDto
    {
        [Required(ErrorMessage = "شهر الزامی است")]
        [StringLength(100, MinimumLength = 1)]
        public string City { get; set; } = "تهران";

        [Required(ErrorMessage = "دسته‌بندی الزامی است")]
        [MaxLength(200)]
        public string Category { get; set; } = string.Empty;

        [RegularExpression(
            "^(all|sheypoor|divar|nshan|balad|googlemaps|manual)$",
            ErrorMessage = "منبع نامعتبر است")]
        public string Source { get; set; } = "manual";

        [Required(ErrorMessage = "حداقل یک شماره الزامی است")]
        [MinLength(1, ErrorMessage = "حداقل یک شماره الزامی است")]
        [MaxLength(1000, ErrorMessage = "حداکثر ۱۰۰۰ شماره در هر بار")]
        public List<string> Phones { get; set; } = new();
    }

    public class AdminPhoneBankImportResultDto
    {
        public int Inserted { get; set; }
        public int TotalSubmitted { get; set; }
        public string City { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class AdminPhoneBankDeletePhonesDto
    {
        [Required(ErrorMessage = "حداقل یک شماره الزامی است")]
        [MinLength(1)]
        [MaxLength(1000)]
        public List<string> Phones { get; set; } = new();
    }

    public class AdminPhoneBankDeleteResultDto
    {
        public int Deleted { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
