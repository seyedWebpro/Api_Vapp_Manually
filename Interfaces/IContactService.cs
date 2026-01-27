using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Contact;

namespace Api_Vapp.Interfaces
{
    /// <summary>
    /// رابط سرویس برای مدیریت مخاطبین
    /// </summary>
    public interface IContactService
    {
        Task<ApiResponse<ContactResponseDto>> CreateContactAsync(int userId, CreateContactDto createDto);
        Task<ApiResponse<ContactResponseDto>> GetContactByIdAsync(int id, int userId);
        Task<ApiResponse<ContactListResponseDto>> GetContactsAsync(int notebookId, int userId, int pageNumber = 1, int pageSize = 10, string? searchTerm = null);
        Task<ApiResponse<ContactResponseDto>> UpdateContactAsync(int id, int userId, UpdateContactDto updateDto);
        Task<ApiResponse<bool>> DeleteContactAsync(int id, int userId);
        Task<ApiResponse<bool>> TransferContactAsync(int contactId, int fromNotebookId, int toNotebookId, int userId);
        Task<ApiResponse<ImportExcelResultDto>> ImportFromExcelAsync(int userId, ImportContactsFromExcelDto importDto);
        Task<ApiResponse<ImportExcelResultDto>> ImportFromListAsync(int userId, ImportContactsFromListDto importDto);
        Task<ApiResponse<ExportExcelResultDto>> GetImportExcelTemplateAsync();
        Task<ApiResponse<ExportExcelResultDto>> ExportToExcelAsync(int notebookId, int userId, int pageNumber = 1, int pageSize = 10);
        Task<ApiResponse<string>> UploadProfileImageAsync(int contactId, int userId, Microsoft.AspNetCore.Http.IFormFile imageFile);
        
        /// <summary>
        /// آپلود عکس پروفایل مخاطب (بدون نیاز به احراز هویت)
        /// </summary>
        Task<ApiResponse<string>> UploadProfileImageAsync(int contactId, Microsoft.AspNetCore.Http.IFormFile imageFile);
        
        Task<ApiResponse<bool>> DeleteProfileImageAsync(int contactId, int userId);
        Task<ApiResponse<List<string>>> UploadAttachmentFilesAsync(int contactId, int userId, List<Microsoft.AspNetCore.Http.IFormFile> files);
        Task<ApiResponse<bool>> DeleteAttachmentFileAsync(int contactId, int userId, string filePath);
        Task<ApiResponse<List<string>>> GetAttachmentFilesAsync(int contactId, int userId);
        
        /// <summary>
        /// دریافت لیست تمام مخاطبین (بدون نیاز به احراز هویت)
        /// </summary>
        Task<ApiResponse<ContactListResponseDto>> GetAllContactsAsync(int pageNumber = 1, int pageSize = 10, string? searchTerm = null);
        
        /// <summary>
        /// اختصاص تگ‌ها به مخاطب
        /// </summary>
        Task<ApiResponse<bool>> AssignTagsToContactAsync(int contactId, int userId, AssignTagsToContactDto assignDto);
        
        /// <summary>
        /// دریافت لیست دفترچه‌های تلفن یک کاربر
        /// </summary>
        Task<ApiResponse<List<ContactNotebookResponseDto>>> GetUserNotebooksAsync(int userId);
    }
}


