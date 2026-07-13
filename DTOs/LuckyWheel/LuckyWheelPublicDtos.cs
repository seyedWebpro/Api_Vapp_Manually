using System.ComponentModel.DataAnnotations;

namespace Api_Vapp.DTOs.LuckyWheel
{
    public class LuckyWheelPublicDto
    {
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string Slug { get; set; } = string.Empty;

        public List<LuckyWheelPublicItemDto> Items { get; set; } = new();
    }

    public class LuckyWheelPublicItemDto
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    public class SpinLuckyWheelPublicDto
    {
        [Required]
        public string ParticipantFullName { get; set; } = string.Empty;

        [Required]
        public string ParticipantMobile { get; set; } = string.Empty;
    }

    public class SpinLuckyWheelPublicResponseDto
    {
        public int ParticipantId { get; set; }

        public int WonItemId { get; set; }

        public string WonItemName { get; set; } = string.Empty;
    }
}
