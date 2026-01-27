using Api_Vapp.Models;
using Api_Vapp._Utilities;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط Repository برای مدیریت مناسبت‌های خاص
    /// </summary>
    public interface ISpecialOccasionRepository : IBaseRepository<SpecialOccasion>
    {
        Task<IEnumerable<SpecialOccasion>> GetByUserIdAsync(int? userId);
        Task<IEnumerable<SpecialOccasion>> GetActiveByUserIdAsync(int? userId);
        Task<IEnumerable<SpecialOccasion>> GetSystemOccasionsAsync();
    }
}

