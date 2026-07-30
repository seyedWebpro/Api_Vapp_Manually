namespace Api_Vapp.Configuration
{
    public class FirebaseOptions
    {
        public const string SectionName = "Firebase";

        /// <summary>
        /// مسیر فایل Service Account JSON (نسبت به ContentRoot یا مطلق)
        /// </summary>
        public string? CredentialsPath { get; set; }

        /// <summary>
        /// محتوای JSON سرویس‌اکانت به‌صورت مستقیم (جایگزین CredentialsPath در env/secret)
        /// </summary>
        public string? CredentialsJson { get; set; }
    }
}
