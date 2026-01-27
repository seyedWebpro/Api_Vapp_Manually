using System.Collections.Generic;

namespace Api_Vapp.DTOs.File
{
    public class FileUploadOptions
    {
        public string UploadsFolder { get; set; } = "wwwroot/uploads";
        public long MaxFileSize { get; set; } = 500 * 1024 * 1024; // 500 MB
        public List<string> AllowedFileTypes { get; set; } = new List<string>
        {
            "image/jpeg", "image/png", "image/gif", "image/webp", "image/jpg",
            "video/mp4", "video/quicktime", "video/x-msvideo", "video/avi",
            "audio/mpeg", "audio/wav", "audio/ogg",
            "application/pdf",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", // .xlsx
            "application/vnd.ms-excel" // .xls
        };
        public bool UseOriginalFileName { get; set; } = false;
        public string BaseUrl { get; set; } = ""; // برای تولید URL کامل (مثل https://example.com)
    }
}

