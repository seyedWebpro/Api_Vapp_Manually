using Api_Vapp.Models;
using Api_Vapp._Utilities;
using Api_Vapp.Utilities;

namespace Api_Vapp.Interfaces
{
    public interface ISpecialOccasionRepository : IBaseRepository<SpecialOccasion>
    {
        Task<IEnumerable<SpecialOccasion>> GetByUserIdAsync(int? userId);
        Task<IEnumerable<SpecialOccasion>> GetActiveByUserIdAsync(int? userId);
        Task<IEnumerable<SpecialOccasion>> GetSystemOccasionsAsync();
        Task<List<SpecialOccasion>> GetCatalogForUserAsync(int userId);
        Task<List<SpecialOccasion>> GetOccasionsMatchingTodayAsync(OccasionCalendarHelper.CalendarDayParts today);
        Task<SpecialOccasion?> GetByCodeAsync(string code);
    }

    public interface IUserOccasionPreferenceRepository
    {
        Task<List<UserOccasionPreference>> GetByUserIdAsync(int userId);
        Task<UserOccasionPreference?> GetByUserAndOccasionAsync(int userId, int occasionId);
        Task<Dictionary<int, UserOccasionPreference>> GetMapByUserIdAsync(int userId);
        Task AddAsync(UserOccasionPreference preference);
        Task UpdateAsync(UserOccasionPreference preference);
    }

    public interface IUserOccasionProfileRepository
    {
        Task<UserOccasionProfile?> GetByUserIdAsync(int userId);
        Task<UserOccasionProfile> GetOrCreateAsync(int userId);
        Task UpdateAsync(UserOccasionProfile profile);
        Task<List<UserOccasionProfile>> GetActiveProfilesLinkedToAutomationAsync();
    }
}
