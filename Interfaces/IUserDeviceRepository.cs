using Api_Vapp.Models;

namespace Api_Vapp.Interfaces
{
    public interface IUserDeviceRepository
    {
        Task<UserDevice?> GetByTokenAsync(string fcmToken);

        Task<List<UserDevice>> GetActiveByUserIdAsync(int userId);

        Task<UserDevice> AddAsync(UserDevice device);

        Task<UserDevice> UpdateAsync(UserDevice device);

        Task SoftDeleteAsync(UserDevice device);
    }
}
