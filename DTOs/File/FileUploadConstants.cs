namespace Api_Vapp.DTOs.File
{
    /// <summary>
    /// ثابت‌های مربوط به آپلود فایل
    /// برای استانداردسازی نام‌های Entity Types و SubFolders
    /// </summary>
    public static class FileUploadConstants
    {
        #region Entity Types (انواع موجودیت‌ها)

        /// <summary>
        /// مخاطب
        /// </summary>
        public const string EntityType_Contact = "contact";

        /// <summary>
        /// دفترچه مخاطبین
        /// </summary>
        public const string EntityType_ContactNotebook = "contactnotebook";

        /// <summary>
        /// کاربر
        /// </summary>
        public const string EntityType_User = "user";

        /// <summary>
        /// تیکت
        /// </summary>
        public const string EntityType_Ticket = "ticket";

        /// <summary>
        /// فرم ساخته‌شده توسط کاربر (فرم‌ساز)
        /// </summary>
        public const string EntityType_UserForm = "userform";

        /// <summary>
        /// گردونه شانس
        /// </summary>
        public const string EntityType_LuckyWheel = "luckywheel";

        #endregion

        #region SubFolders (پوشه‌های فرعی)

        /// <summary>
        /// عکس پروفایل
        /// </summary>
        public const string SubFolder_Profile = "profile";

        /// <summary>
        /// فایل‌های ضمیمه
        /// </summary>
        public const string SubFolder_Attachments = "attachments";

        /// <summary>
        /// فایل‌های ایمپورت
        /// </summary>
        public const string SubFolder_Imports = "imports";

        /// <summary>
        /// فایل‌های اکسپورت
        /// </summary>
        public const string SubFolder_Exports = "exports";

        /// <summary>
        /// اسناد
        /// </summary>
        public const string SubFolder_Documents = "documents";

        /// <summary>
        /// تصاویر
        /// </summary>
        public const string SubFolder_Images = "images";

        /// <summary>
        /// ویدیوها
        /// </summary>
        public const string SubFolder_Videos = "videos";

        /// <summary>
        /// فایل‌های صوتی
        /// </summary>
        public const string SubFolder_Audios = "audios";

        #endregion
    }
}







