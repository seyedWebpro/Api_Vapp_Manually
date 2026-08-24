using Api_Vapp.DTOs.BankAccount;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Message;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط سرویس مدیریت شماره حساب برای ارسال سریع
    /// </summary>
    public interface IBankAccountService
    {
        Task<ApiResponse<BankAccountResponseDto>> CreateBankAccountAsync(int userId, CreateBankAccountDto createDto);

        Task<ApiResponse<BankAccountListResponseDto>> GetBankAccountsAsync(int userId, int pageNumber = 1, int pageSize = 10);

        Task<ApiResponse<BankAccountResponseDto>> GetBankAccountByIdAsync(int id, int userId);

        Task<ApiResponse<BankAccountResponseDto>> UpdateBankAccountAsync(int id, int userId, UpdateBankAccountDto updateDto);

        Task<ApiResponse<bool>> DeleteBankAccountAsync(int id, int userId);

        Task<ApiResponse<BankAccountResponseDto>> SetUserDefaultBankAccountAsync(int userId, int bankAccountId);

        Task<ApiResponse<DirectSendResultDto>> QuickSendBankAccountAsync(int userId, QuickSendBankAccountDto quickSendDto);
    }
}
