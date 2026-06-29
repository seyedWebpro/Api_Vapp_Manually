namespace Api_Vapp.DTOs.LuckyWheel
{
    public class LuckyWheelResponseDto
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? Slug { get; set; }

        public string Status { get; set; } = string.Empty;

        public bool SaveToPhonebook { get; set; }

        public bool IsActive { get; set; }

        public string? PublicUrl { get; set; }

        public List<int> NotebookIds { get; set; } = new();

        public List<LuckyWheelItemDto> Items { get; set; } = new();

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime? PublishedAt { get; set; }
    }
}
