using System.Text.Json;
using Api_Vapp.Constants;
using Api_Vapp.Data;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Subscription;
using Api_Vapp.Exceptions;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services
{
    public class SubscriptionActivationService : ISubscriptionActivationService
    {
        private readonly Api_Context _context;
        private readonly ILogger<SubscriptionActivationService> _logger;

        public SubscriptionActivationService(Api_Context context, ILogger<SubscriptionActivationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task FulfillVerifiedPaymentAsync(Payment payment)
        {
            if (payment.PaymentType != PaymentTypes.Subscription)
                return;

            var alreadyFulfilled = await _context.UserSubscriptions
                .AnyAsync(us => us.SourcePaymentId == payment.Id && !us.IsDeleted);
            if (alreadyFulfilled)
                return;

            if (string.IsNullOrWhiteSpace(payment.MetaData))
            {
                _logger.LogError("Subscription payment {PaymentId} has empty metadata", payment.Id);
                throw AppException.Internal(ErrorCodes.PaymentFailed, SubscriptionMessages.ActivationFailed);
            }

            var metadata = JsonSerializer.Deserialize<SubscriptionPaymentMetadata>(payment.MetaData);
            if (metadata == null)
            {
                _logger.LogError("Subscription payment {PaymentId} has invalid metadata", payment.Id);
                throw AppException.Internal(ErrorCodes.PaymentFailed, SubscriptionMessages.ActivationFailed);
            }

            if (payment.Amount != metadata.PayableAmount)
            {
                _logger.LogWarning(
                    "Payment amount mismatch for subscription payment {PaymentId}. Paid={Paid}, Expected={Expected}",
                    payment.Id,
                    payment.Amount,
                    metadata.PayableAmount);
                throw AppException.BadRequest(ErrorCodes.PaymentFailed, ControlledErrorHelper.PaymentFailed);
            }

            var plan = await _context.SubscriptionPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == metadata.SubscriptionPlanId && !p.IsDeleted && p.IsActive);

            if (plan == null)
            {
                _logger.LogError(
                    "Subscription plan {PlanId} not found for payment {PaymentId}",
                    metadata.SubscriptionPlanId,
                    payment.Id);
                throw AppException.Internal(ErrorCodes.PaymentFailed, SubscriptionMessages.ActivationFailed);
            }

            await ActivateAsync(
                payment.UserId,
                plan,
                metadata.OriginalAmount,
                metadata.DiscountAmount,
                metadata.DiscountCode,
                metadata.DiscountCodeId,
                payment.Id);

            _logger.LogInformation(
                "Subscription activated for user {UserId} via payment {PaymentId}, plan {PlanId}",
                payment.UserId,
                payment.Id,
                plan.Id);
        }

        public async Task<UserSubscription> ActivateAsync(
            int userId,
            SubscriptionPlan plan,
            decimal originalAmount,
            decimal discountAmount,
            string? discountCode,
            int? discountCodeId,
            int? paymentId)
        {
            if (paymentId.HasValue)
            {
                var existing = await _context.UserSubscriptions
                    .FirstOrDefaultAsync(us => us.SourcePaymentId == paymentId && !us.IsDeleted);
                if (existing != null)
                    return existing;
            }

            var ownsTransaction = false;
            var transaction = _context.Database.CurrentTransaction;
            if (transaction == null)
            {
                transaction = await _context.Database.BeginTransactionAsync();
                ownsTransaction = true;
            }

            try
            {
                var now = DateTime.UtcNow;

                var activeSubscriptions = await _context.UserSubscriptions
                    .Where(us =>
                        us.UserId == userId
                        && !us.IsDeleted
                        && us.Status == "Active"
                        && us.ExpiresAt > now)
                    .ToListAsync();

                foreach (var existing in activeSubscriptions)
                {
                    existing.Status = "Cancelled";
                    existing.UpdatedAt = now;
                }

                var subscription = new UserSubscription
                {
                    UserId = userId,
                    SubscriptionPlanId = plan.Id,
                    StartDate = now,
                    ExpiresAt = now.AddDays(plan.DurationDays),
                    Status = "Active",
                    SourcePaymentId = paymentId,
                    CreatedAt = now
                };

                _context.UserSubscriptions.Add(subscription);
                await _context.SaveChangesAsync();

                if (discountCodeId.HasValue && discountAmount > 0)
                {
                    var discount = await _context.SubscriptionDiscountCodes
                        .FirstOrDefaultAsync(d => d.Id == discountCodeId.Value && !d.IsDeleted && d.IsActive);

                    if (discount != null)
                    {
                        discount.UsedCount += 1;
                        discount.UpdatedAt = now;

                        _context.SubscriptionDiscountUsages.Add(new SubscriptionDiscountUsage
                        {
                            SubscriptionDiscountCodeId = discount.Id,
                            UserId = userId,
                            PaymentId = paymentId,
                            UserSubscriptionId = subscription.Id,
                            DiscountAmount = discountAmount,
                            UsedAt = now
                        });

                        await _context.SaveChangesAsync();
                    }
                }

                if (ownsTransaction)
                    await transaction.CommitAsync();

                _ = originalAmount;
                _ = discountCode;
                return subscription;
            }
            catch (Exception ex)
            {
                if (ownsTransaction)
                    await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to activate subscription for user {UserId}, plan {PlanId}", userId, plan.Id);
                throw new AppException(ErrorCodes.Unexpected, SubscriptionMessages.ActivationFailed, 500, innerException: ex);
            }
        }
    }
}
