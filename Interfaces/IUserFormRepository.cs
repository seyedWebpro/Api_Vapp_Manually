using Api_Vapp.Models;
using Api_Vapp._Utilities;

namespace Api_Vapp.Interfaces
{
    public interface IUserFormRepository : IBaseRepository<UserForm>
    {
        Task<UserForm?> GetByIdWithDetailsReadOnlyAsync(int id);

        Task<UserForm?> GetByIdWithDetailsTrackedAsync(int id);

        Task<UserForm?> GetByIdWithDetailsTrackedForUserAsync(int id, int userId);

        Task<UserForm?> GetOwnedFormAsync(int id, int userId, bool tracked = false);

        Task<UserForm?> GetBySlugReadOnlyAsync(string slug);

        Task<bool> SlugExistsAsync(string slug, int? excludeFormId = null);

        Task<IReadOnlyList<string>> GetExistingSlugsWithPrefixAsync(string slugPrefix, int? excludeFormId = null);

        Task<(IReadOnlyList<UserForm> Items, int TotalCount)> GetByUserIdPagedAsync(
            int userId,
            int pageNumber,
            int pageSize);

        Task<IReadOnlyList<UserFormField>> GetFieldsReadOnlyAsync(int userFormId);

        Task<IReadOnlyList<int>> GetNotebookIdsAsync(int userFormId);
    }
}
