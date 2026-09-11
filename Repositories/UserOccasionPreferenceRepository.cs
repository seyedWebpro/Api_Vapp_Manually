using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    public class UserOccasionPreferenceRepository : IUserOccasionPreferenceRepository
    {
        private readonly Api_Context _context;

        public UserOccasionPreferenceRepository(Api_Context context)
        {
            _context = context;
        }

        public async Task<List<UserOccasionPreference>> GetByUserIdAsync(int userId)
        {
            return await _context.UserOccasionPreferences.AsNoTracking()
                .Where(p => p.UserId == userId && !p.IsDeleted)
                .ToListAsync();
        }

        public async Task<UserOccasionPreference?> GetByUserAndOccasionAsync(int userId, int occasionId)
        {
            return await _context.UserOccasionPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId
                    && p.SpecialOccasionId == occasionId
                    && !p.IsDeleted);
        }

        public async Task<Dictionary<int, UserOccasionPreference>> GetMapByUserIdAsync(int userId)
        {
            return await _context.UserOccasionPreferences.AsNoTracking()
                .Where(p => p.UserId == userId && !p.IsDeleted)
                .ToDictionaryAsync(p => p.SpecialOccasionId);
        }

        public async Task AddAsync(UserOccasionPreference preference)
        {
            await _context.UserOccasionPreferences.AddAsync(preference);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(UserOccasionPreference preference)
        {
            preference.UpdatedAt = DateTime.UtcNow;
            _context.UserOccasionPreferences.Update(preference);
            await _context.SaveChangesAsync();
        }
    }

    public class UserOccasionProfileRepository : IUserOccasionProfileRepository
    {
        private readonly Api_Context _context;

        public UserOccasionProfileRepository(Api_Context context)
        {
            _context = context;
        }

        public async Task<UserOccasionProfile?> GetByUserIdAsync(int userId)
        {
            return await _context.UserOccasionProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);
        }

        public async Task<UserOccasionProfile> GetOrCreateAsync(int userId)
        {
            var existing = await GetByUserIdAsync(userId);
            if (existing != null)
                return existing;

            var profile = new UserOccasionProfile
            {
                UserId = userId,
                CongratulationsEnabled = true,
                CondolencesEnabled = true,
                ScheduledTimeTehran = new TimeSpan(10, 0, 0),
                CreatedAt = DateTime.UtcNow
            };

            await _context.UserOccasionProfiles.AddAsync(profile);
            await _context.SaveChangesAsync();
            return profile;
        }

        public async Task UpdateAsync(UserOccasionProfile profile)
        {
            profile.UpdatedAt = DateTime.UtcNow;
            _context.UserOccasionProfiles.Update(profile);
            await _context.SaveChangesAsync();
        }

        public async Task<List<UserOccasionProfile>> GetActiveProfilesLinkedToAutomationAsync()
        {
            return await _context.UserOccasionProfiles.AsNoTracking()
                .Where(p => !p.IsDeleted
                    && p.AutomatedMessageId.HasValue
                    && (p.CongratulationsEnabled || p.CondolencesEnabled))
                .ToListAsync();
        }
    }
}
