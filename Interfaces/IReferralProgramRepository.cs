using Api_Vapp.Models;
using Api_Vapp._Utilities;

namespace Api_Vapp.Interfaces
{
    public interface IReferralProgramRepository : IBaseRepository<ReferralProgram>
    {
        Task<IEnumerable<ReferralProgram>> GetByUserIdAsync(int userId, int pageNumber = 1, int pageSize = 10, bool? isActive = null);

        Task<int> GetCountByUserIdAsync(int userId, bool? isActive = null);

        Task<int> GetActiveCountByUserIdAsync(int userId);

        Task<ReferralProgram?> GetByIdAndUserIdAsync(int id, int userId);

        Task<ReferralProgram?> GetByPublicCodeAsync(int userId, string publicCode);

        Task<bool> ExistsByPublicCodeAsync(int userId, string publicCode, int? excludeId = null);

        Task<bool> ExistsByTitleAsync(int userId, string title, int? excludeId = null);
    }
}
