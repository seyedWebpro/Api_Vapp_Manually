using Api_Vapp.DTOs.File;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Api_Vapp.Services
{
    /// <summary>
    /// سرویس مدیریت آپلود و ذخیره‌سازی فایل‌ها
    /// 
    /// ساختار پوشه‌بندی:
    /// {baseUploadsPath}/{entityType}/{entityId}/{subFolder}/{fileName}
    /// یا اگر subFolder نباشد: {baseUploadsPath}/{entityType}/{entityId}/{fileName}
    /// 
    /// مثال‌ها:
    /// - عکس پروفایل مخاطب: uploads/contact/123/profile/guid_timestamp.jpg
    /// - فایل ضمیمه مخاطب: uploads/contact/123/attachments/guid_timestamp.pdf
    /// - فایل ایمپورت اکسل: uploads/contactnotebook/5/guid_timestamp.xlsx
    /// 
    /// ویژگی‌ها:
    /// - نام فایل‌ها یکتا است (GUID + Timestamp)
    /// - ساختار پوشه‌بندی ساده و واضح
    /// - هر موجودیت فایل‌هایش در پوشه مخصوص خودش است
    /// </summary>
    public class FileUploadService : IFileUploadService
    {
        private readonly FileUploadOptions _options;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileUploadService> _logger;
        private readonly string _baseUploadsPath;

        public FileUploadService(
            IOptions<FileUploadOptions> options,
            IWebHostEnvironment environment,
            ILogger<FileUploadService> logger)
        {
            _options = options.Value;
            _environment = environment;
            _logger = logger;

            // تعیین مسیر کامل پوشه uploads
            if (Path.IsPathRooted(_options.UploadsFolder))
            {
                _baseUploadsPath = _options.UploadsFolder;
            }
            else
            {
                _baseUploadsPath = Path.Combine(_environment.ContentRootPath, _options.UploadsFolder);
            }

            // ایجاد پوشه اگر وجود نداشته باشد
            if (!Directory.Exists(_baseUploadsPath))
            {
                try
                {
                    Directory.CreateDirectory(_baseUploadsPath);
                    _logger.LogInformation("📁 پوشه آپلود ایجاد شد: {UploadsPath}", _baseUploadsPath);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogError(ex, "❌ خطای دسترسی: امکان ایجاد پوشه آپلود {UploadsPath} وجود ندارد. لطفاً دسترسی نوشتن را برای Application Pool Identity تنظیم کنید.", _baseUploadsPath);
                    // خطا را throw نمی‌کنیم تا برنامه اجرا شود، اما در آپلود فایل خطا می‌دهد
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ خطا در ایجاد پوشه آپلود {UploadsPath}", _baseUploadsPath);
                }
            }
            
            // بررسی دسترسی نوشتن
            try
            {
                var testFile = Path.Combine(_baseUploadsPath, ".test");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                _logger.LogInformation("✅ دسترسی نوشتن به پوشه آپلود تأیید شد: {UploadsPath}", _baseUploadsPath);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "⚠️ هشدار: دسترسی نوشتن به پوشه آپلود {UploadsPath} وجود ندارد. لطفاً دسترسی را تنظیم کنید.", _baseUploadsPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ هشدار: امکان بررسی دسترسی به پوشه آپلود {UploadsPath} وجود ندارد.", _baseUploadsPath);
            }
        }

        /// <summary>
        /// آپلود یک فایل با ساختار پوشه‌بندی سازمان‌یافته
        /// ساختار: {entityType}/{entityId}/{subFolder}/{category}/{uniqueFileName}
        /// 
        /// مثال: contact/123/profile/images/a1b2c3d4_20241129120000.jpg
        /// </summary>
        public async Task<string> UploadFileAsync(IFormFile file, string entityType, int entityId, string? subFolder = null)
        {
            _logger.LogDebug("📤 شروع آپلود فایل: {FileName}, EntityType: {EntityType}, EntityId: {EntityId}, SubFolder: {SubFolder}", 
                file?.FileName, entityType, entityId, subFolder ?? "none");

            // Validation
            ValidateFile(file);
            ValidateEntityInfo(entityType, entityId);

            // تولید نام یکتا برای فایل (GUID + Timestamp)
            string fileName = GenerateUniqueFileName(file.FileName);
            
            // ساخت مسیر کامل فایل با ساختار: {entityType}/{entityId}/{subFolder}/{fileName}
            // اگر subFolder نباشد: {entityType}/{entityId}/{fileName}
            string filePath = GenerateFilePath(fileName, entityType, entityId, subFolder);

            // ایجاد پوشه‌ها در صورت نیاز
            string? directory = Path.GetDirectoryName(filePath);
            if (directory != null && !Directory.Exists(directory))
            {
                try
                {
                    Directory.CreateDirectory(directory);
                    _logger.LogDebug("📁 پوشه ایجاد شد: {Directory}", directory);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogError(ex, "❌ خطای دسترسی: امکان ایجاد پوشه {Directory} وجود ندارد. لطفاً دسترسی نوشتن را برای Application Pool Identity تنظیم کنید.", directory);
                    throw new UnauthorizedAccessException($"دسترسی به مسیر '{directory}' امکان‌پذیر نیست. لطفاً با مدیر سیستم تماس بگیرید.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ خطا در ایجاد پوشه {Directory}", directory);
                    throw;
                }
            }

            // آپلود فایل
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                string relativePath = GetRelativePath(filePath);
                _logger.LogInformation("✅ فایل با موفقیت آپلود شد: {RelativePath}", relativePath);

                return relativePath;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "❌ خطای دسترسی: امکان نوشتن فایل در مسیر {FilePath} وجود ندارد.", filePath);
                throw new UnauthorizedAccessException(
                    $"دسترسی به مسیر '{filePath}' امکان‌پذیر نیست. " +
                    "لطفاً دسترسی نوشتن (Write) را برای Application Pool Identity در پوشه wwwroot/uploads تنظیم کنید. " +
                    "برای راهنمایی بیشتر با مدیر سیستم تماس بگیرید.", ex);
            }
            catch (DirectoryNotFoundException ex)
            {
                _logger.LogError(ex, "❌ پوشه یافت نشد: {FilePath}", filePath);
                throw new DirectoryNotFoundException($"پوشه مورد نظر یافت نشد: {filePath}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطا در آپلود فایل: {FileName}", file.FileName);
                throw new InvalidOperationException($"خطا در آپلود فایل: {ex.Message}", ex);
            }
        }

        public async Task<List<string>> UploadMultipleFilesAsync(List<IFormFile> files, string entityType, int entityId, string? subFolder = null)
        {
            _logger.LogDebug("📤 شروع آپلود چند فایل: {Count} فایل, EntityType: {EntityType}, EntityId: {EntityId}",
                files?.Count ?? 0, entityType, entityId);

            if (files == null || !files.Any())
            {
                throw new ArgumentException("هیچ فایلی ارسال نشده است.");
            }

            var fileUrls = new List<string>();
            var errors = new List<string>();

            foreach (var file in files)
            {
                try
                {
                    var relativePath = await UploadFileAsync(file, entityType, entityId, subFolder);
                    fileUrls.Add(relativePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ خطا در آپلود فایل: {FileName}", file.FileName);
                    errors.Add($"{file.FileName}: {ex.Message}");
                }
            }

            if (fileUrls.Count == 0)
            {
                throw new InvalidOperationException($"هیچ فایلی آپلود نشد. خطاها: {string.Join(", ", errors)}");
            }

            if (errors.Any())
            {
                _logger.LogWarning("⚠️ برخی فایل‌ها آپلود نشدند: {Errors}", string.Join(", ", errors));
            }

            _logger.LogInformation("✅ تعداد {Count} فایل از {Total} فایل با موفقیت آپلود شد", 
                fileUrls.Count, files.Count);

            return fileUrls;
        }

        public async Task DeleteFileAsync(string filePath, string entityType, int entityId, string? subFolder = null)
        {
            _logger.LogDebug("🗑️ شروع حذف فایل: {FilePath}, EntityType: {EntityType}, EntityId: {EntityId}",
                filePath, entityType, entityId);

            try
            {
                ValidateEntityInfo(entityType, entityId);

                string fullPath;
                
                // اگر filePath یک مسیر نسبی است
                if (filePath.StartsWith("/") || !Path.IsPathRooted(filePath))
                {
                    // حذف / ابتدایی اگر وجود دارد
                    filePath = filePath.TrimStart('/');

                    // اگر مسیر با uploads/ شروع می‌شود، آن را حذف می‌کنیم چون _baseUploadsPath خودش شامل uploads است
                    if (filePath.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
                    {
                        filePath = filePath.Substring("uploads/".Length);
                    }
                    
                    // اگر فقط نام فایل است، مسیر کامل را بساز
                    if (!filePath.Contains("/"))
                    {
                        fullPath = GenerateFilePath(filePath, entityType, entityId, subFolder);
                    }
                    else
                    {
                        // مسیر نسبی کامل است
                        fullPath = Path.Combine(_baseUploadsPath, filePath.Replace("/", "\\"));
                    }
                }
                else
                {
                    fullPath = filePath;
                }

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    _logger.LogInformation("✅ فایل با موفقیت حذف شد: {FullPath}", fullPath);

                    // حذف پوشه‌های خالی
                    CleanupEmptyDirectories(Path.GetDirectoryName(fullPath));
                }
                else
                {
                    _logger.LogWarning("⚠️ فایل یافت نشد: {FullPath}", fullPath);
                    throw new FileNotFoundException($"فایل پیدا نشد: {filePath}");
                }
            }
            catch (FileNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطا در حذف فایل: {FilePath}", filePath);
                throw new InvalidOperationException($"در هنگام حذف فایل خطایی رخ داده است: {ex.Message}", ex);
            }
        }

        public Task<List<string>> ListFilesAsync(string entityType, int entityId, string? subFolder = null)
        {
            _logger.LogDebug("📋 دریافت لیست فایل‌ها: EntityType: {EntityType}, EntityId: {EntityId}",
                entityType, entityId);

            ValidateEntityInfo(entityType, entityId);

            string entityFolder = Path.Combine(_baseUploadsPath, entityType, entityId.ToString());
            
            if (subFolder != null)
            {
                entityFolder = Path.Combine(entityFolder, subFolder);
            }

            if (!Directory.Exists(entityFolder))
            {
                _logger.LogDebug("📁 پوشه وجود ندارد: {EntityFolder}", entityFolder);
                return Task.FromResult(new List<string>());
            }

            var files = Directory.GetFiles(entityFolder, "*", SearchOption.AllDirectories)
                .Select(GetRelativePath)
                .ToList();

            _logger.LogInformation("✅ تعداد {Count} فایل یافت شد", files.Count);

            return Task.FromResult(files);
        }

        public Task<bool> FileExistsAsync(string filePath, string entityType, int entityId, string? subFolder = null)
        {
            ValidateEntityInfo(entityType, entityId);

            string fullPath;

            if (filePath.StartsWith("/") || !Path.IsPathRooted(filePath))
            {
                filePath = filePath.TrimStart('/');

                // اگر مسیر با uploads/ شروع می‌شود، آن را حذف می‌کنیم چون _baseUploadsPath خودش شامل uploads است
                if (filePath.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
                {
                    filePath = filePath.Substring("uploads/".Length);
                }
                
                if (!filePath.Contains("/"))
                {
                    fullPath = GenerateFilePath(filePath, entityType, entityId, subFolder);
                }
                else
                {
                    fullPath = Path.Combine(_baseUploadsPath, filePath.Replace("/", "\\"));
                }
            }
            else
            {
                fullPath = filePath;
            }

            bool exists = File.Exists(fullPath);
            return Task.FromResult(exists);
        }

        public string GetFileUrl(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return string.Empty;

            // حذف backslash و تبدیل به forward slash
            relativePath = relativePath.Replace("\\", "/").TrimStart('/');

            // اگر BaseUrl تنظیم شده باشد، استفاده کن
            if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
            {
                return $"{_options.BaseUrl.TrimEnd('/')}/{relativePath}";
            }

            // در غیر این صورت از wwwroot استفاده کن
            return $"/{relativePath}";
        }

        public async Task<int> DeleteAllEntityFilesAsync(string entityType, int entityId, string? subFolder = null)
        {
            _logger.LogDebug("🗑️ حذف تمام فایل‌های موجودیت: EntityType: {EntityType}, EntityId: {EntityId}",
                entityType, entityId);

            ValidateEntityInfo(entityType, entityId);

            string entityFolder = Path.Combine(_baseUploadsPath, entityType, entityId.ToString());
            
            if (subFolder != null)
            {
                entityFolder = Path.Combine(entityFolder, subFolder);
            }

            if (!Directory.Exists(entityFolder))
            {
                _logger.LogDebug("📁 پوشه وجود ندارد: {EntityFolder}", entityFolder);
                return 0;
            }

            int deletedCount = 0;
            try
            {
                var files = Directory.GetFiles(entityFolder, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                        deletedCount++;
                        _logger.LogDebug("🗑️ فایل حذف شد: {File}", file);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ خطا در حذف فایل: {File}", file);
                    }
                }

                // حذف پوشه‌های خالی
                CleanupEmptyDirectories(entityFolder);

                _logger.LogInformation("✅ تعداد {Count} فایل از موجودیت {EntityType}/{EntityId} حذف شد",
                    deletedCount, entityType, entityId);

                return deletedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطا در حذف فایل‌های موجودیت {EntityType}/{EntityId}", entityType, entityId);
                throw new InvalidOperationException($"خطا در حذف فایل‌های موجودیت: {ex.Message}", ex);
            }
        }

        public async Task<int> DeleteOldTicketFilesAsync(int daysOld, int? ticketId = null)
        {
            _logger.LogDebug("🗑️ حذف فایل‌های قدیمی تیکت‌ها: قدیمی‌تر از {DaysOld} روز", daysOld);

            if (daysOld < 1)
            {
                throw new ArgumentException("تعداد روزها باید حداقل 1 باشد");
            }

            DateTime cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
            int deletedCount = 0;

            try
            {
                string ticketsFolder = Path.Combine(_baseUploadsPath, "ticket");

                if (!Directory.Exists(ticketsFolder))
                {
                    _logger.LogDebug("📁 پوشه تیکت‌ها وجود ندارد: {TicketsFolder}", ticketsFolder);
                    return 0;
                }

                if (ticketId.HasValue)
                {
                    // حذف فایل‌های یک تیکت خاص
                    string ticketFolder = Path.Combine(ticketsFolder, ticketId.Value.ToString());
                    if (Directory.Exists(ticketFolder))
                    {
                        deletedCount = await DeleteOldFilesInDirectoryAsync(ticketFolder, cutoffDate);
                    }
                }
                else
                {
                    // حذف فایل‌های تمام تیکت‌ها
                    var ticketFolders = Directory.GetDirectories(ticketsFolder);
                    foreach (var ticketFolder in ticketFolders)
                    {
                        int count = await DeleteOldFilesInDirectoryAsync(ticketFolder, cutoffDate);
                        deletedCount += count;
                    }
                }

                // حذف پوشه‌های خالی
                CleanupEmptyDirectories(ticketsFolder);

                _logger.LogInformation("✅ تعداد {Count} فایل قدیمی از تیکت‌ها حذف شد (قدیمی‌تر از {DaysOld} روز)",
                    deletedCount, daysOld);

                return deletedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطا در حذف فایل‌های قدیمی تیکت‌ها");
                throw new InvalidOperationException($"خطا در حذف فایل‌های قدیمی: {ex.Message}", ex);
            }
        }

        #region Helper Methods

        private void ValidateFile(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                throw new ArgumentException("فایل انتخاب نشده است یا خالی است.");
            }

            // بررسی نوع فایل
            var contentType = file.ContentType.ToLower();
            if (!_options.AllowedFileTypes.Any(allowed => 
                allowed.Equals(contentType, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning("⚠️ نوع فایل مجاز نیست: {ContentType}", contentType);
                throw new ArgumentException($"نوع فایل '{contentType}' مجاز نیست. انواع مجاز: {string.Join(", ", _options.AllowedFileTypes)}");
            }

            // بررسی حجم فایل
            if (file.Length > _options.MaxFileSize)
            {
                var maxSizeMB = _options.MaxFileSize / (1024.0 * 1024.0);
                _logger.LogWarning("⚠️ حجم فایل از حد مجاز بیشتر است: {FileSize} bytes, Max: {MaxSize} bytes", 
                    file.Length, _options.MaxFileSize);
                throw new ArgumentException($"حجم فایل ({file.Length / (1024.0 * 1024.0):F2} MB) از حد مجاز ({maxSizeMB:F2} MB) بیشتر است.");
            }

            // بررسی نام فایل
            if (string.IsNullOrWhiteSpace(file.FileName))
            {
                throw new ArgumentException("نام فایل معتبر نیست.");
            }
        }

        private void ValidateEntityInfo(string entityType, int entityId)
        {
            if (string.IsNullOrWhiteSpace(entityType))
            {
                throw new ArgumentException("نوع موجودیت نمی‌تواند خالی باشد.");
            }

            if (entityId <= 0)
            {
                throw new ArgumentException("شناسه موجودیت باید بزرگتر از صفر باشد.");
            }

            // امنیت: بررسی کاراکترهای غیرمجاز در entityType
            var invalidChars = Path.GetInvalidPathChars();
            if (entityType.Any(ch => invalidChars.Contains(ch)))
            {
                throw new ArgumentException("نوع موجودیت شامل کاراکترهای غیرمجاز است.");
            }
        }

        private string GenerateUniqueFileName(string originalFileName)
        {
            string safeFileName = SanitizeFileName(Path.GetFileNameWithoutExtension(originalFileName));
            string extension = Path.GetExtension(originalFileName).ToLower();

            if (string.IsNullOrWhiteSpace(safeFileName))
            {
                safeFileName = "file";
            }

            if (_options.UseOriginalFileName)
            {
                // استفاده از نام اصلی + timestamp + GUID برای یکتا بودن
                string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                string guid = Guid.NewGuid().ToString("N").Substring(0, 8); // 8 کاراکتر اول GUID
                return $"{safeFileName}_{timestamp}_{guid}{extension}";
            }
            else
            {
                // استفاده از GUID + timestamp برای یکتا بودن بیشتر
                string guid = Guid.NewGuid().ToString("N");
                string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                return $"{guid}_{timestamp}{extension}";
            }
        }

        private string GenerateFilePath(string fileName, string entityType, int entityId, string? subFolder = null)
        {
            // ساختار پوشه: {entityType}/{entityId}/{subFolder}/{fileName}
            // مثال: contact/123/profile/guid_timestamp.jpg
            // یا: contactnotebook/5/guid_timestamp.xlsx
            var pathParts = new List<string> { entityType, entityId.ToString() };

            if (!string.IsNullOrWhiteSpace(subFolder))
            {
                // امنیت: sanitize subFolder
                string safeSubFolder = SanitizeFileName(subFolder);
                pathParts.Add(safeSubFolder);
            }
            
            // اضافه کردن نام فایل
            pathParts.Add(fileName);

            return Path.Combine(_baseUploadsPath, Path.Combine(pathParts.ToArray()));
        }

        private string GetRelativePath(string fullPath)
        {
            if (!fullPath.StartsWith(_baseUploadsPath))
            {
                return fullPath;
            }

            string relativePath = fullPath.Substring(_baseUploadsPath.Length);
            // relativePath در اینجا چیزی شبیه contactnotebook/28/images/file.png است
            // طبق نیاز پروژه، می‌خواهیم همیشه با uploads/ شروع شود
            relativePath = relativePath.Replace("\\", "/").TrimStart('/');
            return $"uploads/{relativePath}";
        }

        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "file";

            var invalidChars = Path.GetInvalidFileNameChars().Union(Path.GetInvalidPathChars()).ToArray();
            
            string sanitized = new string(fileName
                .Where(ch => !invalidChars.Contains(ch))
                .ToArray())
                .Replace(" ", "_")
                .Replace("..", "_"); // جلوگیری از path traversal

            // محدود کردن طول
            if (sanitized.Length > 100)
            {
                sanitized = sanitized.Substring(0, 100);
            }

            return sanitized;
        }

        private void CleanupEmptyDirectories(string? directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || !directoryPath.StartsWith(_baseUploadsPath))
                return;

            try
            {
                while (!string.IsNullOrWhiteSpace(directoryPath) && 
                       directoryPath.StartsWith(_baseUploadsPath) &&
                       directoryPath != _baseUploadsPath)
                {
                    if (Directory.Exists(directoryPath))
                    {
                        var files = Directory.GetFiles(directoryPath);
                        var dirs = Directory.GetDirectories(directoryPath);

                        if (files.Length == 0 && dirs.Length == 0)
                        {
                            Directory.Delete(directoryPath);
                            _logger.LogDebug("🗑️ پوشه خالی حذف شد: {Directory}", directoryPath);
                            directoryPath = Path.GetDirectoryName(directoryPath);
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ خطا در حذف پوشه‌های خالی: {Directory}", directoryPath);
            }
        }

        private async Task<int> DeleteOldFilesInDirectoryAsync(string directory, DateTime cutoffDate)
        {
            int deletedCount = 0;

            try
            {
                var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.CreationTimeUtc < cutoffDate || fileInfo.LastWriteTimeUtc < cutoffDate)
                        {
                            File.Delete(file);
                            deletedCount++;
                            _logger.LogDebug("🗑️ فایل قدیمی حذف شد: {File} (تاریخ: {Date})", file, fileInfo.CreationTimeUtc);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ خطا در حذف فایل قدیمی: {File}", file);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ خطا در بررسی فایل‌های قدیمی در پوشه: {Directory}", directory);
            }

            return deletedCount;
        }

        #endregion
    }
}

