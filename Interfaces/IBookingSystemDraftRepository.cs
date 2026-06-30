using Api_Vapp.Models;

namespace Api_Vapp.Interfaces
{
    public interface IBookingSystemDraftRepository
    {
        Task<BookingSystemDraft> AddAsync(BookingSystemDraft draft);
        Task<BookingSystemDraft> UpdateAsync(BookingSystemDraft draft);
        Task<BookingSystemDraft?> GetActiveByDraftIdAsync(string draftId, int userId);
        Task<bool> DeleteAsync(string draftId, int userId);
    }
}
