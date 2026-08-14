using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services
{
    public class NumberSeekerPhoneAccessService : INumberSeekerPhoneAccessService
    {
        private readonly IUserRepository _userRepository;
        private readonly Api_Context _context;
        private readonly ILogger<NumberSeekerPhoneAccessService> _logger;

        public NumberSeekerPhoneAccessService(
            IUserRepository userRepository,
            Api_Context context,
            ILogger<NumberSeekerPhoneAccessService> logger)
        {
            _userRepository = userRepository;
            _context = context;
            _logger = logger;
        }

        public async Task<bool> CanViewPhonesAsync(int userId, CancellationToken cancellationToken = default)
        {
            if (userId <= 0)
                return false;

            var user = await _userRepository.GetByIdAsync(userId);
            var canView = user is { IsDeleted: false, CanViewNumberSeekerPhones: true };
            _logger.LogDebug("NumberSeeker phone visibility for user {UserId}: {CanView}", userId, canView);
            return canView;
        }

        public async Task<HashSet<string>> GetHiddenMobileNumbersAsync(
            int userId,
            CancellationToken cancellationToken = default)
        {
            if (userId <= 0)
                return new HashSet<string>(StringComparer.Ordinal);

            if (await CanViewPhonesAsync(userId, cancellationToken))
                return new HashSet<string>(StringComparer.Ordinal);

            var numbers = await _context.Contacts
                .AsNoTracking()
                .Where(c => !c.IsDeleted
                            && c.HideMobileNumber
                            && c.ContactNotebook.UserId == userId
                            && !c.ContactNotebook.IsDeleted)
                .Select(c => c.MobileNumber)
                .ToListAsync(cancellationToken);

            return numbers.ToHashSet(StringComparer.Ordinal);
        }
    }
}
