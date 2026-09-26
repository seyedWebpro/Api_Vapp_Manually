using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;

namespace Api_Vapp.Interfaces
{
    public interface IAdminPhoneBankService
    {
        Task<ApiResponse<AdminPhoneBankOverviewDto>> GetOverviewAsync();

        Task<ApiResponse<AdminPhoneBankFillResultDto>> StartFillAsync(
            int adminUserId,
            AdminPhoneBankFillDto request);

        Task<ApiResponse<AdminPhoneBankImportResultDto>> ImportPhonesAsync(
            int adminUserId,
            AdminPhoneBankImportDto request);

        Task<ApiResponse<AdminPhoneBankDeleteResultDto>> DeletePhonesAsync(
            int adminUserId,
            AdminPhoneBankDeletePhonesDto request);
    }
}
