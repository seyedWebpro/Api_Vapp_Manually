using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Contact;
using Api_Vapp.DTOs.File;
using Api_Vapp.DTOs.Message;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Utilities;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Api_Vapp.Services
{
    /// <summary>
    /// پیاده‌سازی سرویس مدیریت مخاطبین
    /// </summary>
    public class ContactService : IContactService
    {
        private readonly IContactRepository _contactRepository;
        private readonly IContactNotebookRepository _notebookRepository;
        private readonly Api_Vapp.Data.Api_Context _context;
        private readonly ILogger<ContactService> _logger;
        private readonly IFileUploadService _fileUploadService;

        public ContactService(
            IContactRepository contactRepository,
            IContactNotebookRepository notebookRepository,
            Api_Vapp.Data.Api_Context context,
            ILogger<ContactService> logger,
            IFileUploadService fileUploadService)
        {
            _contactRepository = contactRepository;
            _notebookRepository = notebookRepository;
            _context = context;
            _logger = logger;
            _fileUploadService = fileUploadService;
        }

        public async Task<ApiResponse<ContactResponseDto>> CreateContactAsync(int userId, CreateContactDto createDto)
        {
            try
            {
                // بررسی وجود دفترچه و مالکیت
                var notebook = await _notebookRepository.GetByIdAsync(createDto.ContactNotebookId);
                if (notebook == null)
                {
                    return ApiResponse<ContactResponseDto>.NotFound("دفترچه یافت نشد");
                }

                if (notebook.UserId != userId)
                {
                    return ApiResponse<ContactResponseDto>.Forbidden("شما مجاز به افزودن مخاطب به این دفترچه نیستید");
                }

                // بررسی تکراری بودن شماره موبایل در دفترچه
                // اگر تکراری بود، مخاطب موجود را برمی‌گردانیم (بدون خطا)
                var existingContact = await _context.Contacts
                    .Where(c => c.ContactNotebookId == createDto.ContactNotebookId 
                        && c.MobileNumber == createDto.MobileNumber 
                        && !c.IsDeleted)
                    .FirstOrDefaultAsync();

                if (existingContact != null)
                {
                    _logger.LogInformation("مخاطب با شماره موبایل {MobileNumber} در دفترچه {NotebookId} قبلاً وجود دارد. مخاطب موجود برگردانده می‌شود.", 
                        createDto.MobileNumber, createDto.ContactNotebookId);
                    
                    // بارگذاری مجدد با اطلاعات تکمیلی
                    var contactWithInfo = await _contactRepository.GetByIdWithAdditionalInfoAsync(existingContact.Id);
                    
                    return ApiResponse<ContactResponseDto>.CreateSuccess(
                        await MapToContactResponseDtoAsync(contactWithInfo!),
                        "مخاطب قبلاً در این دفترچه ثبت شده است",
                        200
                    );
                }

                // استفاده از Transaction برای اطمینان از یکپارچگی داده‌ها
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // ایجاد مخاطب جدید
                    var contact = new Contact
                    {
                        ContactNotebookId = createDto.ContactNotebookId,
                        MobileNumber = createDto.MobileNumber,
                        FullName = createDto.FullName,
                        Brand = createDto.Brand,
                        Tags = null, // فیلد Tags دیگر استفاده نمی‌شود، از TagNames استفاده می‌شود
                        CreatedAt = DateTime.UtcNow
                    };

                    await _context.Contacts.AddAsync(contact);
                    await _context.SaveChangesAsync();

                    // ایجاد اطلاعات تکمیلی در صورت وجود
                    if (!string.IsNullOrWhiteSpace(createDto.CustomFields) || createDto.DateOfBirth.HasValue)
                    {
                        var additionalInfo = new ContactAdditionalInfo
                        {
                            ContactId = contact.Id,
                            // تبدیل تاریخ تولد به UTC - فقط تاریخ مهم است
                            DateOfBirth = createDto.DateOfBirth.EnsureDateOnlyUtc(),
                            CustomFields = createDto.CustomFields,
                            CreatedAt = DateTime.UtcNow
                        };

                        await _context.ContactAdditionalInfos.AddAsync(additionalInfo);
                        await _context.SaveChangesAsync();
                    }

                    // افزودن مناسبت‌ها
                    if (createDto.Occasions != null && createDto.Occasions.Any())
                    {
                        var occasions = createDto.Occasions.Select(o => new ContactOccasion
                        {
                            ContactId = contact.Id,
                            Title = o.Title,
                            // تبدیل تاریخ مناسبت به UTC
                            Date = o.Date.EnsureUtc(),
                            HasTime = o.HasTime
                        }).ToList();

                        await _context.ContactOccasions.AddRangeAsync(occasions);
                        await _context.SaveChangesAsync();
                    }

                    // پردازش تگ‌ها (ایجاد یا استفاده خودکار)
                    if (createDto.TagNames != null && createDto.TagNames.Any())
                    {
                        // Normalize کردن نام تگ‌ها
                        var normalizedTagNames = createDto.TagNames
                            .Where(n => !string.IsNullOrWhiteSpace(n))
                            .Select(n => n.Trim())
                            .Where(n => !string.IsNullOrEmpty(n) && n.Length <= 100)
                            .Distinct()
                            .ToList();

                        if (normalizedTagNames.Any())
                        {
                            _logger.LogInformation("Processing {Count} tag names for user {UserId}: {TagNames}", 
                                normalizedTagNames.Count, userId, string.Join(", ", normalizedTagNames));
                            
                            // بررسی اینکه userId معتبر است
                            var userExists = await _context.Users.AnyAsync(u => u.Id == userId && !u.IsDeleted);
                            if (!userExists)
                            {
                                _logger.LogError("کاربر {UserId} وجود ندارد یا حذف شده!", userId);
                                return ApiResponse<ContactResponseDto>.BadRequest($"کاربر با شناسه {userId} یافت نشد");
                            }

                            // دریافت تگ‌های موجود برای کاربر (فقط فعال)
                            // استفاده از ToLower برای مقایسه case-insensitive (برای سازگاری با SQL Server)
                            var existingTags = await _context.MessageTags
                                .Where(t => t.UserId == userId 
                                    && !t.IsDeleted)
                                .ToListAsync();

                            // فیلتر در حافظه با مقایسه case-insensitive
                            existingTags = existingTags
                                .Where(t => normalizedTagNames.Any(n => 
                                    string.Equals(t.Name, n, StringComparison.OrdinalIgnoreCase)))
                                .ToList();

                            _logger.LogInformation("Found {Count} existing tags for user {UserId}", existingTags.Count, userId);

                            var existingTagNames = existingTags.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                            var tagsToCreate = normalizedTagNames
                                .Where(n => !existingTagNames.Contains(n))
                                .ToList();

                            _logger.LogInformation("Will create {Count} new tags for user {UserId}", tagsToCreate.Count, userId);

                            // ایجاد تگ‌های جدید
                            var newTags = new List<MessageTag>();
                            foreach (var tagName in tagsToCreate)
                            {
                                try
                                {
                                    var newTag = new MessageTag
                                    {
                                        UserId = userId,
                                        Name = tagName,
                                        IsActive = true,
                                        CreatedAt = DateTime.UtcNow
                                    };

                                    await _context.MessageTags.AddAsync(newTag);
                                    await _context.SaveChangesAsync(); // Save برای گرفتن ID

                                    existingTags.Add(newTag);
                                }
                                catch (DbUpdateException ex)
                                {
                                    _logger.LogWarning(ex, "Database error creating tag '{TagName}' for user {UserId}. Trying to find existing tag.", tagName, userId);
                                    
                                    // Race condition: تگ توسط request دیگر ایجاد شده
                                    // جستجوی دوباره و استفاده از تگ موجود (case-insensitive)
                                    var allUserTags = await _context.MessageTags
                                        .Where(t => t.UserId == userId && !t.IsDeleted)
                                        .ToListAsync();

                                    var tag = allUserTags
                                        .FirstOrDefault(t => string.Equals(t.Name, tagName, StringComparison.OrdinalIgnoreCase));

                                    if (tag != null)
                                    {
                                        _logger.LogInformation("Found existing tag '{TagName}' (ID: {TagId}) for user {UserId}", tag.Name, tag.Id, userId);
                                        existingTags.Add(tag);
                                    }
                                    else
                                    {
                                        _logger.LogError(ex, "Failed to create tag '{TagName}' for user {UserId} and it doesn't exist after retry", tagName, userId);
                                        // اگر تگ پیدا نشد، آن را نادیده می‌گیریم و ادامه می‌دهیم
                                    }
                                }
                            }

                            // دریافت ContactTag های موجود (برای جلوگیری از duplicate)
                            var existingContactTags = await _context.ContactTags
                                .Where(ct => ct.ContactId == contact.Id 
                                    && existingTags.Select(t => t.Id).Contains(ct.TagId))
                                .Select(ct => ct.TagId)
                                .ToHashSetAsync();

                            // ایجاد ContactTag های جدید
                            var contactTagsToAdd = existingTags
                                .Where(t => !existingContactTags.Contains(t.Id))
                                .Select(t => new ContactTag
                                {
                                    ContactId = contact.Id,
                                    TagId = t.Id,
                                    CreatedAt = DateTime.UtcNow
                                })
                                .ToList();

                            if (contactTagsToAdd.Any())
                            {
                                _logger.LogInformation("Adding {Count} ContactTags for contact {ContactId}", 
                                    contactTagsToAdd.Count, contact.Id);
                                
                                await _context.ContactTags.AddRangeAsync(contactTagsToAdd);
                                await _context.SaveChangesAsync();
                                
                                _logger.LogInformation("Successfully added ContactTags for contact {ContactId}", contact.Id);
                            }
                            else
                            {
                                _logger.LogInformation("No new ContactTags to add for contact {ContactId}", contact.Id);
                            }
                        }
                    }

                    // Commit transaction
                    await transaction.CommitAsync();
                    
                    _logger.LogInformation("Transaction committed successfully for contact {ContactId}", contact.Id);

                    // به‌روزرسانی تاریخ ویرایش دفترچه
                    notebook.UpdatedAt = DateTime.UtcNow;
                    await _notebookRepository.UpdateAsync(notebook);

                    // بررسی نهایی: اطمینان از اینکه تگ‌ها ذخیره شده‌اند
                    if (createDto.TagNames != null && createDto.TagNames.Any())
                    {
                        var finalContactTags = await _context.ContactTags
                            .Where(ct => ct.ContactId == contact.Id)
                            .CountAsync();
                        
                        _logger.LogInformation("Contact {ContactId} has {TagCount} tags after commit", 
                            contact.Id, finalContactTags);
                    }

                    _logger.LogInformation("Contact created successfully with ID: {ContactId} in notebook: {NotebookId}", 
                        contact.Id, createDto.ContactNotebookId);

                    // بارگذاری مجدد با اطلاعات تکمیلی
                    var contactWithInfo = await _contactRepository.GetByIdWithAdditionalInfoAsync(contact.Id);

                    return ApiResponse<ContactResponseDto>.CreateSuccess(
                        await MapToContactResponseDtoAsync(contactWithInfo!),
                        "مخاطب با موفقیت ایجاد شد",
                        201
                    );
                }
                catch (Exception ex)
                {
                    // Rollback transaction در صورت خطا
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "خطا در ایجاد مخاطب در دفترچه: {NotebookId}", createDto.ContactNotebookId);
                    
                    if (ex is ArgumentException)
                    {
                        var errorMessage = ex.Message.Contains("الزامی") || ex.Message.Contains("صحیح نیست") || 
                                           ex.Message.Contains("نمی‌تواند") || ex.Message.Contains("باید") 
                                           ? ex.Message 
                                           : "اطلاعات وارد شده نامعتبر است";
                        return ApiResponse<ContactResponseDto>.BadRequest(errorMessage);
                    }
                    
                    // هندل کردن خطاهای دیتابیس
                    if (ex is Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
                    {
                        var errorMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                        
                        // تبدیل خطاهای انگلیسی به فارسی
                        if (errorMessage.Contains("cannot insert null") || errorMessage.Contains("cannot be null"))
                        {
                            if (errorMessage.Contains("FullName", StringComparison.OrdinalIgnoreCase))
                            {
                                return ApiResponse<ContactResponseDto>.BadRequest("نام خانوادگی الزامی است");
                            }
                            return ApiResponse<ContactResponseDto>.BadRequest("برخی فیلدهای الزامی وارد نشده‌اند");
                        }
                        
                        if (errorMessage.Contains("unique") || errorMessage.Contains("duplicate"))
                        {
                            if (errorMessage.Contains("MobileNumber", StringComparison.OrdinalIgnoreCase))
                            {
                                // اگر خطای unique constraint رخ داد، مخاطب موجود را پیدا و برگردان
                                _logger.LogWarning("خطای unique constraint برای شماره موبایل {MobileNumber} در دفترچه {NotebookId}. در حال جستجوی مخاطب موجود...", 
                                    createDto.MobileNumber, createDto.ContactNotebookId);
                                
                                var duplicateContact = await _context.Contacts
                                    .Where(c => c.ContactNotebookId == createDto.ContactNotebookId 
                                        && c.MobileNumber == createDto.MobileNumber 
                                        && !c.IsDeleted)
                                    .FirstOrDefaultAsync();
                                
                                if (duplicateContact != null)
                                {
                                    var contactWithInfo = await _contactRepository.GetByIdWithAdditionalInfoAsync(duplicateContact.Id);
                                    return ApiResponse<ContactResponseDto>.CreateSuccess(
                                        await MapToContactResponseDtoAsync(contactWithInfo!),
                                        "مخاطب قبلاً در این دفترچه ثبت شده است",
                                        200
                                    );
                                }
                            }
                            // برای سایر خطاهای unique، خطا برمی‌گردانیم
                            return ApiResponse<ContactResponseDto>.BadRequest("اطلاعات تکراری است");
                        }
                        
                        return ApiResponse<ContactResponseDto>.BadRequest("خطا در ذخیره اطلاعات");
                    }
                    
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating contact in notebook: {NotebookId}", createDto.ContactNotebookId);
                throw;
            }
        }

        public async Task<ApiResponse<ContactResponseDto>> GetContactByIdAsync(int id, int userId)
        {
            try
            {
                var contact = await _contactRepository.GetByIdWithAdditionalInfoAsync(id);

                if (contact == null)
                {
                    _logger.LogWarning("Contact not found with ID: {ContactId}", id);
                    return ApiResponse<ContactResponseDto>.NotFound("مخاطب یافت نشد");
                }

                // بررسی مالکیت دفترچه
                var notebook = await _notebookRepository.GetByIdAsync(contact.ContactNotebookId);
                if (notebook == null || notebook.UserId != userId)
                {
                    return ApiResponse<ContactResponseDto>.Forbidden("شما مجاز به دسترسی به این مخاطب نیستید");
                }

                return ApiResponse<ContactResponseDto>.CreateSuccess(
                    await MapToContactResponseDtoAsync(contact)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting contact with ID: {ContactId}", id);
                throw;
            }
        }

        public async Task<ApiResponse<ContactListResponseDto>> GetContactsAsync(
            int notebookId, 
            int userId, 
            int pageNumber = 1, 
            int pageSize = 10, 
            string? searchTerm = null)
        {
            try
            {
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                // بررسی وجود دفترچه و مالکیت
                var notebook = await _notebookRepository.GetByIdAsync(notebookId);
                if (notebook == null)
                {
                    return ApiResponse<ContactListResponseDto>.NotFound("دفترچه یافت نشد");
                }

                if (notebook.UserId != userId)
                {
                    return ApiResponse<ContactListResponseDto>.Forbidden("شما مجاز به دسترسی به این دفترچه نیستید");
                }

                // دریافت مخاطبین
                IEnumerable<Contact> contacts;
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    contacts = await _contactRepository.SearchAsync(notebookId, searchTerm);
                }
                else
                {
                    contacts = await _contactRepository.GetByNotebookIdAsync(notebookId);
                }

                var contactsList = contacts.ToList();
                var totalCount = contactsList.Count;
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var pagedContacts = contactsList
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var contactDtos = new List<ContactResponseDto>();
                foreach (var contact in pagedContacts)
                {
                    contactDtos.Add(await MapToContactResponseDtoAsync(contact));
                }

                // دریافت تعداد فایل‌های ایمپورت شده
                var importedFiles = await _fileUploadService.ListFilesAsync(
                    FileUploadConstants.EntityType_ContactNotebook, 
                    notebookId);

                var response = new ContactListResponseDto
                {
                    Contacts = contactDtos,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages,
                    LastUpdatedAt = notebook.UpdatedAt ?? notebook.CreatedAt,
                    ImportedFileCount = importedFiles.Count
                };

                return ApiResponse<ContactListResponseDto>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting contacts list for notebook: {NotebookId}", notebookId);
                throw;
            }
        }

        public async Task<ApiResponse<ContactListResponseDto>> GetAllContactsAsync(
            int pageNumber = 1, 
            int pageSize = 10, 
            string? searchTerm = null)
        {
            try
            {
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                // دریافت تمام مخاطبین
                IEnumerable<Contact> contacts;
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    contacts = await _contactRepository.SearchAllContactsAsync(searchTerm);
                }
                else
                {
                    contacts = await _contactRepository.GetAllContactsAsync();
                }

                var contactsList = contacts.ToList();
                var totalCount = contactsList.Count;
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var pagedContacts = contactsList
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var contactDtos = new List<ContactResponseDto>();
                foreach (var contact in pagedContacts)
                {
                    contactDtos.Add(await MapToContactResponseDtoAsync(contact));
                }

                var response = new ContactListResponseDto
                {
                    Contacts = contactDtos,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages,
                    LastUpdatedAt = DateTime.UtcNow,
                    ImportedFileCount = 0
                };

                return ApiResponse<ContactListResponseDto>.CreateSuccess(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all contacts list");
                throw;
            }
        }

        public async Task<ApiResponse<ContactResponseDto>> UpdateContactAsync(int id, int userId, UpdateContactDto updateDto)
        {
            try
            {
                var contact = await _contactRepository.GetByIdWithAdditionalInfoAsync(id);

                if (contact == null)
                {
                    _logger.LogWarning("Contact not found for update with ID: {ContactId}", id);
                    return ApiResponse<ContactResponseDto>.NotFound("مخاطب یافت نشد");
                }

                // بررسی مالکیت دفترچه
                var notebook = await _notebookRepository.GetByIdAsync(contact.ContactNotebookId);
                if (notebook == null || notebook.UserId != userId)
                {
                    return ApiResponse<ContactResponseDto>.Forbidden("شما مجاز به ویرایش این مخاطب نیستید");
                }

                // به‌روزرسانی فیلدها
                if (!string.IsNullOrWhiteSpace(updateDto.MobileNumber))
                {
                    // بررسی تکراری بودن شماره موبایل
                    // اگر تکراری بود، شماره را تغییر نمی‌دهیم (بدون خطا)
                    var exists = await _contactRepository.ExistsByMobileNumberInNotebookAsync(
                        contact.ContactNotebookId, 
                        updateDto.MobileNumber, 
                        id);
                    if (exists)
                    {
                        _logger.LogInformation("شماره موبایل {MobileNumber} در دفترچه {NotebookId} تکراری است. شماره تغییر داده نمی‌شود.", 
                            updateDto.MobileNumber, contact.ContactNotebookId);
                        // شماره را تغییر نمی‌دهیم، اما سایر فیلدها را به‌روزرسانی می‌کنیم
                    }
                    else
                    {
                        contact.MobileNumber = updateDto.MobileNumber;
                    }
                }

                if (updateDto.FullName != null)
                {
                    contact.FullName = updateDto.FullName;
                }

                if (updateDto.Brand != null)
                {
                    contact.Brand = updateDto.Brand;
                }

                if (updateDto.Tags != null)
                {
                    contact.Tags = updateDto.Tags; // فیلد قدیمی - برای سازگاری
                }

                contact.UpdatedAt = DateTime.UtcNow;

                var updatedContact = await _contactRepository.UpdateAsync(contact);

                // پردازش تگ‌ها (ایجاد یا استفاده خودکار)
                if (updateDto.TagNames != null)
                {
                    // حذف تگ‌های قبلی
                    var existingContactTags = await _context.ContactTags
                        .Where(ct => ct.ContactId == contact.Id)
                        .ToListAsync();
                    
                    _context.ContactTags.RemoveRange(existingContactTags);
                    await _context.SaveChangesAsync();

                    // Normalize کردن نام تگ‌ها
                    var normalizedTagNames = updateDto.TagNames
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Select(n => n.Trim())
                        .Where(n => !string.IsNullOrEmpty(n) && n.Length <= 100)
                        .Distinct()
                        .ToList();

                    if (normalizedTagNames.Any())
                    {
                        // دریافت تگ‌های موجود برای کاربر (فقط فعال)
                        var allUserTags = await _context.MessageTags
                            .Where(t => t.UserId == userId && !t.IsDeleted)
                            .ToListAsync();

                        // فیلتر در حافظه با مقایسه case-insensitive
                        var existingTags = allUserTags
                            .Where(t => normalizedTagNames.Any(n => 
                                string.Equals(t.Name, n, StringComparison.OrdinalIgnoreCase)))
                            .ToList();

                        var existingTagNames = existingTags.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                        var tagsToCreate = normalizedTagNames
                            .Where(n => !existingTagNames.Contains(n))
                            .ToList();

                        // ایجاد تگ‌های جدید
                        foreach (var tagName in tagsToCreate)
                        {
                            try
                            {
                                var newTag = new MessageTag
                                {
                                    UserId = userId,
                                    Name = tagName,
                                    IsActive = true,
                                    CreatedAt = DateTime.UtcNow
                                };

                                await _context.MessageTags.AddAsync(newTag);
                                await _context.SaveChangesAsync();

                                existingTags.Add(newTag);
                            }
                            catch (DbUpdateException ex)
                            {
                                // Race condition: تگ توسط request دیگر ایجاد شده
                                var allUserTagsRetry = await _context.MessageTags
                                    .Where(t => t.UserId == userId && !t.IsDeleted)
                                    .ToListAsync();

                                var tag = allUserTagsRetry
                                    .FirstOrDefault(t => string.Equals(t.Name, tagName, StringComparison.OrdinalIgnoreCase));

                                if (tag != null)
                                {
                                    existingTags.Add(tag);
                                }
                            }
                        }

                        // ایجاد ContactTag های جدید
                        var contactTagsToAdd = existingTags
                            .Select(t => new ContactTag
                            {
                                ContactId = contact.Id,
                                TagId = t.Id,
                                CreatedAt = DateTime.UtcNow
                            })
                            .ToList();

                        if (contactTagsToAdd.Any())
                        {
                            await _context.ContactTags.AddRangeAsync(contactTagsToAdd);
                            await _context.SaveChangesAsync();
                        }
                    }
                }

                // به‌روزرسانی یا ایجاد اطلاعات تکمیلی
                if (!string.IsNullOrWhiteSpace(updateDto.CustomFields) || updateDto.DateOfBirth.HasValue)
                {
                    var additionalInfo = contact.AdditionalInfo;
                    if (additionalInfo == null)
                    {
                        additionalInfo = new ContactAdditionalInfo
                        {
                            ContactId = contact.Id,
                            CreatedAt = DateTime.UtcNow
                        };
                        await _context.ContactAdditionalInfos.AddAsync(additionalInfo);
                    }

                    if (updateDto.CustomFields != null)
                    {
                        additionalInfo.CustomFields = updateDto.CustomFields;
                    }

                    // به‌روزرسانی تاریخ تولد - اگر null ارسال شود، تاریخ تولد پاک می‌شود
                    // تبدیل به UTC قبل از ذخیره
                    additionalInfo.DateOfBirth = updateDto.DateOfBirth.EnsureDateOnlyUtc();

                    additionalInfo.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                // به‌روزرسانی مناسبت‌ها
                if (updateDto.Occasions != null)
                {
                    // حذف مناسبت‌های قبلی
                    var existingOccasions = await _context.ContactOccasions
                        .Where(o => o.ContactId == id)
                        .ToListAsync();
                    
                    _context.ContactOccasions.RemoveRange(existingOccasions);
                    
                    if (updateDto.Occasions.Any())
                    {
                        var newOccasions = updateDto.Occasions.Select(o => new ContactOccasion
                        {
                            ContactId = id,
                            Title = o.Title,
                            // تبدیل تاریخ مناسبت به UTC
                            Date = o.Date.EnsureUtc(),
                            HasTime = o.HasTime
                        }).ToList();
                        
                        await _context.ContactOccasions.AddRangeAsync(newOccasions);
                    }
                    
                    await _context.SaveChangesAsync();
                }

                // به‌روزرسانی تاریخ ویرایش دفترچه
                notebook.UpdatedAt = DateTime.UtcNow;
                await _notebookRepository.UpdateAsync(notebook);

                _logger.LogInformation("Contact updated successfully with ID: {ContactId}", id);

                // بارگذاری مجدد با اطلاعات تکمیلی
                var updatedContactWithInfo = await _contactRepository.GetByIdWithAdditionalInfoAsync(id);

                return ApiResponse<ContactResponseDto>.CreateSuccess(
                    await MapToContactResponseDtoAsync(updatedContactWithInfo!),
                    "اطلاعات مخاطب با موفقیت به‌روزرسانی شد"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating contact with ID: {ContactId}", id);
                throw;
            }
        }

        public async Task<ApiResponse<bool>> DeleteContactAsync(int id, int userId)
        {
            try
            {
                var contact = await _contactRepository.GetByIdAsync(id);

                if (contact == null)
                {
                    _logger.LogWarning("Contact not found for delete with ID: {ContactId}", id);
                    return ApiResponse<bool>.NotFound("مخاطب یافت نشد");
                }

                // بررسی مالکیت دفترچه
                var notebook = await _notebookRepository.GetByIdAsync(contact.ContactNotebookId);
                if (notebook == null || notebook.UserId != userId)
                {
                    return ApiResponse<bool>.Forbidden("شما مجاز به حذف این مخاطب نیستید");
                }

                // Soft Delete
                contact.IsDeleted = true;
                contact.UpdatedAt = DateTime.UtcNow;
                await _contactRepository.UpdateAsync(contact);

                // به‌روزرسانی تاریخ ویرایش دفترچه
                // note: 'notebook' is already fetched above
                if (notebook != null)
                {
                    notebook.UpdatedAt = DateTime.UtcNow;
                    await _notebookRepository.UpdateAsync(notebook);
                }

                _logger.LogInformation("Contact soft deleted successfully with ID: {ContactId}", id);

                return ApiResponse<bool>.CreateSuccess(true, "مخاطب با موفقیت حذف شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting contact with ID: {ContactId}", id);
                throw;
            }
        }

        public async Task<ApiResponse<bool>> TransferContactAsync(int contactId, int fromNotebookId, int toNotebookId, int userId)
        {
            try
            {
                var contact = await _contactRepository.GetByIdAsync(contactId);

                if (contact == null)
                {
                    return ApiResponse<bool>.NotFound("مخاطب یافت نشد");
                }

                if (contact.ContactNotebookId != fromNotebookId)
                {
                    return ApiResponse<bool>.BadRequest("مخاطب در دفترچه مبدا یافت نشد");
                }

                // بررسی مالکیت هر دو دفترچه
                var fromNotebook = await _notebookRepository.GetByIdAsync(fromNotebookId);
                var toNotebook = await _notebookRepository.GetByIdAsync(toNotebookId);

                if (fromNotebook == null || fromNotebook.UserId != userId)
                {
                    return ApiResponse<bool>.Forbidden("شما مجاز به انتقال از این دفترچه نیستید");
                }

                if (toNotebook == null || toNotebook.UserId != userId)
                {
                    return ApiResponse<bool>.Forbidden("شما مجاز به انتقال به این دفترچه نیستید");
                }

                // بررسی تکراری بودن شماره موبایل در دفترچه مقصد
                // اگر تکراری بود، انتقال را انجام نمی‌دهیم (بدون خطا)
                var exists = await _contactRepository.ExistsByMobileNumberInNotebookAsync(toNotebookId, contact.MobileNumber);
                if (exists)
                {
                    _logger.LogInformation("شماره موبایل {MobileNumber} در دفترچه مقصد {ToNotebookId} قبلاً وجود دارد. انتقال انجام نمی‌شود.", 
                        contact.MobileNumber, toNotebookId);
                    return ApiResponse<bool>.CreateSuccess(true, "مخاطب قبلاً در دفترچه مقصد وجود دارد");
                }

                // انتقال
                contact.ContactNotebookId = toNotebookId;
                contact.UpdatedAt = DateTime.UtcNow;
                await _contactRepository.UpdateAsync(contact);

                // به‌روزرسانی تاریخ ویرایش دفترچه‌های مبدا و مقصد
                if (fromNotebook != null)
                {
                    fromNotebook.UpdatedAt = DateTime.UtcNow;
                    await _notebookRepository.UpdateAsync(fromNotebook);
                }

                if (toNotebook != null)
                {
                    toNotebook.UpdatedAt = DateTime.UtcNow;
                    await _notebookRepository.UpdateAsync(toNotebook);
                }

                _logger.LogInformation("Contact {ContactId} transferred from notebook {FromNotebookId} to {ToNotebookId}", 
                    contactId, fromNotebookId, toNotebookId);

                return ApiResponse<bool>.CreateSuccess(true, "مخاطب با موفقیت منتقل شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error transferring contact {ContactId}", contactId);
                throw;
            }
        }

        public async Task<ApiResponse<ImportExcelResultDto>> ImportFromExcelAsync(int userId, ImportContactsFromExcelDto importDto)
        {
            try
            {
                // بررسی وجود دفترچه و مالکیت
                var notebook = await _notebookRepository.GetByIdAsync(importDto.ContactNotebookId);
                if (notebook == null)
                {
                    return ApiResponse<ImportExcelResultDto>.NotFound("دفترچه یافت نشد");
                }

                if (notebook.UserId != userId)
                {
                    return ApiResponse<ImportExcelResultDto>.Forbidden("شما مجاز به ایمپورت به این دفترچه نیستید");
                }

                // اعتبارسنجی فایل اکسل
                if (importDto.ExcelFile == null || importDto.ExcelFile.Length == 0)
                {
                    return ApiResponse<ImportExcelResultDto>.BadRequest("فایل اکسل انتخاب نشده است");
                }

                var allowedExcelTypes = new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/vnd.ms-excel" };
                var contentType = importDto.ExcelFile.ContentType.ToLower();
                
                if (!allowedExcelTypes.Contains(contentType))
                {
                    return ApiResponse<ImportExcelResultDto>.BadRequest($"نوع فایل '{contentType}' مجاز نیست. فقط فایل‌های Excel (.xlsx, .xls) قابل قبول هستند");
                }

                // بررسی حجم فایل (حداکثر 10 مگابایت برای فایل اکسل)
                var maxSize = 10 * 1024 * 1024; // 10 MB
                if (importDto.ExcelFile.Length > maxSize)
                {
                    var fileSizeMB = importDto.ExcelFile.Length / (1024.0 * 1024.0);
                    return ApiResponse<ImportExcelResultDto>.BadRequest($"حجم فایل ({fileSizeMB:F2} MB) از حد مجاز (10 MB) بیشتر است");
                }

                // نتیجه ایمپورت
                var result = new ImportExcelResultDto();

                // خواندن فایل اکسل و ایمپورت مخاطبین
                using var stream = importDto.ExcelFile.OpenReadStream();
                    using var workbook = new XLWorkbook(stream);
                    var worksheet = workbook.Worksheets.FirstOrDefault();

                    if (worksheet == null)
                    {
                        return ApiResponse<ImportExcelResultDto>.BadRequest("فایل اکسل فاقد شیت است");
                    }

                    // پیدا کردن ستون‌ها بر اساس نام در ردیف اول (هدر)
                    var headerRow = worksheet.Row(1);
                    var columnMapping = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    
                    foreach (var cell in headerRow.CellsUsed())
                    {
                        var headerName = cell.GetString().Trim().ToLower();
                        columnMapping[headerName] = cell.Address.ColumnNumber;
                    }

                    // بررسی وجود ستون شماره موبایل
                    var mobileColumnNames = new[] { "mobilenumber", "mobile", "موبایل", "شماره موبایل", "شماره", "phone", "phonenumber", "تلفن همراه" };
                    int mobileColumn = 0;
                    foreach (var name in mobileColumnNames)
                    {
                        if (columnMapping.TryGetValue(name, out int col))
                        {
                            mobileColumn = col;
                            break;
                        }
                    }

                    if (mobileColumn == 0)
                    {
                        // اگر هدر پیدا نشد، فرض می‌کنیم ستون اول شماره موبایل است
                        mobileColumn = 1;
                    }

                    // پیدا کردن ستون‌های دیگر
                    var fullNameColumnNames = new[] { "fullname", "full name", "نام کامل", "نام و نام خانوادگی" };
                    var firstNameColumnNames = new[] { "firstname", "نام", "first name", "name" };
                    var lastNameColumnNames = new[] { "lastname", "نام خانوادگی", "last name", "family", "familyname" };
                    var brandColumnNames = new[] { "brand", "برند" };
                    var tagsColumnNames = new[] { "tags", "برچسب", "برچسب‌ها", "tag" };
                    var dateOfBirthColumnNames = new[] { "dateofbirth", "تاریخ تولد", "birthday", "birthdate", "تولد" };

                    int fullNameColumn = FindColumn(columnMapping, fullNameColumnNames);
                    int firstNameColumn = FindColumn(columnMapping, firstNameColumnNames);
                    int lastNameColumn = FindColumn(columnMapping, lastNameColumnNames);
                    int brandColumn = FindColumn(columnMapping, brandColumnNames);
                    int tagsColumn = FindColumn(columnMapping, tagsColumnNames);
                    int dateOfBirthColumn = FindColumn(columnMapping, dateOfBirthColumnNames);

                    // دریافت لیست شماره‌های موبایل موجود در دفترچه برای بررسی تکراری
                    var existingMobileNumbers = await _context.Contacts
                        .Where(c => c.ContactNotebookId == importDto.ContactNotebookId && !c.IsDeleted)
                        .Select(c => c.MobileNumber)
                        .ToHashSetAsync();

                    // Regex برای اعتبارسنجی شماره موبایل
                    var mobileRegex = new Regex(@"^09\d{9}$");

                    // شروع از ردیف دوم (بعد از هدر)
                    var lastRowUsed = worksheet.LastRowUsed()?.RowNumber() ?? 1;
                    var contactsData = new List<(Contact contact, string? tags, DateTime? dateOfBirth)>();

                    for (int rowNum = 2; rowNum <= lastRowUsed; rowNum++)
                    {
                        var row = worksheet.Row(rowNum);
                        
                        // خواندن شماره موبایل
                        var mobileCell = row.Cell(mobileColumn);
                        var mobileNumber = mobileCell.GetString()?.Trim() ?? "";
                        
                        // نرمال‌سازی شماره موبایل
                        mobileNumber = NormalizeMobileNumber(mobileNumber);

                        result.TotalRows++;

                        // بررسی خالی بودن
                        if (string.IsNullOrWhiteSpace(mobileNumber))
                        {
                            result.SkippedCount++;
                            result.Errors.Add(new ImportRowError
                            {
                                RowNumber = rowNum,
                                MobileNumber = null,
                                ErrorMessage = "شماره موبایل خالی است"
                            });
                            continue;
                        }

                        // بررسی فرمت شماره موبایل
                        if (!mobileRegex.IsMatch(mobileNumber))
                        {
                            result.SkippedCount++;
                            result.Errors.Add(new ImportRowError
                            {
                                RowNumber = rowNum,
                                MobileNumber = mobileNumber,
                                ErrorMessage = "فرمت شماره موبایل نامعتبر است (باید با 09 شروع شود و 11 رقم باشد)"
                            });
                            continue;
                        }

                        // بررسی تکراری بودن در دفترچه
                        if (existingMobileNumbers.Contains(mobileNumber))
                        {
                            result.DuplicateCount++;
                            result.Errors.Add(new ImportRowError
                            {
                                RowNumber = rowNum,
                                MobileNumber = mobileNumber,
                                ErrorMessage = "این شماره موبایل قبلاً در دفترچه ثبت شده است"
                            });
                            continue;
                        }

                        // بررسی تکراری بودن در لیست فعلی ایمپورت
                        if (contactsData.Any(c => c.contact.MobileNumber == mobileNumber))
                        {
                            result.DuplicateCount++;
                            result.Errors.Add(new ImportRowError
                            {
                                RowNumber = rowNum,
                                MobileNumber = mobileNumber,
                                ErrorMessage = "این شماره موبایل در فایل تکراری است"
                            });
                            continue;
                        }

                        // خواندن سایر فیلدها
                        var fullName = fullNameColumn > 0 ? row.Cell(fullNameColumn).GetString()?.Trim() : null;
                        if (string.IsNullOrWhiteSpace(fullName))
                        {
                            var fName = firstNameColumn > 0 ? row.Cell(firstNameColumn).GetString()?.Trim() : null;
                            var lName = lastNameColumn > 0 ? row.Cell(lastNameColumn).GetString()?.Trim() : null;
                            if (!string.IsNullOrWhiteSpace(fName) || !string.IsNullOrWhiteSpace(lName))
                            {
                                fullName = $"{fName ?? ""} {lName ?? ""}".Trim();
                            }
                        }

                        // چک کردن اجباری بودن نام
                        if (string.IsNullOrWhiteSpace(fullName))
                        {
                            result.SkippedCount++;
                            result.Errors.Add(new ImportRowError
                            {
                                RowNumber = rowNum,
                                MobileNumber = mobileNumber,
                                ErrorMessage = "نام کامل مخاطب الزامی است"
                            });
                            continue;
                        }

                        var brand = brandColumn > 0 ? row.Cell(brandColumn).GetString()?.Trim() : null;
                        var tags = tagsColumn > 0 ? row.Cell(tagsColumn).GetString()?.Trim() : null;

                        // خواندن تاریخ تولد
                        DateTime? dateOfBirth = null;
                        if (dateOfBirthColumn > 0)
                        {
                            var dateCell = row.Cell(dateOfBirthColumn);
                            if (dateCell.TryGetValue<DateTime>(out var parsedDate))
                            {
                                dateOfBirth = parsedDate;
                            }
                            else
                            {
                                var dateString = dateCell.GetString()?.Trim();
                                if (!string.IsNullOrWhiteSpace(dateString) && DateTime.TryParse(dateString, out var parsedDateString))
                                {
                                    dateOfBirth = parsedDateString;
                                }
                            }
                        }

                        // ایجاد مخاطب
                        var contact = new Contact
                        {
                            ContactNotebookId = importDto.ContactNotebookId,
                            MobileNumber = mobileNumber,
                            FullName = string.IsNullOrWhiteSpace(fullName) ? null : fullName,
                            Brand = string.IsNullOrWhiteSpace(brand) ? null : brand,
                            Tags = null, // فیلد Tags دیگر استفاده نمی‌شود
                            CreatedAt = DateTime.UtcNow
                        };

                        contactsData.Add((contact, tags, dateOfBirth));
                        existingMobileNumbers.Add(mobileNumber); // برای جلوگیری از تکراری در همین فایل
                    }

                    // ذخیره مخاطبین در دیتابیس
                    string excelFilePath = string.Empty;
                    if (contactsData.Any())
                    {
                        using var transaction = await _context.Database.BeginTransactionAsync();
                        try
                        {
                            // ذخیره مخاطبین برای گرفتن ID
                            foreach (var (contact, _, _) in contactsData)
                            {
                                await _context.Contacts.AddAsync(contact);
                                await _context.SaveChangesAsync(); // Save برای گرفتن ContactId
                            }

                            // ایجاد AdditionalInfo برای مخاطبینی که تاریخ تولد دارند
                            foreach (var (contact, _, dateOfBirth) in contactsData)
                            {
                                if (dateOfBirth.HasValue)
                                {
                                    var additionalInfo = new ContactAdditionalInfo
                                    {
                                        ContactId = contact.Id,
                                        // تبدیل تاریخ تولد به UTC - فقط تاریخ مهم است
                                        DateOfBirth = dateOfBirth.Value.EnsureDateOnlyUtc(),
                                        CreatedAt = DateTime.UtcNow
                                    };
                                    await _context.ContactAdditionalInfos.AddAsync(additionalInfo);
                                }
                            }
                            await _context.SaveChangesAsync();

                            // پردازش تگ‌ها برای همه مخاطبین
                            foreach (var (contact, tags, _) in contactsData)
                            {
                                // پردازش تگ‌ها اگر وجود داشت
                                if (!string.IsNullOrWhiteSpace(tags))
                                {
                                    // تبدیل tags string به لیست (با کاما یا خط جدا شده)
                                    var tagNames = tags.Split(new[] { ',', '،', '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(t => t.Trim())
                                        .Where(t => !string.IsNullOrWhiteSpace(t))
                                        .Distinct()
                                        .ToList();

                                    if (tagNames.Any())
                                    {
                                        // دریافت تگ‌های موجود
                                        var allUserTags = await _context.MessageTags
                                            .Where(t => t.UserId == userId && !t.IsDeleted)
                                            .ToListAsync();

                                        var existingTags = allUserTags
                                            .Where(t => tagNames.Any(n => 
                                                string.Equals(t.Name, n, StringComparison.OrdinalIgnoreCase)))
                                            .ToList();

                                        var existingTagNames = existingTags.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                                        var tagsToCreate = tagNames
                                            .Where(n => !existingTagNames.Contains(n) && n.Length <= 100)
                                            .ToList();

                                        // ایجاد تگ‌های جدید
                                        foreach (var tagName in tagsToCreate)
                                        {
                                            try
                                            {
                                                var newTag = new MessageTag
                                                {
                                                    UserId = userId,
                                                    Name = tagName,
                                                    IsActive = true,
                                                    CreatedAt = DateTime.UtcNow
                                                };

                                                await _context.MessageTags.AddAsync(newTag);
                                                await _context.SaveChangesAsync();
                                                existingTags.Add(newTag);
                                            }
                                            catch (DbUpdateException)
                                            {
                                                // Race condition - تگ قبلاً ایجاد شده
                                                var retryTag = await _context.MessageTags
                                                    .Where(t => t.UserId == userId 
                                                        && string.Equals(t.Name, tagName, StringComparison.OrdinalIgnoreCase)
                                                        && !t.IsDeleted)
                                                    .FirstOrDefaultAsync();
                                                
                                                if (retryTag != null)
                                                    existingTags.Add(retryTag);
                                            }
                                        }

                                        // ایجاد ContactTags
                                        var contactTagsToAdd = existingTags
                                            .Select(t => new ContactTag
                                            {
                                                ContactId = contact.Id,
                                                TagId = t.Id,
                                                CreatedAt = DateTime.UtcNow
                                            })
                                            .ToList();

                                        if (contactTagsToAdd.Any())
                                        {
                                            await _context.ContactTags.AddRangeAsync(contactTagsToAdd);
                                            await _context.SaveChangesAsync();
                                        }
                                    }
                                }
                            }

                            await transaction.CommitAsync();

                            // آپلود فایل اکسل با استفاده از سرویس آپلود فایل (فقط اگر transaction موفق بود)
                            try
                            {
                                excelFilePath = await _fileUploadService.UploadFileAsync(
                                    importDto.ExcelFile, 
                                    FileUploadConstants.EntityType_ContactNotebook, 
                                    importDto.ContactNotebookId, 
                                    null
                                );
                                _logger.LogInformation("فایل اکسل با موفقیت آپلود شد: {FilePath}", excelFilePath);
                                result.UploadedFilePath = excelFilePath;
                            }
                            catch (Exception uploadEx)
                            {
                                _logger.LogError(uploadEx, "خطا در آپلود فایل اکسل");
                                // آپلود فایل اختیاری است، خطا را ignore می‌کنیم
                            }

                            // به‌روزرسانی تاریخ ویرایش دفترچه
                            notebook.UpdatedAt = DateTime.UtcNow;
                            await _notebookRepository.UpdateAsync(notebook);

                            result.SuccessCount = contactsData.Count;

                            _logger.LogInformation(
                                "ایمپورت اکسل با موفقیت انجام شد. دفترچه: {NotebookId}, موفق: {Success}, تکراری: {Duplicate}, رد شده: {Skipped}",
                                importDto.ContactNotebookId, result.SuccessCount, result.DuplicateCount, result.SkippedCount);
                        }
                        catch (Exception ex)
                        {
                            await transaction.RollbackAsync();
                            _logger.LogError(ex, "خطا در ذخیره مخاطبین ایمپورت شده");
                            
                            try
                            {
                                if (!string.IsNullOrEmpty(excelFilePath))
                                {
                                    await _fileUploadService.DeleteFileAsync(
                                        excelFilePath, 
                                        FileUploadConstants.EntityType_ContactNotebook, 
                                        importDto.ContactNotebookId, 
                                        null);
                                }
                            }
                            catch (Exception deleteEx)
                            {
                                _logger.LogWarning(deleteEx, "Error deleting failed import file: {FilePath}", excelFilePath);
                            }

                            result.ErrorCount = contactsData.Count;
                            return ApiResponse<ImportExcelResultDto>.InternalServerError("خطا در ذخیره مخاطبین در دیتابیس");
                        }
                    }

                var message = $"ایمپورت با موفقیت انجام شد. {result.SuccessCount} مخاطب اضافه شد";
                if (result.DuplicateCount > 0)
                    message += $"، {result.DuplicateCount} مورد تکراری";
                if (result.SkippedCount > 0)
                    message += $"، {result.SkippedCount} مورد رد شده";

                return ApiResponse<ImportExcelResultDto>.CreateSuccess(result, message, 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در خواندن فایل اکسل");

                // اگر خطایی در خواندن رخ دهد، فایل هنوز آپلود نشده است پس نیازی به حذف نیست
                return ApiResponse<ImportExcelResultDto>.BadRequest($"خطا در خواندن فایل اکسل: {ex.Message}");
            }
        }

        /// <summary>
        /// پیدا کردن شماره ستون بر اساس نام‌های ممکن
        /// </summary>
        private int FindColumn(Dictionary<string, int> columnMapping, string[] possibleNames)
        {
            foreach (var name in possibleNames)
            {
                if (columnMapping.TryGetValue(name, out int col))
                {
                    return col;
                }
            }
            return 0;
        }


        /// <summary>
        /// نرمال‌سازی شماره موبایل
        /// </summary>
        private string NormalizeMobileNumber(string mobileNumber)
        {
            if (string.IsNullOrWhiteSpace(mobileNumber))
                return "";

            // حذف فاصله‌ها و کاراکترهای اضافی
            mobileNumber = mobileNumber.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
            
            // تبدیل اعداد فارسی/عربی به انگلیسی
            mobileNumber = ConvertPersianDigitsToEnglish(mobileNumber);

            // اگر با +98 شروع شده، تبدیل به 0
            if (mobileNumber.StartsWith("+98"))
            {
                mobileNumber = "0" + mobileNumber.Substring(3);
            }
            // اگر با 98 شروع شده (بدون +)
            else if (mobileNumber.StartsWith("98") && mobileNumber.Length == 12)
            {
                mobileNumber = "0" + mobileNumber.Substring(2);
            }
            // اگر با 9 شروع شده (بدون 0)
            else if (mobileNumber.StartsWith("9") && mobileNumber.Length == 10)
            {
                mobileNumber = "0" + mobileNumber;
            }

            return mobileNumber;
        }

        /// <summary>
        /// تبدیل اعداد فارسی/عربی به انگلیسی
        /// </summary>
        private string ConvertPersianDigitsToEnglish(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var persianDigits = new[] { '۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹' };
            var arabicDigits = new[] { '٠', '٫', '٢', '٣', '٤', '٥', '٦', '٧', '٨', '٩' };
            var englishDigits = new[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };

            var result = input.ToCharArray();
            for (int i = 0; i < result.Length; i++)
            {
                var index = Array.IndexOf(persianDigits, result[i]);
                if (index >= 0)
                {
                    result[i] = englishDigits[index];
                    continue;
                }
                
                index = Array.IndexOf(arabicDigits, result[i]);
                if (index >= 0)
                {
                    result[i] = englishDigits[index];
                }
            }

            return new string(result);
        }

        public async Task<ApiResponse<ExportExcelResultDto>> GetImportExcelTemplateAsync()
        {
            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("قالب ایمپورت مخاطبین");

                // تنظیمات RTL
                worksheet.RightToLeft = true;

                // هدرها
                worksheet.Cell(1, 1).Value = "شماره موبایل";
                worksheet.Cell(1, 2).Value = "نام کامل";
                worksheet.Cell(1, 3).Value = "برند";
                worksheet.Cell(1, 4).Value = "برچسب‌ها";

                // استایل هدر
                var headerRange = worksheet.Range(1, 1, 1, 4);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
                headerRange.Style.Font.FontColor = XLColor.White;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                // مثال داده
                worksheet.Cell(2, 1).Value = "09123456789";
                worksheet.Cell(2, 2).Value = "علی محمدی";
                worksheet.Cell(2, 3).Value = "فروشگاه نمونه";
                worksheet.Cell(2, 4).Value = "VIP, مشتری جدید";

                // توضیحات (Comment) برای ستون موبایل
                var comment = worksheet.Cell(1, 1).GetComment();
                comment.AddText("شماره موبایل باید ۱۱ رقم باشد و با 09 شروع شود.\nمثال: 09123456789");
                comment.Author = "سیستم";

                // تنظیم عرض ستون‌ها
                worksheet.Column(1).Width = 15;
                worksheet.Column(2).Width = 25;
                worksheet.Column(3).Width = 20;
                worksheet.Column(4).Width = 25;

                using var memoryStream = new MemoryStream();
                workbook.SaveAs(memoryStream);
                var fileContent = memoryStream.ToArray();

                var result = new ExportExcelResultDto
                {
                    FileContent = fileContent,
                    FileName = "ContactImportTemplate.xlsx",
                    ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    TotalCount = 0,
                    ExportedCount = 0
                };

                return ApiResponse<ExportExcelResultDto>.CreateSuccess(result, "قالب اکسل آماده دانلود است");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating import excel template");
                return ApiResponse<ExportExcelResultDto>.InternalServerError("خطا در ایجاد فایل قالب");
            }
        }

        public async Task<ApiResponse<ExportExcelResultDto>> ExportToExcelAsync(int notebookId, int userId, int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                // اعتبارسنجی پارامترهای صفحه‌بندی
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 1000) pageSize = 100; // حداکثر 1000 برای اکسپورت

                // بررسی وجود دفترچه و مالکیت
                var notebook = await _notebookRepository.GetByIdAsync(notebookId);
                if (notebook == null)
                {
                    return ApiResponse<ExportExcelResultDto>.NotFound("دفترچه یافت نشد");
                }

                if (notebook.UserId != userId)
                {
                    return ApiResponse<ExportExcelResultDto>.Forbidden("شما مجاز به اکسپورت از این دفترچه نیستید");
                }

                // دریافت مخاطبین با اطلاعات تکمیلی و مناسبت‌ها
                var allContacts = await _context.Contacts
                    .Include(c => c.AdditionalInfo)
                    .Include(c => c.Occasions)
                    .Include(c => c.ContactTags)
                        .ThenInclude(ct => ct.Tag)
                    .Where(c => c.ContactNotebookId == notebookId && !c.IsDeleted)
                    .OrderBy(c => c.FullName)
                    .ToListAsync();

                var totalCount = allContacts.Count;
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                // اعمال صفحه‌بندی
                var pagedContacts = allContacts
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // ایجاد فایل اکسل
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("مخاطبین");

                // تنظیمات RTL برای فارسی
                worksheet.RightToLeft = true;

                // ایجاد هدر
                var headerRow = 1;
                worksheet.Cell(headerRow, 1).Value = "ردیف";
                worksheet.Cell(headerRow, 2).Value = "شماره موبایل";
                worksheet.Cell(headerRow, 3).Value = "نام کامل";
                worksheet.Cell(headerRow, 4).Value = "برند";
                worksheet.Cell(headerRow, 5).Value = "برچسب‌ها";
                worksheet.Cell(headerRow, 6).Value = "مناسبت‌ها";
                worksheet.Cell(headerRow, 7).Value = "تاریخ ایجاد";

                // استایل هدر
                var headerRange = worksheet.Range(headerRow, 1, headerRow, 7);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
                headerRange.Style.Font.FontColor = XLColor.White;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                // پر کردن داده‌ها
                var rowNum = 2;
                var itemIndex = (pageNumber - 1) * pageSize + 1;
                foreach (var contact in pagedContacts)
                {
                    worksheet.Cell(rowNum, 1).Value = itemIndex;
                    worksheet.Cell(rowNum, 2).Value = contact.MobileNumber;
                    worksheet.Cell(rowNum, 3).Value = contact.FullName ?? "";
                    worksheet.Cell(rowNum, 4).Value = contact.Brand ?? "";
                    
                    // خواندن تگ‌ها از ContactTags
                    var tagsStr = "";
                    if (contact.ContactTags != null && contact.ContactTags.Any())
                    {
                        var tagNames = contact.ContactTags
                            .Where(ct => ct.Tag != null && !ct.Tag.IsDeleted && ct.Tag.IsActive)
                            .Select(ct => ct.Tag!.Name)
                            .ToList();
                        tagsStr = string.Join(", ", tagNames);
                    }
                    worksheet.Cell(rowNum, 5).Value = tagsStr;
                    
                    // فرمت مناسبت‌ها: عنوان: تاریخ | عنوان: تاریخ
                    var occasionsStr = "";
                    if (contact.Occasions != null && contact.Occasions.Any())
                    {
                        occasionsStr = string.Join(" | ", contact.Occasions.Select(o => 
                            $"{o.Title}: {o.Date.ToString(o.HasTime ? "yyyy-MM-dd HH:mm" : "yyyy-MM-dd")}"));
                    }
                    worksheet.Cell(rowNum, 6).Value = occasionsStr;
                    
                    worksheet.Cell(rowNum, 7).Value = contact.CreatedAt.ToString("yyyy-MM-dd HH:mm");

                    // استایل ردیف‌های داده
                    var dataRange = worksheet.Range(rowNum, 1, rowNum, 7);
                    dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    
                    // رنگ‌بندی ردیف‌های زوج و فرد
                    if (rowNum % 2 == 0)
                    {
                        dataRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E2F3");
                    }

                    rowNum++;
                    itemIndex++;
                }

                // تنظیم عرض ستون‌ها
                worksheet.Column(1).Width = 8;   // ردیف
                worksheet.Column(2).Width = 15;  // شماره موبایل
                worksheet.Column(3).Width = 25;  // نام کامل
                worksheet.Column(4).Width = 20;  // برند
                worksheet.Column(5).Width = 25;  // برچسب‌ها
                worksheet.Column(6).Width = 40;  // مناسبت‌ها
                worksheet.Column(7).Width = 18;  // تاریخ ایجاد

                // تبدیل به byte array
                using var memoryStream = new MemoryStream();
                workbook.SaveAs(memoryStream);
                var fileContent = memoryStream.ToArray();

                // ایجاد نام فایل با زمان UTC
                var fileName = $"Contacts_{notebook.Name}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
                // حذف کاراکترهای غیرمجاز از نام فایل
                fileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));

                var result = new ExportExcelResultDto
                {
                    FileContent = fileContent,
                    FileName = fileName,
                    ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    TotalCount = totalCount,
                    ExportedCount = pagedContacts.Count,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                _logger.LogInformation(
                    "اکسپورت اکسل با موفقیت انجام شد. دفترچه: {NotebookId}, صفحه: {Page}/{TotalPages}, تعداد: {Count}",
                    notebookId, pageNumber, totalPages, pagedContacts.Count);

                return ApiResponse<ExportExcelResultDto>.CreateSuccess(
                    result, 
                    $"فایل اکسل با {pagedContacts.Count} مخاطب از {totalCount} مخاطب آماده دانلود است (صفحه {pageNumber} از {totalPages})");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting contacts to Excel for notebook: {NotebookId}", notebookId);
                return ApiResponse<ExportExcelResultDto>.InternalServerError("خطا در تولید فایل اکسل");
            }
        }

        /// <summary>
        /// اعتبارسنجی فایل عکس پروفایل
        /// </summary>
        private string? ValidateProfileImage(Microsoft.AspNetCore.Http.IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                return "فایل تصویر انتخاب نشده است";
            }

            // بررسی نوع فایل - فقط عکس مجاز است
            var allowedImageTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
            var contentType = imageFile.ContentType.ToLower();
            
            if (!allowedImageTypes.Contains(contentType))
            {
                return $"نوع فایل '{contentType}' مجاز نیست. فقط فایل‌های تصویری (JPEG, PNG, GIF, WebP) قابل قبول هستند";
            }

            // بررسی حجم فایل (حداکثر 10 مگابایت برای عکس پروفایل)
            var maxSize = 10 * 1024 * 1024; // 10 MB
            if (imageFile.Length > maxSize)
            {
                var fileSizeMB = imageFile.Length / (1024.0 * 1024.0);
                return $"حجم فایل ({fileSizeMB:F2} MB) از حد مجاز (10 MB) بیشتر است";
            }

            // بررسی نام فایل
            if (string.IsNullOrWhiteSpace(imageFile.FileName))
            {
                return "نام فایل معتبر نیست";
            }

            return null; // فایل معتبر است
        }

        /// <summary>
        /// تبدیل Contact به ContactResponseDto
        /// </summary>
        private async Task<ContactResponseDto> MapToContactResponseDtoAsync(Contact contact)
        {
            // بارگذاری دفترچه در صورت نیاز
            string notebookName;
            if (contact.ContactNotebook == null)
            {
                var notebook = await _notebookRepository.GetByIdAsync(contact.ContactNotebookId);
                notebookName = notebook?.Name ?? "";
            }
            else
            {
                notebookName = contact.ContactNotebook.Name;
            }

            var responseDto = new ContactResponseDto
            {
                Id = contact.Id,
                ContactNotebookId = contact.ContactNotebookId,
                ContactNotebookName = notebookName,
                MobileNumber = contact.MobileNumber,
                FullName = contact.FullName,
                Brand = contact.Brand,
                ProfileImagePath = contact.ProfileImagePath,
                DateOfBirth = contact.AdditionalInfo?.DateOfBirth,
                MarriageDate = contact.AdditionalInfo?.MarriageDate,
                CustomFields = contact.AdditionalInfo?.CustomFields,
                CreatedAt = contact.CreatedAt,
                UpdatedAt = contact.UpdatedAt
            };
            
            // افزودن تگ‌های مخاطب
            if (contact.ContactTags != null && contact.ContactTags.Any())
            {
                responseDto.ContactTags = contact.ContactTags
                    .Where(ct => ct.Tag != null && !ct.Tag.IsDeleted && ct.Tag.IsActive)
                    .Select(ct => new MessageTagResponseDto
                    {
                        Id = ct.Tag!.Id,
                        Name = ct.Tag.Name,
                        Color = ct.Tag.Color,
                        Description = ct.Tag.Description,
                        IsActive = ct.Tag.IsActive,
                        CreatedAt = ct.Tag.CreatedAt
                    })
                    .ToList();
            }
            
            if (contact.Occasions != null)
            {
                responseDto.Occasions = contact.Occasions.Select(o => new ContactOccasionDto
                {
                    Id = o.Id,
                    Title = o.Title,
                    Date = o.Date,
                    HasTime = o.HasTime
                }).ToList();
            }

            // افزودن URL عکس پروفایل در صورت وجود
            if (!string.IsNullOrWhiteSpace(contact.ProfileImagePath))
            {
                responseDto.ProfileImageUrl = _fileUploadService.GetFileUrl(contact.ProfileImagePath);
            }

            return responseDto;
        }

        public async Task<ApiResponse<string>> UploadProfileImageAsync(int contactId, int userId, Microsoft.AspNetCore.Http.IFormFile imageFile)
        {
            try
            {
                var contact = await _contactRepository.GetByIdAsync(contactId);
                if (contact == null)
                {
                    return ApiResponse<string>.NotFound("مخاطب یافت نشد");
                }

                // بررسی مالکیت دفترچه
                var notebook = await _notebookRepository.GetByIdAsync(contact.ContactNotebookId);
                if (notebook == null || notebook.UserId != userId)
                {
                    return ApiResponse<string>.Forbidden("شما مجاز به ویرایش این مخاطب نیستید");
                }

                // اعتبارسنجی فایل عکس
                var validationError = ValidateProfileImage(imageFile);
                if (validationError != null)
                {
                    return ApiResponse<string>.BadRequest(validationError);
                }

                // استفاده از Transaction برای اطمینان از یکپارچگی داده‌ها
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // حذف عکس قبلی در صورت وجود
                    if (!string.IsNullOrWhiteSpace(contact.ProfileImagePath))
                    {
                        try
                        {
                            await _fileUploadService.DeleteFileAsync(
                                contact.ProfileImagePath, 
                                FileUploadConstants.EntityType_Contact, 
                                contactId, 
                                FileUploadConstants.SubFolder_Profile);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "خطا در حذف عکس قبلی مخاطب {ContactId}", contactId);
                            // ادامه می‌دهیم حتی اگر حذف عکس قبلی با خطا مواجه شود
                        }
                    }

                    // آپلود عکس جدید
                    var relativePath = await _fileUploadService.UploadFileAsync(
                        imageFile, 
                        FileUploadConstants.EntityType_Contact, 
                        contactId, 
                        FileUploadConstants.SubFolder_Profile);

                    // به‌روزرسانی مسیر عکس در دیتابیس
                    contact.ProfileImagePath = relativePath;
                    contact.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    // Commit transaction
                    await transaction.CommitAsync();

                    var imageUrl = _fileUploadService.GetFileUrl(relativePath);

                    _logger.LogInformation("عکس پروفایل برای مخاطب {ContactId} با موفقیت آپلود شد", contactId);

                    return ApiResponse<string>.CreateSuccess(imageUrl, "عکس پروفایل با موفقیت آپلود شد");
                }
                catch (Exception ex)
                {
                    // Rollback transaction در صورت خطا
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "خطا در آپلود عکس پروفایل برای مخاطب {ContactId}", contactId);
                    
                    if (ex is ArgumentException)
                    {
                        return ApiResponse<string>.BadRequest(ex.Message);
                    }
                    
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در آپلود عکس پروفایل برای مخاطب {ContactId}", contactId);
                throw;
            }
        }

        /// <summary>
        /// آپلود عکس پروفایل مخاطب (بدون نیاز به احراز هویت)
        /// </summary>
        public async Task<ApiResponse<string>> UploadProfileImageAsync(int contactId, Microsoft.AspNetCore.Http.IFormFile imageFile)
        {
            try
            {
                var contact = await _contactRepository.GetByIdAsync(contactId);
                if (contact == null)
                {
                    return ApiResponse<string>.NotFound("مخاطب یافت نشد");
                }

                // اعتبارسنجی فایل عکس
                var validationError = ValidateProfileImage(imageFile);
                if (validationError != null)
                {
                    return ApiResponse<string>.BadRequest(validationError);
                }

                // استفاده از Transaction برای اطمینان از یکپارچگی داده‌ها
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // حذف عکس قبلی در صورت وجود
                    if (!string.IsNullOrWhiteSpace(contact.ProfileImagePath))
                    {
                        try
                        {
                            await _fileUploadService.DeleteFileAsync(
                                contact.ProfileImagePath, 
                                FileUploadConstants.EntityType_Contact, 
                                contactId, 
                                FileUploadConstants.SubFolder_Profile);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "خطا در حذف عکس قبلی مخاطب {ContactId}", contactId);
                            // ادامه می‌دهیم حتی اگر حذف عکس قبلی با خطا مواجه شود
                        }
                    }

                    // آپلود عکس جدید
                    var relativePath = await _fileUploadService.UploadFileAsync(
                        imageFile, 
                        FileUploadConstants.EntityType_Contact, 
                        contactId, 
                        FileUploadConstants.SubFolder_Profile);

                    // به‌روزرسانی مسیر عکس در دیتابیس
                    contact.ProfileImagePath = relativePath;
                    contact.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    // Commit transaction
                    await transaction.CommitAsync();

                    var imageUrl = _fileUploadService.GetFileUrl(relativePath);

                    _logger.LogInformation("عکس پروفایل برای مخاطب {ContactId} با موفقیت آپلود شد (بدون احراز هویت)", contactId);

                    return ApiResponse<string>.CreateSuccess(imageUrl, "عکس پروفایل با موفقیت آپلود شد");
                }
                catch (Exception ex)
                {
                    // Rollback transaction در صورت خطا
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "خطا در آپلود عکس پروفایل برای مخاطب {ContactId}", contactId);
                    
                    if (ex is ArgumentException)
                    {
                        return ApiResponse<string>.BadRequest(ex.Message);
                    }
                    
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در آپلود عکس پروفایل برای مخاطب {ContactId}", contactId);
                throw;
            }
        }

        public async Task<ApiResponse<bool>> DeleteProfileImageAsync(int contactId, int userId)
        {
            try
            {
                var contact = await _contactRepository.GetByIdAsync(contactId);
                if (contact == null)
                {
                    return ApiResponse<bool>.NotFound("مخاطب یافت نشد");
                }

                // بررسی مالکیت دفترچه
                var notebook = await _notebookRepository.GetByIdAsync(contact.ContactNotebookId);
                if (notebook == null || notebook.UserId != userId)
                {
                    return ApiResponse<bool>.Forbidden("شما مجاز به ویرایش این مخاطب نیستید");
                }

                if (string.IsNullOrWhiteSpace(contact.ProfileImagePath))
                {
                    return ApiResponse<bool>.BadRequest("عکس پروفایلی برای حذف وجود ندارد");
                }

                // استفاده از Transaction برای اطمینان از یکپارچگی داده‌ها
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var oldImagePath = contact.ProfileImagePath;

                    // حذف مسیر از دیتابیس
                    contact.ProfileImagePath = null;
                    contact.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    // Commit transaction قبل از حذف فایل
                    await transaction.CommitAsync();

                    // حذف فایل از سرور (بعد از commit)
                    try
                    {
                        await _fileUploadService.DeleteFileAsync(
                            oldImagePath, 
                            FileUploadConstants.EntityType_Contact, 
                            contactId, 
                            FileUploadConstants.SubFolder_Profile);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "خطا در حذف فایل عکس پروفایل از سرور برای مخاطب {ContactId}", contactId);
                        // حتی اگر حذف فایل با خطا مواجه شود، رکورد در دیتابیس به‌روزرسانی شده است
                    }

                    _logger.LogInformation("عکس پروفایل برای مخاطب {ContactId} با موفقیت حذف شد", contactId);

                    return ApiResponse<bool>.CreateSuccess(true, "عکس پروفایل با موفقیت حذف شد");
                }
                catch (Exception ex)
                {
                    // Rollback transaction در صورت خطا
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "خطا در حذف عکس پروفایل برای مخاطب {ContactId}", contactId);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در حذف عکس پروفایل برای مخاطب {ContactId}", contactId);
                throw;
            }
        }

        public async Task<ApiResponse<List<string>>> UploadAttachmentFilesAsync(int contactId, int userId, List<Microsoft.AspNetCore.Http.IFormFile> files)
        {
            try
            {
                var contact = await _contactRepository.GetByIdAsync(contactId);
                if (contact == null)
                {
                    return ApiResponse<List<string>>.NotFound("مخاطب یافت نشد");
                }

                // بررسی مالکیت دفترچه
                var notebook = await _notebookRepository.GetByIdAsync(contact.ContactNotebookId);
                if (notebook == null || notebook.UserId != userId)
                {
                    return ApiResponse<List<string>>.Forbidden("شما مجاز به ویرایش این مخاطب نیستید");
                }

                if (files == null || !files.Any())
                {
                    return ApiResponse<List<string>>.BadRequest("هیچ فایلی ارسال نشده است");
                }

                // اعتبارسنجی فایل‌ها
                var validationErrors = new List<string>();
                foreach (var file in files)
                {
                    if (file == null || file.Length == 0)
                    {
                        validationErrors.Add($"فایل '{file?.FileName ?? "نامشخص"}' خالی است");
                        continue;
                    }

                    // بررسی حجم فایل (حداکثر 50 مگابایت برای ضمیمه)
                    var maxSize = 50 * 1024 * 1024; // 50 MB
                    if (file.Length > maxSize)
                    {
                        var fileSizeMB = file.Length / (1024.0 * 1024.0);
                        validationErrors.Add($"حجم فایل '{file.FileName}' ({fileSizeMB:F2} MB) از حد مجاز (50 MB) بیشتر است");
                    }
                }

                if (validationErrors.Any())
                {
                    return ApiResponse<List<string>>.BadRequest($"خطا در اعتبارسنجی فایل‌ها: {string.Join("; ", validationErrors)}");
                }

                // آپلود فایل‌ها
                var relativePaths = await _fileUploadService.UploadMultipleFilesAsync(
                    files, 
                    FileUploadConstants.EntityType_Contact, 
                    contactId, 
                    FileUploadConstants.SubFolder_Attachments);

                // تبدیل به URL
                var fileUrls = relativePaths.Select(path => _fileUploadService.GetFileUrl(path)).ToList();

                _logger.LogInformation("تعداد {Count} فایل ضمیمه برای مخاطب {ContactId} با موفقیت آپلود شد", fileUrls.Count, contactId);

                return ApiResponse<List<string>>.CreateSuccess(fileUrls, "فایل‌های ضمیمه با موفقیت آپلود شدند");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در آپلود فایل‌های ضمیمه برای مخاطب {ContactId}", contactId);
                
                if (ex is ArgumentException || ex is InvalidOperationException)
                {
                    return ApiResponse<List<string>>.BadRequest(ex.Message);
                }
                
                throw;
            }
        }

        public async Task<ApiResponse<bool>> DeleteAttachmentFileAsync(int contactId, int userId, string filePath)
        {
            try
            {
                var contact = await _contactRepository.GetByIdAsync(contactId);
                if (contact == null)
                {
                    return ApiResponse<bool>.NotFound("مخاطب یافت نشد");
                }

                // بررسی مالکیت دفترچه
                var notebook = await _notebookRepository.GetByIdAsync(contact.ContactNotebookId);
                if (notebook == null || notebook.UserId != userId)
                {
                    return ApiResponse<bool>.Forbidden("شما مجاز به ویرایش این مخاطب نیستید");
                }

                // حذف فایل
                await _fileUploadService.DeleteFileAsync(
                    filePath, 
                    FileUploadConstants.EntityType_Contact, 
                    contactId, 
                    FileUploadConstants.SubFolder_Attachments);

                _logger.LogInformation("فایل ضمیمه {FilePath} برای مخاطب {ContactId} با موفقیت حذف شد", filePath, contactId);

                return ApiResponse<bool>.CreateSuccess(true, "فایل ضمیمه با موفقیت حذف شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در حذف فایل ضمیمه {FilePath} برای مخاطب {ContactId}", filePath, contactId);
                throw;
            }
        }

        public async Task<ApiResponse<List<string>>> GetAttachmentFilesAsync(int contactId, int userId)
        {
            try
            {
                var contact = await _contactRepository.GetByIdAsync(contactId);
                if (contact == null)
                {
                    return ApiResponse<List<string>>.NotFound("مخاطب یافت نشد");
                }

                // بررسی مالکیت دفترچه
                var notebook = await _notebookRepository.GetByIdAsync(contact.ContactNotebookId);
                if (notebook == null || notebook.UserId != userId)
                {
                    return ApiResponse<List<string>>.Forbidden("شما مجاز به دسترسی به این مخاطب نیستید");
                }

                // دریافت لیست فایل‌های ضمیمه
                var relativePaths = await _fileUploadService.ListFilesAsync(
                    FileUploadConstants.EntityType_Contact, 
                    contactId, 
                    FileUploadConstants.SubFolder_Attachments);

                // تبدیل به URL
                var fileUrls = relativePaths.Select(path => _fileUploadService.GetFileUrl(path)).ToList();

                return ApiResponse<List<string>>.CreateSuccess(fileUrls);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت لیست فایل‌های ضمیمه برای مخاطب {ContactId}", contactId);
                throw;
            }
        }
        public async Task<ApiResponse<ImportExcelResultDto>> ImportFromListAsync(int userId, ImportContactsFromListDto importDto)
        {
            try
            {
                // بررسی وجود دفترچه و مالکیت
                var notebook = await _notebookRepository.GetByIdAsync(importDto.ContactNotebookId);
                if (notebook == null)
                {
                    return ApiResponse<ImportExcelResultDto>.NotFound("دفترچه یافت نشد");
                }

                if (notebook.UserId != userId)
                {
                    return ApiResponse<ImportExcelResultDto>.Forbidden("شما مجاز به ایمپورت به این دفترچه نیستید");
                }

                var result = new ImportExcelResultDto();
                result.TotalRows = importDto.Contacts.Count;

                // دریافت لیست شماره‌های موبایل موجود در دفترچه برای بررسی تکراری
                var existingMobileNumbers = await _context.Contacts
                    .Where(c => c.ContactNotebookId == importDto.ContactNotebookId && !c.IsDeleted)
                    .Select(c => c.MobileNumber)
                    .ToHashSetAsync();

                // Regex برای اعتبارسنجی شماره موبایل
                var mobileRegex = new Regex(@"^09\d{9}$");

                var contactsToAdd = new List<Contact>();
                int index = 0;

                foreach (var item in importDto.Contacts)
                {
                    index++;
                    var mobileNumber = item.MobileNumber?.Trim() ?? "";

                    // نرمال‌سازی شماره موبایل
                    mobileNumber = NormalizeMobileNumber(mobileNumber);

                    // بررسی خالی بودن
                    if (string.IsNullOrWhiteSpace(mobileNumber))
                    {
                        result.SkippedCount++;
                        result.Errors.Add(new ImportRowError
                        {
                            RowNumber = index,
                            MobileNumber = null,
                            ErrorMessage = "شماره موبایل خالی است"
                        });
                        continue;
                    }

                    // بررسی فرمت شماره موبایل
                    if (!mobileRegex.IsMatch(mobileNumber))
                    {
                        result.SkippedCount++;
                        result.Errors.Add(new ImportRowError
                        {
                            RowNumber = index,
                            MobileNumber = mobileNumber,
                            ErrorMessage = "فرمت شماره موبایل نامعتبر است (باید با 09 شروع شود و 11 رقم باشد)"
                        });
                        continue;
                    }

                    // بررسی تکراری بودن در دفترچه
                    if (existingMobileNumbers.Contains(mobileNumber))
                    {
                        result.DuplicateCount++;
                        result.Errors.Add(new ImportRowError
                        {
                            RowNumber = index,
                            MobileNumber = mobileNumber,
                            ErrorMessage = "این شماره موبایل قبلاً در دفترچه ثبت شده است"
                        });
                        continue;
                    }

                    // بررسی تکراری بودن در لیست فعلی ایمپورت
                    if (contactsToAdd.Any(c => c.MobileNumber == mobileNumber))
                    {
                        result.DuplicateCount++;
                        result.Errors.Add(new ImportRowError
                        {
                            RowNumber = index,
                            MobileNumber = mobileNumber,
                            ErrorMessage = "این شماره موبایل در لیست تکراری است"
                        });
                        continue;
                    }

                    // چک کردن اجباری بودن نام
                    var fullName = item.Name?.Trim();
                    if (string.IsNullOrWhiteSpace(fullName))
                    {
                        result.SkippedCount++;
                        result.Errors.Add(new ImportRowError
                        {
                            RowNumber = index,
                            MobileNumber = mobileNumber,
                            ErrorMessage = "نام کامل مخاطب الزامی است"
                        });
                        continue;
                    }

                    // ایجاد مخاطب
                    var contact = new Contact
                    {
                        ContactNotebookId = importDto.ContactNotebookId,
                        MobileNumber = mobileNumber,
                        FullName = fullName,
                        Tags = null, // فیلد Tags دیگر استفاده نمی‌شود
                        CreatedAt = DateTime.UtcNow
                    };

                    await _context.Contacts.AddAsync(contact);
                    await _context.SaveChangesAsync(); // Save برای گرفتن ContactId

                    // ImportFromList فیلد Tags ندارد - اگر نیاز باشد می‌توان در آینده اضافه کرد

                    contactsToAdd.Add(contact);
                    existingMobileNumbers.Add(mobileNumber); // برای جلوگیری از تکراری در همین لیست
                }

                // مخاطبین در داخل loop ذخیره شده‌اند (برای گرفتن ContactId)
                // فقط بررسی نهایی و به‌روزرسانی دفترچه
                if (contactsToAdd.Any())
                {
                    // به‌روزرسانی تاریخ ویرایش دفترچه
                    notebook.UpdatedAt = DateTime.UtcNow;
                    await _notebookRepository.UpdateAsync(notebook);

                    result.SuccessCount = contactsToAdd.Count;

                    _logger.LogInformation(
                        "ایمپورت لیست با موفقیت انجام شد. دفترچه: {NotebookId}, موفق: {Success}, تکراری: {Duplicate}, رد شده: {Skipped}",
                        importDto.ContactNotebookId, result.SuccessCount, result.DuplicateCount, result.SkippedCount);
                }

                var message = $"ایمپورت با موفقیت انجام شد. {result.SuccessCount} مخاطب اضافه شد";
                if (result.DuplicateCount > 0)
                    message += $"، {result.DuplicateCount} مورد تکراری";
                if (result.SkippedCount > 0)
                    message += $"، {result.SkippedCount} مورد رد شده";

                return ApiResponse<ImportExcelResultDto>.CreateSuccess(result, message, 200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing contacts from List for notebook: {NotebookId}", importDto.ContactNotebookId);
                throw;
            }
        }

        /// <summary>
        /// دریافت لیست دفترچه‌های تلفن یک کاربر
        /// </summary>
        public async Task<ApiResponse<List<ContactNotebookResponseDto>>> GetUserNotebooksAsync(int userId)
        {
            try
            {
                // بررسی وجود کاربر
                var userExists = await _context.Users.AnyAsync(u => u.Id == userId && !u.IsDeleted);
                if (!userExists)
                {
                    _logger.LogWarning("User not found with ID: {UserId}", userId);
                    return ApiResponse<List<ContactNotebookResponseDto>>.NotFound("کاربر یافت نشد");
                }

                // دریافت دفترچه‌های کاربر
                var notebooks = await _notebookRepository.GetByUserIdAsync(userId);
                var notebooksList = notebooks.ToList();

                var notebookDtos = new List<ContactNotebookResponseDto>();
                foreach (var notebook in notebooksList)
                {
                    // شمارش مخاطبین
                    var contactsCount = await _context.Contacts
                        .CountAsync(c => c.ContactNotebookId == notebook.Id && !c.IsDeleted);

                    var notebookDto = new ContactNotebookResponseDto
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
                        notebookDto.IconUrl = _fileUploadService.GetFileUrl(notebook.Icon);
                    }

                    notebookDtos.Add(notebookDto);
                }

                _logger.LogInformation("Retrieved {Count} notebooks for user {UserId}", notebookDtos.Count, userId);

                return ApiResponse<List<ContactNotebookResponseDto>>.CreateSuccess(notebookDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notebooks for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// اختصاص تگ‌ها به مخاطب
        /// </summary>
        public async Task<ApiResponse<bool>> AssignTagsToContactAsync(int contactId, int userId, AssignTagsToContactDto assignDto)
        {
            try
            {
                // بررسی وجود مخاطب
                var contact = await _contactRepository.GetByIdAsync(contactId);
                if (contact == null)
                {
                    return ApiResponse<bool>.NotFound("مخاطب یافت نشد");
                }

                // بررسی مالکیت دفترچه
                var notebook = await _notebookRepository.GetByIdAsync(contact.ContactNotebookId);
                if (notebook == null || notebook.UserId != userId)
                {
                    return ApiResponse<bool>.Forbidden("شما مجاز به ویرایش این مخاطب نیستید");
                }

                // بررسی معتبر بودن تگ‌ها
                var validTagIds = assignDto.TagIds.Where(id => id > 0).Distinct().ToList();
                if (!validTagIds.Any())
                {
                    return ApiResponse<bool>.BadRequest("هیچ تگ معتبری انتخاب نشده است");
                }

                // بررسی اینکه تگ‌ها متعلق به کاربر هستند
                var userTags = await _context.MessageTags
                    .Where(t => t.UserId == userId && validTagIds.Contains(t.Id) && !t.IsDeleted && t.IsActive)
                    .Select(t => t.Id)
                    .ToListAsync();

                var invalidTagIds = validTagIds.Except(userTags).ToList();
                if (invalidTagIds.Any())
                {
                    return ApiResponse<bool>.BadRequest($"تگ‌های با شناسه‌های [{string.Join(", ", invalidTagIds)}] یافت نشد یا به شما تعلق ندارند");
                }

                // استفاده از Transaction
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // دریافت تگ‌های فعلی مخاطب
                    var existingContactTags = await _context.ContactTags
                        .Where(ct => ct.ContactId == contactId)
                        .Select(ct => ct.TagId)
                        .ToListAsync();

                    // تگ‌هایی که باید اضافه شوند
                    var tagsToAdd = userTags.Except(existingContactTags).ToList();

                    // تگ‌هایی که باید حذف شوند (اختیاری - می‌توانیم فقط اضافه کنیم)
                    // برای حال حاضر، فقط تگ‌های جدید را اضافه می‌کنیم

                    // اضافه کردن تگ‌های جدید
                    if (tagsToAdd.Any())
                    {
                        var newContactTags = tagsToAdd.Select(tagId => new ContactTag
                        {
                            ContactId = contactId,
                            TagId = tagId,
                            CreatedAt = DateTime.UtcNow
                        }).ToList();

                        await _context.ContactTags.AddRangeAsync(newContactTags);
                        await _context.SaveChangesAsync();
                    }

                    await transaction.CommitAsync();

                    _logger.LogInformation("Tags assigned to contact {ContactId}. Added {Count} tags", 
                        contactId, tagsToAdd.Count);

                    return ApiResponse<bool>.CreateSuccess(true, $"{tagsToAdd.Count} تگ با موفقیت به مخاطب اضافه شد");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error assigning tags to contact {ContactId}", contactId);
                    throw;
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error assigning tags to contact {ContactId}", contactId);
                return ApiResponse<bool>.InternalServerError("خطا در اختصاص تگ‌ها. لطفاً دوباره تلاش کنید");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error assigning tags to contact {ContactId}", contactId);
                return ApiResponse<bool>.InternalServerError("خطای غیرمنتظره در اختصاص تگ‌ها");
            }
        }
    }
}


