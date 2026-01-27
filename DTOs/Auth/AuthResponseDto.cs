namespace Api_Vapp.DTOs.Auth
{
    public class AuthResponseDto
    {
        public int StatusCode { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public TokenResponseDto? Tokens { get; set; }
        public UserInfoDto? User { get; set; }
        public List<string>? Errors { get; set; } // لیست خطاهای اعتبارسنجی (اختیاری)
    }

    public class TokenResponseDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; } // زمان انقضای Access Token
        public DateTime RefreshTokenExpiresAt { get; set; } // زمان انقضای Refresh Token (24 ساعت)
    }

    public class UserInfoDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public bool IsPhoneVerified { get; set; }
    }
}



