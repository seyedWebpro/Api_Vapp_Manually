using Microsoft.AspNetCore.Http;

namespace Api_Vapp.Interfaces
{
    public interface IFileUploadService
    {
        /// <summary>
        /// آپلود یک فایل
        /// </summary>
        /// <param name="file">فایل برای آپلود</param>
        /// <param name="entityType">نوع موجودیت (مثل: user, product, ticket)</param>
        /// <param name="entityId">شناسه موجودیت</param>
        /// <param name="subFolder">پوشه فرعی اختیاری (مثل: profile, images, documents)</param>
        /// <returns>مسیر نسبی فایل آپلود شده</returns>
        Task<string> UploadFileAsync(IFormFile file, string entityType, int entityId, string? subFolder = null);

        /// <summary>
        /// آپلود چند فایل به صورت همزمان
        /// </summary>
        /// <param name="files">لیست فایل‌ها برای آپلود</param>
        /// <param name="entityType">نوع موجودیت</param>
        /// <param name="entityId">شناسه موجودیت</param>
        /// <param name="subFolder">پوشه فرعی اختیاری</param>
        /// <returns>لیست مسیرهای نسبی فایل‌های آپلود شده</returns>
        Task<List<string>> UploadMultipleFilesAsync(List<IFormFile> files, string entityType, int entityId, string? subFolder = null);

        /// <summary>
        /// حذف یک فایل
        /// </summary>
        /// <param name="filePath">مسیر نسبی فایل (یا نام فایل)</param>
        /// <param name="entityType">نوع موجودیت</param>
        /// <param name="entityId">شناسه موجودیت</param>
        /// <param name="subFolder">پوشه فرعی اختیاری</param>
        Task DeleteFileAsync(string filePath, string entityType, int entityId, string? subFolder = null);

        /// <summary>
        /// دریافت لیست فایل‌های یک موجودیت
        /// </summary>
        /// <param name="entityType">نوع موجودیت</param>
        /// <param name="entityId">شناسه موجودیت</param>
        /// <param name="subFolder">پوشه فرعی اختیاری</param>
        /// <returns>لیست مسیرهای نسبی فایل‌ها</returns>
        Task<List<string>> ListFilesAsync(string entityType, int entityId, string? subFolder = null);

        /// <summary>
        /// بررسی وجود فایل
        /// </summary>
        /// <param name="filePath">مسیر نسبی فایل</param>
        /// <param name="entityType">نوع موجودیت</param>
        /// <param name="entityId">شناسه موجودیت</param>
        /// <param name="subFolder">پوشه فرعی اختیاری</param>
        /// <returns>true اگر فایل وجود داشته باشد</returns>
        Task<bool> FileExistsAsync(string filePath, string entityType, int entityId, string? subFolder = null);

        /// <summary>
        /// دریافت URL کامل فایل برای دسترسی از طریق وب
        /// </summary>
        /// <param name="relativePath">مسیر نسبی فایل</param>
        /// <returns>URL کامل</returns>
        string GetFileUrl(string relativePath);

        /// <summary>
        /// حذف تمام فایل‌های یک موجودیت
        /// </summary>
        /// <param name="entityType">نوع موجودیت</param>
        /// <param name="entityId">شناسه موجودیت</param>
        /// <param name="subFolder">پوشه فرعی اختیاری</param>
        /// <returns>تعداد فایل‌های حذف شده</returns>
        Task<int> DeleteAllEntityFilesAsync(string entityType, int entityId, string? subFolder = null);

        /// <summary>
        /// حذف فایل‌های قدیمی تیکت‌ها بر اساس تاریخ (برای جلوگیری از انباشته شدن)
        /// </summary>
        /// <param name="daysOld">فایل‌های قدیمی‌تر از چند روز حذف شوند</param>
        /// <param name="ticketId">شناسه تیکت (اختیاری - اگر null باشد، همه تیکت‌ها)</param>
        /// <returns>تعداد فایل‌های حذف شده</returns>
        Task<int> DeleteOldTicketFilesAsync(int daysOld, int? ticketId = null);
    }
}

