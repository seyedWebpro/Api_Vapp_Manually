namespace Api_Vapp.DTOs.AppVersion
{
    public class AppVersionCheckResponseDto
    {
        /// <summary>none | optional | forced</summary>
        public string UpdateType { get; set; } = "none";

        public string LatestVersion { get; set; } = string.Empty;

        public string MinSupportedVersion { get; set; } = string.Empty;

        public string? StoreUrl { get; set; }

        public string? Title { get; set; }

        public string? Message { get; set; }

        public List<string> Changelog { get; set; } = [];
    }
}
