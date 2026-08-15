namespace Api_Vapp.Models
{
    public class RefreshToken
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsRevoked { get; set; } = false;
        public DateTime? RevokedAt { get; set; }

        /// <summary>
        /// پس از rotation اتمیک، مقدار refresh token جایگزین روی ردیف قدیمی نوشته می‌شود
        /// تا درخواست‌های همزمان در پنجره grace همان توکن را بگیرند (نه 401).
        /// </summary>
        public string? ReplacementToken { get; set; }

        /// <summary>
        /// FK اختیاری به ردیف توکن جدید پس از rotation.
        /// </summary>
        public int? ReplacedByTokenId { get; set; }

        public virtual User User { get; set; } = null!;
    }
}
