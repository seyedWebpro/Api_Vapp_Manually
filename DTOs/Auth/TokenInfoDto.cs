namespace Api_Vapp.DTOs.Auth
{
    /// <summary>
    /// اطلاعات استخراج شده از JWT Token
    /// </summary>
    public class TokenInfoDto
    {
        /// <summary>
        /// شناسه کاربر
        /// </summary>
        public int? UserId { get; set; }

        /// <summary>
        /// شماره تلفن کاربر
        /// </summary>
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// شناسه یکتای توکن (JTI)
        /// </summary>
        public string? TokenId { get; set; }

        /// <summary>
        /// بررسی معتبر بودن اطلاعات توکن
        /// </summary>
        public bool IsValid => UserId.HasValue && !string.IsNullOrEmpty(PhoneNumber);
    }
}



















