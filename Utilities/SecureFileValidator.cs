using Microsoft.AspNetCore.Http;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// اعتبارسنجی امن آپلود فایل — Content-Type به‌تنهایی کافی نیست (قابل جعل است).
    /// پسوند + امضای باینری (magic bytes) + سقف حجم الزامی است.
    /// Content-Type و پسوند اعلام‌شده advisory هستند؛ منبع حقیقت محتوای باینری است
    /// (مثلاً JPEG واقعی با پسوند .png که مرورگر/اسکرین‌شات‌ها اغلب می‌سازند).
    /// </summary>
    public static class SecureFileValidator
    {
        public const long TicketMaxBytes = 8 * 1024 * 1024; // 8 MB
        public const long ProfileImageMaxBytes = 5 * 1024 * 1024;
        public const long ContactImageMaxBytes = 10 * 1024 * 1024;
        public const long ContactAttachmentMaxBytes = 50 * 1024 * 1024;
        public const long IconMaxBytes = 2 * 1024 * 1024;
        public const long VideoMaxBytes = 2L * 1024 * 1024 * 1024; // 2 GB

        private static readonly HashSet<string> DangerousExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".bat", ".cmd", ".com", ".msi", ".scr", ".ps1", ".vbs", ".js", ".jse",
            ".wsf", ".wsh", ".hta", ".jar", ".php", ".phtml", ".asp", ".aspx", ".jsp", ".cgi",
            ".sh", ".bash", ".zsh", ".py", ".rb", ".pl", ".html", ".htm", ".shtml", ".svg",
            ".wasm", ".apk", ".ipa", ".dmg", ".iso", ".bin", ".so", ".dylib"
        };

        public static readonly IReadOnlyDictionary<string, string[]> ContentTypeToExtensions =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["image/jpeg"] = new[] { ".jpg", ".jpeg" },
                ["image/jpg"] = new[] { ".jpg", ".jpeg" },
                ["image/png"] = new[] { ".png" },
                ["image/gif"] = new[] { ".gif" },
                ["image/webp"] = new[] { ".webp" },
                ["application/pdf"] = new[] { ".pdf" },
                ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = new[] { ".xlsx" },
                ["application/vnd.ms-excel"] = new[] { ".xls" },
                ["video/mp4"] = new[] { ".mp4" },
                ["video/quicktime"] = new[] { ".mov" },
                ["video/x-msvideo"] = new[] { ".avi" },
                ["video/avi"] = new[] { ".avi" },
                ["audio/mpeg"] = new[] { ".mp3" },
                ["audio/wav"] = new[] { ".wav" },
                ["audio/ogg"] = new[] { ".ogg" }
            };

        public static readonly string[] ImageContentTypes =
        {
            "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp"
        };

        public static readonly string[] TicketContentTypes =
        {
            "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp", "application/pdf"
        };

        public static readonly string[] ExcelContentTypes =
        {
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/vnd.ms-excel"
        };

        public static readonly string[] VideoContentTypes =
        {
            "video/mp4", "video/quicktime", "video/x-msvideo", "video/avi"
        };

        /// <summary>
        /// اعتبارسنجی کامل فایل. در صورت نامعتبر بودن پیام فارسی برمی‌گرداند؛ در غیر این صورت null.
        /// </summary>
        public static string? Validate(
            IFormFile? file,
            IReadOnlyCollection<string> allowedContentTypes,
            long maxBytes,
            string maxSizeLabelFa)
        {
            if (file == null || file.Length == 0)
                return "فایل انتخاب نشده است یا خالی است";

            if (string.IsNullOrWhiteSpace(file.FileName))
                return "نام فایل معتبر نیست";

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension))
                return "پسوند فایل نامعتبر است";

            if (DangerousExtensions.Contains(extension))
                return "این نوع فایل به دلایل امنیتی مجاز نیست";

            if (file.Length > maxBytes)
            {
                var sizeMb = Math.Round(file.Length / (1024.0 * 1024.0), 2);
                return $"حجم فایل ({sizeMb} مگابایت) بیشتر از حد مجاز ({maxSizeLabelFa}) است";
            }

            var actualType = DetectContentTypeFromMagic(file);
            if (actualType == null)
                return "محتوای فایل با نوع اعلام‌شده مطابقت ندارد (احتمال فایل مخرب)";

            if (!IsAllowedContentType(actualType, allowedContentTypes))
                return "نوع فایل مجاز نیست";

            // پسوند باید متعلق به همان خانوادهٔ مجاز باشد (مثلاً تصویر→تصویر)،
            // نه لزوماً دقیقاً همان نوع اعلام‌شده — JPEG با پسوند .png رایج است.
            if (!IsExtensionCompatible(extension, actualType, allowedContentTypes))
                return "پسوند فایل با نوع اعلام‌شده هم‌خوانی ندارد";

            return null;
        }

        public static string? ValidateTicketAttachment(IFormFile? file) =>
            Validate(file, TicketContentTypes, TicketMaxBytes, "۸ مگابایت");

        public static string? ValidateImage(
            IFormFile? file,
            long maxBytes,
            string maxSizeLabelFa) =>
            Validate(file, ImageContentTypes, maxBytes, maxSizeLabelFa);

        public static string? ValidateExcel(IFormFile? file, long maxBytes = ContactAttachmentMaxBytes)
        {
            var maxMb = Math.Max(1, (int)Math.Round(maxBytes / (1024.0 * 1024.0)));
            return Validate(file, ExcelContentTypes, maxBytes, $"{maxMb} مگابایت");
        }

        public static string? ValidateVideo(IFormFile? file, long maxBytes = VideoMaxBytes)
        {
            var maxGb = Math.Max(1, (int)Math.Round(maxBytes / (1024.0 * 1024.0 * 1024.0)));
            return Validate(file, VideoContentTypes, maxBytes, $"{maxGb} گیگابایت");
        }

        /// <summary>
        /// پسوند مناسب بر اساس محتوای واقعی فایل (برای ذخیرهٔ صحیح روی دیسک).
        /// </summary>
        public static string? GetPreferredExtension(IFormFile file)
        {
            var actualType = DetectContentTypeFromMagic(file);
            if (actualType == null)
                return null;

            if (ContentTypeToExtensions.TryGetValue(actualType, out var exts) && exts.Length > 0)
                return exts[0];

            return null;
        }

        private static bool IsAllowedContentType(string contentType, IReadOnlyCollection<string> allowed)
        {
            if (allowed.Any(t => t.Equals(contentType, StringComparison.OrdinalIgnoreCase)))
                return true;

            // image/jpg و image/jpeg را معادل بگیر
            if (contentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)
                && allowed.Any(t => t.Equals("image/jpg", StringComparison.OrdinalIgnoreCase)))
                return true;
            if (contentType.Equals("image/jpg", StringComparison.OrdinalIgnoreCase)
                && allowed.Any(t => t.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)))
                return true;

            return false;
        }

        private static bool IsExtensionCompatible(
            string extension,
            string actualType,
            IReadOnlyCollection<string> allowedContentTypes)
        {
            if (ContentTypeToExtensions.TryGetValue(actualType, out var exactExts)
                && exactExts.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            var slash = actualType.IndexOf('/');
            if (slash <= 0)
                return false;

            var category = actualType[..slash]; // image / video / audio / application
            foreach (var kv in ContentTypeToExtensions)
            {
                if (!IsAllowedContentType(kv.Key, allowedContentTypes))
                    continue;

                var keySlash = kv.Key.IndexOf('/');
                if (keySlash <= 0)
                    continue;

                if (!kv.Key.AsSpan(0, keySlash).Equals(category, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (kv.Value.Contains(extension, StringComparer.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>تشخیص Content-Type واقعی از امضای باینری؛ در صورت ناشناخته بودن null.</summary>
        public static string? DetectContentTypeFromMagic(IFormFile file)
        {
            try
            {
                var stream = file.OpenReadStream();
                if (!stream.CanRead)
                    return null;

                long? originalPosition = stream.CanSeek ? stream.Position : null;
                if (stream.CanSeek)
                    stream.Position = 0;

                Span<byte> header = stackalloc byte[16];
                var read = stream.Read(header);

                if (originalPosition.HasValue)
                    stream.Position = originalPosition.Value;

                if (read < 4)
                    return null;

                if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
                    return "image/jpeg";

                if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
                    return "image/png";

                if (header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38)
                    return "image/gif";

                if (read >= 12
                    && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                    && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
                    return "image/webp";

                if (header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46)
                    return "application/pdf";

                if (header[0] == 0x50 && header[1] == 0x4B)
                    return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

                if (header[0] == 0xD0 && header[1] == 0xCF && header[2] == 0x11 && header[3] == 0xE0)
                    return "application/vnd.ms-excel";

                if (read >= 8 && header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70)
                    return "video/mp4";

                if (read >= 12
                    && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                    && header[8] == 0x41 && header[9] == 0x56 && header[10] == 0x49 && header[11] == 0x20)
                    return "video/avi";

                if ((header[0] == 0xFF && (header[1] & 0xE0) == 0xE0)
                    || (header[0] == 0x49 && header[1] == 0x44 && header[2] == 0x33))
                    return "audio/mpeg";

                if (header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46)
                    return "audio/wav";

                if (header[0] == 0x4F && header[1] == 0x67 && header[2] == 0x67 && header[3] == 0x53)
                    return "audio/ogg";

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
