using Api_Vapp.DTOs.NumberSeeker;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// کلاینت سطح پایین HTTP به سرویس Python Number Scraper.
    /// </summary>
    public interface INumberScraperClient
    {
        bool IsEnabled { get; }

        Task<NumberSeekerTaskCreatedDto> StartScrapeAsync(
            StartNumberSeekerScrapeDto request,
            CancellationToken cancellationToken = default);

        Task<NumberSeekerTaskStatusDto> GetTaskStatusAsync(
            string taskId,
            CancellationToken cancellationToken = default);

        Task<NumberSeekerCancelResultDto> CancelTaskAsync(
            string taskId,
            CancellationToken cancellationToken = default);

        Task<NumberSeekerHealthDto> GetHealthAsync(
            CancellationToken cancellationToken = default);

        Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    }
}
