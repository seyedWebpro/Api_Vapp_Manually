using Api_Vapp.DTOs.Auth;

namespace Api_Vapp.Interfaces
{
    public interface IAuthService
    {
        Task<SendOtpResponseDto> RegisterAsync(RegisterDto registerDto, string? ipAddress = null);
        Task<AuthResponseDto> VerifyRegistrationOtpAsync(VerifyOtpDto verifyOtpDto, string? ipAddress = null);
        Task<SendOtpResponseDto> ResendRegistrationOtpAsync(LoginDto loginDto, string? ipAddress = null);
        Task<SendOtpResponseDto> LoginAsync(LoginDto loginDto, string? ipAddress = null, bool requireAdminPanelAccess = false);
        Task<AuthResponseDto> VerifyLoginOtpAsync(VerifyOtpDto verifyOtpDto, string? ipAddress = null, bool requireAdminPanelAccess = false);
        Task<SendOtpResponseDto> ResendLoginOtpAsync(LoginDto loginDto, string? ipAddress = null, bool requireAdminPanelAccess = false);
        Task<SendOtpResponseDto> ForgotPasswordAsync(ForgotPasswordDto forgotPasswordDto, string? ipAddress = null);
        Task<AuthResponseDto> ResetPasswordAsync(ResetPasswordDto resetPasswordDto, string? ipAddress = null);
        Task<SendOtpResponseDto> ResendForgotPasswordOtpAsync(LoginDto loginDto, string? ipAddress = null);
        Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenDto refreshTokenDto, string? ipAddress = null);
        Task<LogoutResponseDto> LogoutAsync(int userId, string? jti, string? ipAddress = null);
    }
}



