using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Subscription;

namespace Api_Vapp.Interfaces
{
    public interface IUserSubscriptionService
    {
        Task<ApiResponse<SubscriptionCatalogDto>> GetCatalogAsync(int userId);
    }
}
