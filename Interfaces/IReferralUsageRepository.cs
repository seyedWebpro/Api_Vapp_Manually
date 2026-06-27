using Api_Vapp.Models;
using Api_Vapp._Utilities;

namespace Api_Vapp.Interfaces
{
    public interface IReferralUsageRepository : IBaseRepository<ReferralUsage>
    {
        Task<IEnumerable<ReferralUsage>> GetByProgramIdAsync(
            int programId,
            int userId,
            int pageNumber = 1,
            int pageSize = 10,
            DateTime? fromDate = null,
            DateTime? toDate = null);

        Task<int> GetCountByProgramIdAsync(
            int programId,
            int userId,
            DateTime? fromDate = null,
            DateTime? toDate = null);

        Task<int> GetSuccessfulReferralsCountAsync(int userId);

        Task<decimal> GetTotalRewardsPaidAsync(int userId);

        Task<int> GetDistinctActiveContactsCountAsync(int userId);

        Task<(decimal CustomerTotal, decimal ReferrerTotal)> GetTotalsByProgramIdAsync(
            int programId,
            int userId,
            DateTime? fromDate = null,
            DateTime? toDate = null);
    }
}
