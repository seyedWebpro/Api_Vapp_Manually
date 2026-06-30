using Api_Vapp.Models;

namespace Api_Vapp.Interfaces
{
    public interface IBookingSystemRepository
    {
        Task<IEnumerable<BookingSystem>> GetByUserIdAsync(int userId, int pageNumber, int pageSize, bool? isActive);
        Task<int> GetCountByUserIdAsync(int userId, bool? isActive);
        Task<int> GetActiveCountByUserIdAsync(int userId);
        Task<BookingSystem?> GetByIdAndUserIdAsync(int id, int userId);
        Task<BookingSystem?> GetByIdWithDetailsAsync(int id, int userId);
        Task<bool> ExistsByTitleAsync(int userId, string title, int? excludeId = null);
        Task<bool> ExistsBySlugAsync(string slug, int? excludeId = null);
    }
}
