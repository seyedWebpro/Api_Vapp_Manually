using Api_Vapp.Constants;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Api_Vapp.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller.Admin
{
    /// <summary>
    /// ارسال Push از پنل ادمین (به‌روزرسانی اپ و ...)
    /// </summary>
    [ApiController]
    [Route("api/Admin/PushNotification")]
    [Authorize(Policy = "AdminOnly")]
    [Produces("application/json")]
    public class PushNotificationController : VappControllerBase
    {
        private readonly IUserPushNotifier _pushNotifier;
        private readonly IPushNotificationService _pushNotificationService;

        public PushNotificationController(
            IUserPushNotifier pushNotifier,
            IPushNotificationService pushNotificationService,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _pushNotifier = pushNotifier;
            _pushNotificationService = pushNotificationService;
        }

        /// <summary>
        /// ارسال اعلان به‌روزرسانی اپ به کاربرانی که دسته Updates را فعال کرده‌اند
        /// </summary>
        [HttpPost("broadcast-app-update")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<object>>> BroadcastAppUpdate(
            [FromBody] BroadcastAppUpdateDto dto,
            CancellationToken cancellationToken)
        {
            var invalid = InvalidModelStateResponse<object>();
            if (invalid != null)
                return invalid;

            if (!_pushNotificationService.TryInitialize())
            {
                return StatusCode(400, ApiResponse<object>.BadRequest(
                    ControlledErrorHelper.PushNotConfigured,
                    errorCode: ErrorCodes.PushNotConfigured));
            }

            var version = dto.Version.Trim();
            var copy = PushNotificationCopy.AppUpdate(version, dto.Notes);
            var usersReached = await _pushNotifier.NotifyBroadcastAsync(
                NotificationCategory.Updates,
                copy.Title,
                copy.Body,
                cancellationToken);

            return Ok(ApiResponse<object>.CreateSuccess(
                new { version, usersReached, title = copy.Title, body = copy.Body },
                usersReached > 0
                    ? $"اعلان به‌روزرسانی به {usersReached} کاربر ارسال شد"
                    : "کاربری با تنظیمات Updates و دستگاه فعال یافت نشد"));
        }

        /// <summary>
        /// ارسال نمونه‌های متن Push (موارد ۱ تا ۱۰) به یک کاربر — برای QA
        /// </summary>
        [HttpPost("preview-samples/{userId:int}")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<object>>> PreviewSamples(
            int userId,
            CancellationToken cancellationToken)
        {
            if (userId <= 0)
            {
                return StatusCode(400, ApiResponse<object>.BadRequest(
                    "شناسه کاربر نامعتبر است",
                    errorCode: ErrorCodes.ValidationFailed));
            }

            if (!_pushNotificationService.TryInitialize())
            {
                return StatusCode(400, ApiResponse<object>.BadRequest(
                    ControlledErrorHelper.PushNotConfigured,
                    errorCode: ErrorCodes.PushNotConfigured));
            }

            var samples = new (NotificationCategory Category, string Title, string Body)[]
            {
                Pack(NotificationCategory.WalletTransaction, PushNotificationCopy.WalletCredited(50_000, 150_000, "شارژ تست")),
                Pack(NotificationCategory.WalletTransaction, PushNotificationCopy.WalletDebited(12_000, 138_000, "ارسال پیامک")),
                Pack(NotificationCategory.NewCustomerRegistration, PushNotificationCopy.NewContact("علی رضایی")),
                Pack(NotificationCategory.CustomerCashback, PushNotificationCopy.CashbackApplied(3, 75_000)),
                Pack(NotificationCategory.ImportantNotifications, PushNotificationCopy.SubscriptionActivated("حرفه‌ای", DateTime.UtcNow.AddDays(30))),
                Pack(NotificationCategory.SystemWarnings, PushNotificationCopy.PaymentFailed()),
                Pack(NotificationCategory.FinancialReport, PushNotificationCopy.FinancialDailyReport(138_000, 50_000, 12_000, 2)),
                Pack(NotificationCategory.Suggestions, PushNotificationCopy.CampaignCompleted("کمپین تست", 18, 1)),
                Pack(NotificationCategory.EducationAndTips, PushNotificationCopy.EducationTip("آموزش ارسال گروهی")),
                Pack(NotificationCategory.Updates, PushNotificationCopy.AppUpdate("2.5.0", "بهبود پایداری اعلان‌ها")),
            };

            var results = new List<object>();
            foreach (var sample in samples)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _pushNotifier.NotifyAsync(
                    userId,
                    sample.Category,
                    sample.Title,
                    sample.Body,
                    cancellationToken);
                results.Add(new { category = sample.Category.ToString(), sample.Title, sample.Body });
            }

            return Ok(ApiResponse<object>.CreateSuccess(
                new { userId, count = results.Count, samples = results },
                $"{results.Count} نمونه اعلان برای کاربر {userId} صف ارسال شد (با رعایت تنظیمات پروفایل)"));
        }

        private static (NotificationCategory Category, string Title, string Body) Pack(
            NotificationCategory category,
            (string Title, string Body) copy) =>
            (category, copy.Title, copy.Body);
    }
}
