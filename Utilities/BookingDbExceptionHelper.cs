using Api_Vapp.DTOs.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// تبدیل خطاهای دیتابیس به پاسخ کنترل‌شده — بدون افشای جزئیات فنی
    /// </summary>
    public static class BookingDbExceptionHelper
    {
        public static ApiResponse<T> MapDbUpdateException<T>(
            DbUpdateException dbEx,
            ILogger logger,
            string operation,
            int? entityId = null,
            int? userId = null)
        {
            if (IsUniqueConstraintViolation(dbEx))
            {
                logger.LogWarning(
                    dbEx,
                    "Unique constraint while {Operation} — EntityId: {EntityId}, UserId: {UserId}",
                    operation,
                    entityId,
                    userId);

                return ApiResponse<T>.BadRequest(
                    "اطلاعات ارسالی با داده‌های موجود تداخل دارد",
                    errorCode: ErrorCodes.ValidationFailed);
            }

            logger.LogError(
                dbEx,
                "Database error while {Operation} — EntityId: {EntityId}, UserId: {UserId}",
                operation,
                entityId,
                userId);

            return ApiResponse<T>.InternalServerError(
                ControlledErrorHelper.Database,
                ErrorCodes.DatabaseError);
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException dbEx)
        {
            if (dbEx.InnerException is SqlException sqlEx)
            {
                return sqlEx.Number is 2601 or 2627;
            }

            var message = dbEx.InnerException?.Message ?? dbEx.Message;
            return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
        }
    }
}
