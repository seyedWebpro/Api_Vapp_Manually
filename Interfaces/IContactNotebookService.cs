using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Contact;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط سرویس برای مدیریت دفترچه‌های تلفن
    /// </summary>
    public interface IContactNotebookService
    {
        Task<ApiResponse<ContactNotebookResponseDto>> CreateNotebookAsync(int userId, CreateContactNotebookDto createDto);
        Task<ApiResponse<ContactNotebookResponseDto>> GetNotebookByIdAsync(int id, int userId);
        Task<ApiResponse<ContactNotebookListResponseDto>> GetNotebooksAsync(int userId, int pageNumber = 1, int pageSize = 10, bool? isActive = null, string? searchTerm = null);
        Task<ApiResponse<ContactNotebookResponseDto>> UpdateNotebookAsync(int id, int userId, UpdateContactNotebookDto updateDto);
        Task<ApiResponse<bool>> DeleteNotebookAsync(int id, int userId);
        Task<ApiResponse<bool>> ToggleNotebookActiveStatusAsync(int id, int userId, bool isActive);
        Task<ApiResponse<ContactNotebookStatisticsDto>> GetNotebookStatisticsAsync(int id, int userId);
    }
}


