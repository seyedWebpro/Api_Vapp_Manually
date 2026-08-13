using System.Linq;
using Api_Vapp.Models;

namespace Api_Vapp.Services.Audit
{
    /// <summary>
    /// اسنپ‌شات استاندارد لاگ مالی پرداخت — برای ذخیره در AdminAuditLogs (Metadata/After)
    /// بدون secret (MerchantId کامل، توکن و …)
    /// </summary>
    public static class PaymentAuditDetails
    {
        public static object UserSnapshot(User? user) => new
        {
            userId = user?.Id,
            phoneNumber = user?.PhoneNumber,
            fullName = user?.FullName
        };

        public static object PaymentSnapshot(Payment payment, User? user = null, object? extra = null)
        {
            return new
            {
                occurredAtUtc = DateTime.UtcNow,
                user = UserSnapshot(user ?? payment.User),
                paymentId = payment.Id,
                userId = payment.UserId,
                amount = payment.Amount,
                currency = "IRT",
                amountLabel = $"{payment.Amount:N0} تومان",
                paymentType = payment.PaymentType,
                gateway = payment.Gateway,
                orderId = payment.OrderId,
                authority = payment.RefId,
                referenceNumber = payment.ReferenceNumber,
                transactionId = payment.TransactionId,
                cardNumberMasked = MaskCard(payment.CardNumber),
                status = payment.Status,
                errorCode = payment.ErrorCode,
                errorMessage = payment.ErrorMessage,
                description = payment.Description,
                createdAtUtc = payment.CreatedAt,
                paidAtUtc = payment.PaidAt,
                verifiedAtUtc = payment.VerifiedAt,
                extra
            };
        }

        public static object ChargeRequest(
            User user,
            Payment payment,
            decimal requestedAmount,
            decimal payableAmount,
            decimal discountAmount,
            bool referralApplied,
            string? referralCode,
            string? gatewayUrl,
            bool isSimulation)
        {
            return new
            {
                occurredAtUtc = DateTime.UtcNow,
                eventType = "WalletChargeRequest",
                user = UserSnapshot(user),
                paymentId = payment.Id,
                requestedAmount,
                payableAmount,
                discountAmount,
                referralApplied,
                referralCode,
                amountLabel = $"{payableAmount:N0} تومان",
                paymentType = payment.PaymentType,
                gateway = payment.Gateway,
                orderId = payment.OrderId,
                authority = payment.RefId,
                status = payment.Status,
                isSimulation,
                gatewayHost = TryHost(gatewayUrl),
                description = payment.Description
            };
        }

        public static object SubscriptionRequest(
            User? user,
            Payment payment,
            int planId,
            string planName,
            string tierCode,
            decimal originalAmount,
            decimal discountAmount,
            decimal payableAmount,
            string? discountCode,
            string? gatewayUrl)
        {
            return new
            {
                occurredAtUtc = DateTime.UtcNow,
                eventType = "SubscriptionPurchaseRequest",
                user = UserSnapshot(user),
                paymentId = payment.Id,
                planId,
                planName,
                tierCode,
                originalAmount,
                discountAmount,
                payableAmount,
                discountCode,
                amountLabel = $"{payableAmount:N0} تومان",
                paymentType = payment.PaymentType,
                gateway = payment.Gateway,
                orderId = payment.OrderId,
                authority = payment.RefId,
                status = payment.Status,
                gatewayHost = TryHost(gatewayUrl),
                description = payment.Description
            };
        }

        public static object Callback(
            Payment? payment,
            User? user,
            string? authority,
            string? status,
            bool success,
            string? outcomeMessage)
        {
            return new
            {
                occurredAtUtc = DateTime.UtcNow,
                eventType = "ZarinPalCallback",
                callbackStatus = status,
                authority,
                success,
                outcomeMessage,
                payment = payment == null ? null : PaymentSnapshot(payment, user)
            };
        }

        private static string? MaskCard(string? cardNumber)
        {
            if (string.IsNullOrWhiteSpace(cardNumber))
                return cardNumber;
            var digits = new string(cardNumber.Where(char.IsDigit).ToArray());
            if (digits.Length < 4)
                return "****";
            return $"******{digits[^4..]}";
        }

        private static string? TryHost(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;
            return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : null;
        }
    }
}
