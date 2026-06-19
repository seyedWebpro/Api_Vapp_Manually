using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Contact;
using Api_Vapp.DTOs.File;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services
{
    /// <summary>
    /// پیاده‌سازی سرویس مدیریت دفترچه‌های تلفن
    /// </summary>
    public class ContactNotebookService : IContactNotebookService
    {
        private readonly IContactNotebookRepository _notebookRepository;
        private readonly Api_Vapp.Data.Api_Context _context;
        private readonly ILogger<ContactNotebookService> _logger;
        private readonly IFileUploadService _fileUploadService;

        public ContactNotebookService(
            IContactNotebookRepository notebookRepository,
            Api_Vapp.Data.Api_Context context,
            ILogger<ContactNotebookService> logger,
            IFileUploadService fileUploadService)
        {
            _notebookRepository = notebookRepository;
            _context = context;
            _logger = logger;
            _fileUploadService = fileUploadService;
        }

        public async Task<ApiResponse<ContactNotebookResponseDto>> CreateNotebookAsync(int userId, CreateContactNotebookDto createDto)
        {
            try
            {
                // بررسی وجود دفترچه با همین نام برای کاربر
                var exists = await _notebookRepository.ExistsByNameForUserAsync(userId, createDto.Name);
                if (exists)
                {
                    _logger.LogWarning("Attempt to create notebook with existing name: {NotebookName} for user: {UserId}", createDto.Name, userId);
                    return ApiResponse<ContactNotebookResponseDto>.BadRequest("دفترچه‌ای با این نام قبلاً ثبت شده است");
                }

                // اعتبارسنجی آیکون قبل از ایجاد دفترچه
                if (createDto.Icon != null && createDto.Icon.Length > 0)
                {
                    var validationError = ValidateIcon(createDto.Icon);
                    if (validationError != null)
                    {
                        return ApiResponse<ContactNotebookResponseDto>.BadRequest(validationError);
                    }
                }

                // استفاده از Transaction برای اطمینان از یکپارچگی داده‌ها
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // ایجاد دفترچه جدید
                    var notebook = new ContactNotebook
                    {
                        UserId = userId,
                        Name = createDto.Name,
                        Description = createDto.Description,
                        IsActive = createDto.IsActive,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _context.ContactNotebooks.AddAsync(notebook);
                    await _context.SaveChangesAsync();

                    // آپلود آیکون در صورت وجود
                    if (createDto.Icon != null && createDto.Icon.Length > 0)
                    {
                        var relativePath = await _fileUploadService.UploadFileAsync(
                            createDto.Icon, 
                            FileUploadConstants.EntityType_ContactNotebook, 
                            notebook.Id, 
                            FileUploadConstants.SubFolder_Images);

                        // به‌روزرسانی مسیر آیکون در دیتابیس
                        notebook.Icon = relativePath;
                        notebook.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();

                        _logger.LogInformation("آیکون برای دفترچه {NotebookId} با موفقیت آپلود شد", notebook.Id);
                    }

                    // Commit transaction
                    await transaction.CommitAsync();

                    _logger.LogInformation("Contact notebook created successfully with ID: {NotebookId} for user: {UserId}", notebook.Id, userId);

                    // بارگذاری مجدد با آیکون
                    var notebookWithIcon = await _notebookRepository.GetByIdAsync(notebook.Id);

                    return ApiResponse<ContactNotebookResponseDto>.CreateSuccess(
                        await MapToNotebookResponseDtoAsync(notebookWithIcon!),
                        "دفترچه با موفقیت ایجاد شد",
                        201
                    );
                }
                catch (Exception ex)
                {
                    // Rollback transaction در صورت خطا
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "خطا در ایجاد دفترچه برای کاربر: {UserId}", userId);
                    
                    // اگر خطا مربوط به validation فایل بود، پیام واضح‌تری برمی‌گردانیم
                    if (ex is ArgumentException)
                    {
                        return ApiResponse<ContactNotebookResponseDto>.BadRequest(ControlledErrorHelper.SanitizeArgumentMessage(ex.Message, ControlledErrorHelper.InvalidInput));
                    }
                    
                    throw;
                }
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error while creating contact notebook for user: {UserId}", userId);
                return ApiResponse<ContactNotebookResponseDto>.InternalServerError("خطا در ارتباط با پایگاه داده");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating contact notebook for user: {UserId}", userId);
                return ApiResponse<ContactNotebookResponseDto>.InternalServerError("خطای غیرمنتظره در ایجاد دفترچه");
            }
        }

        public async Task<ApiResponse<ContactNotebookResponseDto>> GetNotebookByIdAsync(int id, int userId)
        {
            try
            {
                var notebook = await _notebookRepository.GetByIdAsync(id);

                if (notebook == null)
                {
                    _logger.LogWarning("Contact notebook not found with ID: {NotebookId}", id);
                    return ApiResponse<ContactNotebookResponseDto>.NotFound("دفترچه یافت نشد");
                }

                // بررسی مالکیت
                if (notebook.UserId != userId)
                {
                    _logger.LogWarning("User {UserId} attempted to access notebook {NotebookId} owned by {OwnerId}", userId, id, notebook.UserId);
                    return ApiResponse<ContactNotebookResponseDto>.Forbidden("شما مجاز به دسترسی به این دفترچه نیستید");
                }

                return ApiResponse<ContactNotebookResponseDto>.CreateSuccess(
                    await MapToNotebookResponseDtoAsync(notebook)
                );
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error while getting contact notebook with ID: {NotebookId}", id);
                return ApiResponse<ContactNotebookResponseDto>.InternalServerError("خطا در ارتباط با پایگاه داده");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error getting contact notebook with ID: {NotebookId}", id);
                return ApiResponse<ContactNotebookResponseDto>.InternalServerError("خطای غیرمنتظره در دریافت دفترچه");
            }
        }

        public async Task<ApiResponse<ContactNotebookListResponseDto>> GetNotebooksAsync(int userId, int pageNumber = 1, int pageSize = 10, bool? isActive = null, string? searchTerm = null)
        {
            try
            {
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                var notebooks = await _notebookRepository.GetByUserIdAsync(userId, isActive);
                var notebooksList = notebooks.ToList();

                // اعمال جستجو بر اساس نام یا توضیحات دفترچه (در صورت ارسال searchTerm)
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var normalizedSearch = searchTerm.Trim().ToLower();
                    notebooksList = notebooksList
                        .Where(n =>
                            (!string.IsNullOrEmpty(n.Name) && n.Name.ToLower().Contains(normalizedSearch)) ||
                            (!string.IsNullOrEmpty(n.Description) && n.Description.ToLower().Contains(normalizedSearch)))
                        .ToList();
                }

                var totalCount = notebooksList.Count;
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var pagedNotebooks = notebooksList
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var notebookDtos = new List<ContactNotebookResponseDto>();
                foreach (var notebook in pagedNotebooks)
                {
                    notebookDtos.Add(await MapToNotebookResponseDtoAsync(notebook));
                }

                var response = new ContactNotebookListResponseDto
                {
                    Notebooks = notebookDtos,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                return ApiResponse<ContactNotebookListResponseDto>.CreateSuccess(response);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error while getting contact notebooks list for user: {UserId}", userId);
                return ApiResponse<ContactNotebookListResponseDto>.InternalServerError("خطا در ارتباط با پایگاه داده");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error getting contact notebooks list for user: {UserId}", userId);
                return ApiResponse<ContactNotebookListResponseDto>.InternalServerError("خطای غیرمنتظره در دریافت لیست دفترچه‌ها");
            }
        }

        public async Task<ApiResponse<ContactNotebookResponseDto>> UpdateNotebookAsync(int id, int userId, UpdateContactNotebookDto updateDto)
        {
            try
            {
                var notebook = await _notebookRepository.GetByIdAsync(id);

                if (notebook == null)
                {
                    _logger.LogWarning("Contact notebook not found for update with ID: {NotebookId}", id);
                    return ApiResponse<ContactNotebookResponseDto>.NotFound("دفترچه یافت نشد");
                }

                // بررسی مالکیت
                if (notebook.UserId != userId)
                {
                    return ApiResponse<ContactNotebookResponseDto>.Forbidden("شما مجاز به ویرایش این دفترچه نیستید");
                }

                // به‌روزرسانی فیلدها
                if (!string.IsNullOrWhiteSpace(updateDto.Name))
                {
                    // بررسی تکراری نبودن نام
                    var exists = await _notebookRepository.ExistsByNameForUserAsync(userId, updateDto.Name, id);
                    if (exists)
                    {
                        return ApiResponse<ContactNotebookResponseDto>.BadRequest("دفترچه‌ای با این نام قبلاً ثبت شده است");
                    }
                    notebook.Name = updateDto.Name;
                }

                if (updateDto.Description != null)
                {
                    notebook.Description = updateDto.Description;
                }

                if (updateDto.Icon != null)
                {
                    notebook.Icon = updateDto.Icon;
                }

                if (updateDto.IsActive.HasValue)
                {
                    notebook.IsActive = updateDto.IsActive.Value;
                }

                notebook.UpdatedAt = DateTime.UtcNow;

                var updatedNotebook = await _notebookRepository.UpdateAsync(notebook);

                _logger.LogInformation("Contact notebook updated successfully with ID: {NotebookId}", id);

                return ApiResponse<ContactNotebookResponseDto>.CreateSuccess(
                    await MapToNotebookResponseDtoAsync(updatedNotebook),
                    "اطلاعات دفترچه با موفقیت به‌روزرسانی شد"
                );
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error while updating contact notebook with ID: {NotebookId}", id);
                return ApiResponse<ContactNotebookResponseDto>.InternalServerError("خطا در ارتباط با پایگاه داده");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error updating contact notebook with ID: {NotebookId}", id);
                return ApiResponse<ContactNotebookResponseDto>.InternalServerError("خطای غیرمنتظره در به‌روزرسانی دفترچه");
            }
        }

        public async Task<ApiResponse<bool>> DeleteNotebookAsync(int id, int userId)
        {
            try
            {
                var notebook = await _notebookRepository.GetByIdAsync(id);

                if (notebook == null)
                {
                    _logger.LogWarning("Contact notebook not found for delete with ID: {NotebookId}", id);
                    return ApiResponse<bool>.NotFound("دفترچه یافت نشد");
                }

                // بررسی مالکیت
                if (notebook.UserId != userId)
                {
                    _logger.LogWarning("User {UserId} attempted to delete notebook {NotebookId} owned by {OwnerId}", userId, id, notebook.UserId);
                    return ApiResponse<bool>.Forbidden("شما مجاز به حذف این دفترچه نیستید");
                }

                // بررسی اینکه آیا قبلاً حذف شده است
                if (notebook.IsDeleted)
                {
                    _logger.LogWarning("Contact notebook {NotebookId} is already deleted", id);
                    return ApiResponse<bool>.BadRequest("این دفترچه قبلاً حذف شده است");
                }

                // Soft Delete
                notebook.IsDeleted = true;
                notebook.UpdatedAt = DateTime.UtcNow;
                await _notebookRepository.UpdateAsync(notebook);

                _logger.LogInformation("Contact notebook soft deleted successfully with ID: {NotebookId}", id);

                return ApiResponse<bool>.CreateSuccess(true, "دفترچه با موفقیت حذف شد");
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error while deleting contact notebook with ID: {NotebookId}", id);
                return ApiResponse<bool>.InternalServerError("خطا در ارتباط با پایگاه داده");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error deleting contact notebook with ID: {NotebookId}", id);
                return ApiResponse<bool>.InternalServerError("خطای غیرمنتظره در حذف دفترچه");
            }
        }

        public async Task<ApiResponse<bool>> ToggleNotebookActiveStatusAsync(int id, int userId, bool isActive)
        {
            try
            {
                var notebook = await _notebookRepository.GetByIdAsync(id);

                if (notebook == null)
                {
                    _logger.LogWarning("Contact notebook not found for toggle active status with ID: {NotebookId}", id);
                    return ApiResponse<bool>.NotFound("دفترچه یافت نشد");
                }

                // بررسی مالکیت
                if (notebook.UserId != userId)
                {
                    return ApiResponse<bool>.Forbidden("شما مجاز به تغییر وضعیت این دفترچه نیستید");
                }

                notebook.IsActive = isActive;
                notebook.UpdatedAt = DateTime.UtcNow;
                await _notebookRepository.UpdateAsync(notebook);

                var message = isActive ? "دفترچه با موفقیت فعال شد" : "دفترچه با موفقیت غیرفعال شد";

                _logger.LogInformation("Contact notebook active status toggled to {Status} for ID: {NotebookId}", isActive, id);

                return ApiResponse<bool>.CreateSuccess(true, message);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error while toggling contact notebook active status with ID: {NotebookId}", id);
                return ApiResponse<bool>.InternalServerError("خطا در ارتباط با پایگاه داده");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error toggling contact notebook active status with ID: {NotebookId}", id);
                return ApiResponse<bool>.InternalServerError("خطای غیرمنتظره در تغییر وضعیت دفترچه");
            }
        }

        public async Task<ApiResponse<ContactNotebookStatisticsDto>> GetNotebookStatisticsAsync(int id, int userId)
        {
            try
            {
                var notebook = await _notebookRepository.GetByIdAsync(id);

                if (notebook == null)
                {
                    _logger.LogWarning("Contact notebook not found for statistics with ID: {NotebookId}", id);
                    return ApiResponse<ContactNotebookStatisticsDto>.NotFound("دفترچه یافت نشد");
                }

                // بررسی مالکیت
                if (notebook.UserId != userId)
                {
                    _logger.LogWarning("User {UserId} attempted to access statistics for notebook {NotebookId} owned by {OwnerId}", userId, id, notebook.UserId);
                    return ApiResponse<ContactNotebookStatisticsDto>.Forbidden("شما مجاز به دسترسی به این دفترچه نیستید");
                }

                // شمارش مخاطبین
                var contactsCount = await _context.Contacts
                    .CountAsync(c => c.ContactNotebookId == notebook.Id && !c.IsDeleted);

                // شمارش فایل‌های اکسل ایمپورت شده
                var importedFilesCount = 0;
                try
                {
                    var files = await _fileUploadService.ListFilesAsync(
                        FileUploadConstants.EntityType_ContactNotebook,
                        notebook.Id,
                        null // فایل‌های اکسل بدون subFolder ذخیره می‌شوند
                    );
                    
                    // فیلتر فایل‌های اکسل (.xlsx, .xls)
                    importedFilesCount = files.Count(f => 
                        !string.IsNullOrEmpty(f) && 
                        (f.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || 
                         f.EndsWith(".xls", StringComparison.OrdinalIgnoreCase)));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "خطا در شمارش فایل‌های ایمپورت شده برای دفترچه {NotebookId}", id);
                }

                // پیدا کردن آخرین بروزرسانی
                DateTime? lastUpdateDateTime = notebook.UpdatedAt;
                
                // بررسی آخرین بروزرسانی مخاطبین
                var lastContactUpdate = await _context.Contacts
                    .Where(c => c.ContactNotebookId == notebook.Id && !c.IsDeleted)
                    .Select(c => new { c.UpdatedAt, c.CreatedAt })
                    .ToListAsync();
                
                if (lastContactUpdate.Any())
                {
                    var maxContactUpdate = lastContactUpdate
                        .Select(c => c.UpdatedAt ?? (DateTime?)c.CreatedAt)
                        .Where(d => d.HasValue)
                        .DefaultIfEmpty()
                        .Max();
                    
                    if (maxContactUpdate.HasValue && (!lastUpdateDateTime.HasValue || maxContactUpdate.Value > lastUpdateDateTime.Value))
                    {
                        lastUpdateDateTime = maxContactUpdate.Value;
                    }
                }

                // اگر هنوز بروزرسانی نداشتیم، از CreatedAt استفاده می‌کنیم
                if (!lastUpdateDateTime.HasValue)
                {
                    lastUpdateDateTime = notebook.CreatedAt;
                }

                // تبدیل به زمان نسبی
                string? lastUpdateRelativeTime = null;
                if (lastUpdateDateTime.HasValue)
                {
                    lastUpdateRelativeTime = GetRelativeTime(lastUpdateDateTime.Value);
                }

                var statistics = new ContactNotebookStatisticsDto
                {
                    ImportedFilesCount = importedFilesCount,
                    LastUpdateRelativeTime = lastUpdateRelativeTime,
                    LastUpdateDateTime = lastUpdateDateTime,
                    ContactsCount = contactsCount
                };

                return ApiResponse<ContactNotebookStatisticsDto>.CreateSuccess(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error getting statistics for notebook {NotebookId}", id);
                return ApiResponse<ContactNotebookStatisticsDto>.InternalServerError("خطای غیرمنتظره در دریافت آمار دفترچه");
            }
        }

        /// <summary>
        /// تبدیل DateTime به زمان نسبی (مثل "24 ساعت پیش")
        /// </summary>
        private string GetRelativeTime(DateTime dateTime)
        {
            var now = DateTime.UtcNow;
            var timeSpan = now - dateTime;

            if (timeSpan.TotalSeconds < 60)
            {
                return "همین الان";
            }
            else if (timeSpan.TotalMinutes < 60)
            {
                var minutes = (int)timeSpan.TotalMinutes;
                return $"{minutes} دقیقه پیش";
            }
            else if (timeSpan.TotalHours < 24)
            {
                var hours = (int)timeSpan.TotalHours;
                return $"{hours} ساعت پیش";
            }
            else if (timeSpan.TotalDays < 30)
            {
                var days = (int)timeSpan.TotalDays;
                return $"{days} روز پیش";
            }
            else if (timeSpan.TotalDays < 365)
            {
                var months = (int)(timeSpan.TotalDays / 30);
                return $"{months} ماه پیش";
            }
            else
            {
                var years = (int)(timeSpan.TotalDays / 365);
                return $"{years} سال پیش";
            }
        }

        /// <summary>
        /// اعتبارسنجی فایل آیکون
        /// </summary>
        private string? ValidateIcon(Microsoft.AspNetCore.Http.IFormFile iconFile)
        {
            if (iconFile == null || iconFile.Length == 0)
            {
                return "فایل آیکون انتخاب نشده است";
            }

            // بررسی نوع فایل - فقط عکس مجاز است
            var allowedImageTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
            var contentType = iconFile.ContentType.ToLower();
            
            if (!allowedImageTypes.Contains(contentType))
            {
                return $"نوع فایل '{contentType}' مجاز نیست. فقط فایل‌های تصویری (JPEG, PNG, GIF, WebP) قابل قبول هستند";
            }

            // بررسی حجم فایل (حداکثر 5 مگابایت برای آیکون)
            var maxSize = 5 * 1024 * 1024; // 5 MB
            if (iconFile.Length > maxSize)
            {
                var fileSizeMB = iconFile.Length / (1024.0 * 1024.0);
                return $"حجم فایل ({fileSizeMB:F2} MB) از حد مجاز (5 MB) بیشتر است";
            }

            // بررسی نام فایل
            if (string.IsNullOrWhiteSpace(iconFile.FileName))
            {
                return "نام فایل معتبر نیست";
            }

            return null; // فایل معتبر است
        }

        /// <summary>
        /// تبدیل ContactNotebook به ContactNotebookResponseDto
        /// </summary>
        private async Task<ContactNotebookResponseDto> MapToNotebookResponseDtoAsync(ContactNotebook notebook)
        {
            // شمارش مخاطبین
            var contactsCount = await _context.Contacts
                .CountAsync(c => c.ContactNotebookId == notebook.Id && !c.IsDeleted);

            var responseDto = new ContactNotebookResponseDto
            {
                Id = notebook.Id,
                UserId = notebook.UserId,
                Name = notebook.Name,
                Description = notebook.Description,
                Icon = notebook.Icon,
                IsActive = notebook.IsActive,
                ContactsCount = contactsCount,
                CreatedAt = notebook.CreatedAt,
                UpdatedAt = notebook.UpdatedAt
            };

            // افزودن URL آیکون در صورت وجود
            if (!string.IsNullOrWhiteSpace(notebook.Icon))
            {
                responseDto.IconUrl = _fileUploadService.GetFileUrl(notebook.Icon);
            }

            return responseDto;
        }
    }
}


