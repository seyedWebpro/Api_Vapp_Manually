using Microsoft.AspNetCore.Http;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// اعتبارسنجی امن آپلود فایل — Content-Type به‌تنهایی کافی نیست (قابل جعل است).
    /// پسوند + امضای باینری (magic bytes) + سقف حجم الزامی است.
    /// </summary>
    public static class SecureFileValidator
    {
        public const long TicketMaxBytes = 8 * 1024 * 1024; // 8 MB
        public const long ProfileImageMaxBytes = 5 * 1024 * 1024;
        public const long ContactImageMaxBytes = 10 * 1024 * 1024;
        public const long ContactAttachmentMaxBytes = 50 * 1024 * 1024;
        public const long IconMaxBytes = 2 * 1024 * 1024;

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

            var contentType = (file.ContentType ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(contentType) ||
                !allowedContentTypes.Any(t => t.Equals(contentType, StringComparison.OrdinalIgnoreCase)))
            {
                return "نوع فایل مجاز نیست";
            }

            if (!ContentTypeToExtensions.TryGetValue(contentType, out var allowedExts) ||
                !allowedExts.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return "پسوند فایل با نوع اعلام‌شده هم‌خوانی ندارد";
            }

            if (!MatchesMagicBytes(file, contentType))
                return "محتوای فایل با نوع اعلام‌شده مطابقت ندارد (احتمال فایل مخرب)";

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

        private static bool MatchesMagicBytes(IFormFile file, string contentType)
        {
            try
            {
                using var stream = file.OpenReadStream();
                if (!stream.CanRead)
                    return false;

                Span<byte> header = stackalloc byte[16];
                var read = stream.Read(header);
                if (read < 4)
                    return false;

                return contentType switch
                {
                    "image/jpeg" or "image/jpg" =>
                        header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,

                    "image/png" =>
                        header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47,

                    "image/gif" =>
                        header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38,

                    "image/webp" =>
                        read >= 12
                        && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                        && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50,

                    "application/pdf" =>
                        header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46, // %PDF

                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" =>
                        header[0] == 0x50 && header[1] == 0x4B, // ZIP/OOXML

                    "application/vnd.ms-excel" =>
                        (header[0] == 0xD0 && header[1] == 0xCF && header[2] == 0x11 && header[3] == 0xE0) // OLE
                        || (header[0] == 0x50 && header[1] == 0x4B), // occasional xlsx mislabeled

                    "video/mp4" or "video/quicktime" =>
                        read >= 8 && header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70, // ftyp

                    "video/x-msvideo" or "video/avi" =>
                        header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46,

                    "audio/mpeg" =>
                        (header[0] == 0xFF && (header[1] & 0xE0) == 0xE0) // frame sync
                        || (header[0] == 0x49 && header[1] == 0x44 && header[2] == 0x33), // ID3

                    "audio/wav" =>
                        header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46,

                    "audio/ogg" =>
                        header[0] == 0x4F && header[1] == 0x67 && header[2] == 0x67 && header[3] == 0x53, // OggS

                    _ => false
                };
            }
            catch
            {
                return false;
            }
        }
    }
}
