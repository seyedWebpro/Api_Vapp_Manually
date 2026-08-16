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
            "محتوای شما در صف تأیید ادمین است. پس از تأیید، می‌توانید لینک را بدون نیاز به تأیید مجدد ارسال کنید.";

        public static string BuildPublishSubmittedMessage(string resourcePersianLabel) =>
            $"درخواست انتشار {resourcePersianLabel} ثبت شد. پس از تأیید ادمین، لینک عمومی فعال می‌شود.";

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
        /// اگر Approved نباشد، پاسخ مناسب برای مالک محتوا برمی‌گرداند؛ در غیر این صورت null (ادامه ارسال).
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
                return ApiResponse<DirectSendResultDto>.BadRequest(
                    BuildOwnerRejectedMessage(entityPersianLabel, rejectionReason),
                    errorCode: ErrorCodes.ContentRejected);
            }

            return ApiResponse<DirectSendResultDto>.CreateSuccess(
                new DirectSendResultDto
                {
                    AdminApprovalStatus = AdminApprovalStatuses.Pending
                },
                PendingUserMessage,
                202);
        }

        /// <summary>
        /// دسترسی عمومی فقط پس از Approved — Pending/Rejected مسدود می‌شود.
        /// دلیل رد ادمین فقط برای مالک محتواست، نه بازدیدکننده عمومی.
        /// </summary>
        public static ApiResponse<T>? TryBlockPublicAccess<T>(
            string? approvalStatus,
            string resourcePersianLabel)
        {
            if (IsApproved(approvalStatus))
                return null;

            if (IsRejected(approvalStatus))
            {
                return ApiResponse<T>.Forbidden(
                    BuildPublicRejectedMessage(resourcePersianLabel),
                    ErrorCodes.ContentRejected);
            }

            return ApiResponse<T>.Forbidden(
                BuildPublicPendingMessage(resourcePersianLabel),
                ErrorCodes.ContentPendingApproval);
        }

        public static string BuildOwnerRejectedMessage(string resourcePersianLabel, string? rejectionReason)
        {
            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                return $"{resourcePersianLabel} تأیید نشد. لطفاً محتوا را اصلاح کنید و دوباره برای تأیید ارسال کنید.";
            }

            return $"{resourcePersianLabel} تأیید نشد. دلیل: {rejectionReason.Trim()} — پس از اصلاح، دوباره برای تأیید ارسال می‌شود.";
        }

        public static string BuildPublicPendingMessage(string resourcePersianLabel) =>
            $"{resourcePersianLabel} هنوز منتشر نشده است. پس از تأیید ادمین، این لینک فعال می‌شود.";

        public static string BuildPublicRejectedMessage(string resourcePersianLabel) =>
            $"{resourcePersianLabel} در دسترس عمومی نیست. لطفاً با سازنده لینک تماس بگیرید.";
    }
}
