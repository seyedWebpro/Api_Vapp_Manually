using Api_Vapp.Models;
using Api_Vapp._Utilities;

namespace Api_Vapp.Interfaces
{
    public interface IReferralContactCodeRepository : IBaseRepository<ReferralContactCode>
    {
        Task<ReferralContactCode?> GetByCodeAsync(int userId, string code);

        Task<bool> ExistsByCodeAsync(int userId, string code);

        Task<IEnumerable<ReferralContactCode>> GetByProgramIdAsync(
            int programId,
            int userId,
            int pageNumber = 1,
            int pageSize = 20);

        Task<int> GetCountByProgramIdAsync(int programId, int userId);

        Task SoftDeleteByProgramIdAsync(int programId, int userId);
    }
}
