namespace Api_Vapp.Interfaces
{
    public interface INumberSeekerRateLimiter
    {
        Task<(bool Allowed, int? RetryAfterSeconds)> CheckScrapeAsync(int userId);

        Task<(bool Allowed, int? RetryAfterSeconds)> CheckImportAsync(int userId);

        Task RecordScrapeAsync(int userId);

        Task RecordImportAsync(int userId);
    }
}
