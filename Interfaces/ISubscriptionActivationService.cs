using Api_Vapp.Models;

namespace Api_Vapp.Interfaces
{
    public interface ISubscriptionActivationService
    {
        Task<UserSubscription> ActivateAsync(
            int userId,
            SubscriptionPlan plan,
            decimal originalAmount,
            decimal discountAmount,
            string? discountCode,
            int? discountCodeId,
            int? paymentId);

        Task FulfillVerifiedPaymentAsync(Payment payment);
    }
}
