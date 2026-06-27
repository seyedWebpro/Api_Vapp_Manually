using Api_Vapp.Models;
using Api_Vapp._Utilities;

namespace Api_Vapp.Interfaces
{
    public interface IReferralProgramDraftRepository : IBaseRepository<ReferralProgramDraft>
    {
        Task<ReferralProgramDraft?> GetByDraftIdAsync(string draftId, int userId);

        Task<ReferralProgramDraft?> GetActiveByDraftIdAsync(string draftId, int userId);

        Task<bool> DeleteAsync(string draftId, int userId);
    }
}
