namespace Api_Vapp.Configuration
{
    public class PublicParticipantOptions
    {
        public const string SectionName = "PublicParticipant";

        /// <summary>
        /// مدت اعتبار جلسه ثبت‌نام (دقیقه)
        /// </summary>
        public int SessionMinutes { get; set; } = 120;

        /// <summary>
        /// pepper جدا برای هش توکن — در صورت خالی بودن از Jwt:Secret استفاده می‌شود
        /// </summary>
        public string? TokenPepper { get; set; }
    }
}
