using Api_Vapp._Utilities;
using Api_Vapp.Data;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Repositories
{
    public class UserFormRepository : BaseRepository<UserForm>, IUserFormRepository
    {
        public UserFormRepository(Api_Context context) : base(context)
        {
        }

        public override async Task<UserForm?> GetByIdAsync(int id)
        {
            return await _dbSet
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted);
        }

        public async Task<UserForm?> GetByIdWithDetailsReadOnlyAsync(int id)
        {
            return await _dbSet
                .AsNoTracking()
                .AsSplitQuery()
                .Include(f => f.Fields.OrderBy(field => field.DisplayOrder))
                .Include(f => f.Notebooks)
                .FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted);
        }

        public async Task<UserForm?> GetByIdWithDetailsTrackedAsync(int id)
        {
            return await _dbSet
                .AsSplitQuery()
                .Include(f => f.Fields.OrderBy(field => field.DisplayOrder))
                .Include(f => f.Notebooks)
                .FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted);
        }

        public async Task<UserForm?> GetByIdWithDetailsTrackedForUserAsync(int id, int userId)
        {
            return await _dbSet
                .AsSplitQuery()
                .Include(f => f.Fields.OrderBy(field => field.DisplayOrder))
                .Include(f => f.Notebooks)
                .FirstOrDefaultAsync(f =>
                    f.Id == id &&
                    f.UserId == userId &&
                    !f.IsDeleted);
        }

        public async Task<UserForm?> GetOwnedFormAsync(int id, int userId, bool tracked = false)
        {
            var query = tracked ? _dbSet.AsQueryable() : _dbSet.AsNoTracking();

            return await query.FirstOrDefaultAsync(f =>
                f.Id == id &&
                f.UserId == userId &&
                !f.IsDeleted);
        }

        public async Task<UserForm?> GetBySlugReadOnlyAsync(string slug)
        {
            return await _dbSet
                .AsNoTracking()
                .AsSplitQuery()
                .Include(f => f.Fields.Where(field => field.IsActive).OrderBy(field => field.DisplayOrder))
                .FirstOrDefaultAsync(f =>
                    f.Slug == slug &&
                    !f.IsDeleted &&
                    f.Status == UserFormStatus.Published &&
                    f.IsActive);
        }

        public async Task<bool> SlugExistsAsync(string slug, int? excludeFormId = null)
        {
            var query = _dbSet
                .AsNoTracking()
                .Where(f => f.Slug == slug && !f.IsDeleted);

            if (excludeFormId.HasValue)
            {
                query = query.Where(f => f.Id != excludeFormId.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<IReadOnlyList<string>> GetExistingSlugsWithPrefixAsync(string slugPrefix, int? excludeFormId = null)
        {
            var query = _dbSet
                .AsNoTracking()
                .Where(f =>
                    f.Slug != null &&
                    f.Slug.StartsWith(slugPrefix) &&
                    !f.IsDeleted);

            if (excludeFormId.HasValue)
            {
                query = query.Where(f => f.Id != excludeFormId.Value);
            }

            return await query
                .Select(f => f.Slug!)
                .ToListAsync();
        }

        public async Task<(IReadOnlyList<UserForm> Items, int TotalCount)> GetByUserIdPagedAsync(
            int userId,
            int pageNumber,
            int pageSize)
        {
            var query = _dbSet
                .AsNoTracking()
                .Where(f => f.UserId == userId && !f.IsDeleted);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(f => f.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(f => new UserForm
                {
                    Id = f.Id,
                    Title = f.Title,
                    Slug = f.Slug,
                    Status = f.Status,
                    IsActive = f.IsActive,
                    CreatedAt = f.CreatedAt,
                    PublishedAt = f.PublishedAt
                })
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<IReadOnlyList<UserFormField>> GetFieldsReadOnlyAsync(int userFormId)
        {
            return await _context.UserFormFields
                .AsNoTracking()
                .Where(f => f.UserFormId == userFormId)
                .OrderBy(f => f.DisplayOrder)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<int>> GetNotebookIdsAsync(int userFormId)
        {
            return await _context.UserFormNotebooks
                .AsNoTracking()
                .Where(n => n.UserFormId == userFormId)
                .Select(n => n.ContactNotebookId)
                .ToListAsync();
        }

        public async Task AddSubmissionAsync(UserFormSubmission submission)
        {
            await _context.UserFormSubmissions.AddAsync(submission);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> HasSubmissionWithMobileAsync(int userFormId, string mobile)
        {
            return await _context.UserFormSubmissions
                .AsNoTracking()
                .AnyAsync(s =>
                    s.UserFormId == userFormId &&
                    s.ParticipantMobile == mobile);
        }

        public async Task<int> GetSubmissionCountAsync(int userFormId)
        {
            return await _context.UserFormSubmissions
                .AsNoTracking()
                .CountAsync(s => s.UserFormId == userFormId);
        }

        public async Task<(IReadOnlyList<UserFormSubmission> Items, int TotalCount)> GetSubmissionsPagedAsync(
            int userFormId,
            int pageNumber,
            int pageSize,
            string? searchTerm = null,
            DateTime? fromUtc = null,
            DateTime? toUtc = null)
        {
            var query = BuildSubmissionsQuery(userFormId, searchTerm, fromUtc, toUtc);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Include(s => s.FieldValues)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<IReadOnlyList<UserFormSubmission>> GetSubmissionsForExportAsync(int userFormId)
        {
            return await _context.UserFormSubmissions
                .AsNoTracking()
                .Where(s => s.UserFormId == userFormId)
                .OrderByDescending(s => s.CreatedAt)
                .Include(s => s.FieldValues)
                .ToListAsync();
        }

        private IQueryable<UserFormSubmission> BuildSubmissionsQuery(
            int userFormId,
            string? searchTerm,
            DateTime? fromUtc,
            DateTime? toUtc)
        {
            var query = _context.UserFormSubmissions
                .AsNoTracking()
                .Where(s => s.UserFormId == userFormId);

            if (fromUtc.HasValue)
            {
                query = query.Where(s => s.CreatedAt >= fromUtc.Value);
            }

            if (toUtc.HasValue)
            {
                query = query.Where(s => s.CreatedAt < toUtc.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                query = query.Where(s =>
                    s.ParticipantFullName.Contains(term) ||
                    s.ParticipantMobile.Contains(term) ||
                    s.FieldValues.Any(v =>
                        v.Value != null &&
                        v.Value.Contains(term)));
            }

            return query;
        }
    }
}
