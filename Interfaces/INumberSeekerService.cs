using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Contact;
using Api_Vapp.DTOs.NumberSeeker;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// لایه کسب‌وکار شماره‌جو — مالکیت تسک، امنیت، نگاشت پاسخ UI موبایل.
    /// </summary>
    public interface INumberSeekerService
    {
        Task<ApiResponse<NumberSeekerTaskCreatedDto>> StartScrapeAsync(
            int userId,
            StartNumberSeekerScrapeDto request);

        Task<ApiResponse<NumberSeekerTaskStatusDto>> GetTaskStatusAsync(
            int userId,
            string taskId);

        Task<ApiResponse<NumberSeekerCancelResultDto>> CancelTaskAsync(
            int userId,
            string taskId);

        Task<ApiResponse<NumberSeekerTaskListDto>> GetRecentTasksAsync(
            int userId,
            int limit = 20);

        Task<ApiResponse<NumberSeekerHealthDto>> GetHealthAsync();

        ApiResponse<NumberSeekerSourcesDto> GetSources();

        ApiResponse<NumberSeekerCitiesDto> GetCities();

        ApiResponse<NumberSeekerCategoriesDto> GetCategories();

        ApiResponse<NumberSeekerFormMetaDto> GetFormMeta();

        Task<ApiResponse<NumberSeekerExportDto>> ExportPhonesAsync(
            int userId,
            string taskId);

        /// <summary>دانلود فایل اکسل لیست شماره‌ها</summary>
        Task<ApiResponse<ExportExcelResultDto>> ExportPhonesToExcelAsync(
            int userId,
            string taskId);

        Task<ApiResponse<NumberSeekerImportResultDto>> ImportPhonesAsync(
            int userId,
            string taskId,
            ImportNumberSeekerPhonesDto request);

        Task<ApiResponse<bool>> HandleWebhookAsync(NumberSeekerWebhookDto webhook);
    }
}
