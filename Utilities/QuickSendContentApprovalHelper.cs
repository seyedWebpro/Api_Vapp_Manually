using Api_Vapp.Constants;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Message;
using Api_Vapp.Models;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// تأیید یک‌باره محتوای ارسال سریع — مثل قالب‌ها.
    /// تا قبل از ویرایش مجدد، پس از Approved نیازی به تأیید دوباره نیست.
    /// </summary>
    public static class QuickSendContentApprovalHelper
    {
        public const string PendingUserMessage =
            "پیام سریع شما در صف تأیید ادمین است. پس از تأیید، می‌توانید بدون نیاز به تأیید مجدد ارسال کنید.";

        public static bool IsApproved(string? approvalStatus) =>
            string.Equals(approvalStatus, AdminApprovalStatuses.Approved, StringComparison.Ordinal);

        public static bool IsRejected(string? approvalStatus) =>
            string.Equals(approvalStatus, AdminApprovalStatuses.Rejected, StringComparison.Ordinal);

        /// <summary>
        /// بعد از ساخت / انتشار / ویرایش محتوا → Pending و پاک‌کردن نتیجه بررسی قبلی.
        /// </summary>
        public static void ResetToPending(IQuickSendApprovable entity)
        {
            entity.ApprovalStatus = AdminApprovalStatuses.Pending;
            entity.ApprovedAt = null;
            entity.ApprovedByUserId = null;
            entity.RejectionReason = null;
            entity.UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// فقط وقتی محتوا واقعاً عوض شده Pending شود (مثلاً IsActive alone نباید تأیید را باطل کند).
        /// </summary>
        public static void ResetToPendingIfNeeded(IQuickSendApprovable entity, bool contentChanged)
        {
            if (contentChanged)
                ResetToPending(entity);
        }

        /// <summary>
        /// اگر Approved نباشد، پاسخ مناسب برای کاربر برمی‌گرداند؛ در غیر این صورت null (ادامه ارسال).
        /// </summary>
        public static ApiResponse<DirectSendResultDto>? TryBlockIfNotApproved(
            string? approvalStatus,
            string? rejectionReason,
            string entityPersianLabel)
        {
            if (IsApproved(approvalStatus))
                return null;

            if (IsRejected(approvalStatus))
            {
                var reason = string.IsNullOrWhiteSpace(rejectionReason)
                    ? "دلیل مشخص نشده است"
                    : rejectionReason.Trim();

                return ApiResponse<DirectSendResultDto>.BadRequest(
                    $"محتوای ارسال سریع {entityPersianLabel} رد شده است. دلیل: {reason}",
                    errorCode: ErrorCodes.InvalidInput);
            }

            return ApiResponse<DirectSendResultDto>.CreateSuccess(
                new DirectSendResultDto
                {
                    AdminApprovalStatus = AdminApprovalStatuses.Pending
                },
                PendingUserMessage,
                202);
        }
    }
}
