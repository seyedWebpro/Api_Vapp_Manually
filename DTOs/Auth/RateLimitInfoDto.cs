namespace Api_Vapp.DTOs.Auth
{
    /// <summary>
    /// اطلاعات Rate Limit برای نگهداری زمان انقضا
    /// </summary>
    public class RateLimitInfoDto
    {
        public DateTime ExpiresAt { get; set; }
        public bool IsActive { get; set; } = true;
    }
}


