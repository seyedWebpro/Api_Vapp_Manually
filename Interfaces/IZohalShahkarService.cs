using Api_Vapp.DTOs.Zohal;
using Api_Vapp.Services.Zohal;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// تطبیق کد ملی و شماره موبایل از طریق سرویس شاهکار زحل
    /// </summary>
    public interface IZohalShahkarService
    {
        bool IsEnabled { get; }

        Task<ShahkarVerificationResult> VerifyAsync(
            string nationalCode,
            string mobile,
            ShahkarVerifyContext? context = null,
            CancellationToken cancellationToken = default);
    }
}
