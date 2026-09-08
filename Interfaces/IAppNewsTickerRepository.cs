using Api_Vapp.Models;

namespace Api_Vapp.Interfaces
{
    public interface IAppNewsTickerRepository
    {
        Task<List<AppNewsTickerMessage>> GetAllAsync(bool includeInactive);
        Task<List<AppNewsTickerMessage>> GetActiveAsync();
        Task<AppNewsTickerMessage?> GetByIdAsync(int id);
        void Add(AppNewsTickerMessage entity);
        Task SaveChangesAsync();
    }
}
