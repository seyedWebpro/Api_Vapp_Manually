namespace Api_Vapp.DTOs.Auth
{
    public class RegisterOtpCacheDto
    {
        public string OtpCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string NationalId { get; set; } = string.Empty;
    }
}



