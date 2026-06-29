using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Api_Vapp.Tests.UserForm;

internal sealed class FakeFileUploadService : IFileUploadService
{
    public List<(string EntityType, int EntityId)> DeletedEntities { get; } = new();

    public Task<int> DeleteAllEntityFilesAsync(string entityType, int entityId, string? subFolder = null)
    {
        DeletedEntities.Add((entityType, entityId));
        return Task.FromResult(0);
    }

    public Task DeleteFileAsync(string filePath, string entityType, int entityId, string? subFolder = null)
        => Task.CompletedTask;

    public Task<int> DeleteOldTicketFilesAsync(int daysOld, int? ticketId = null)
        => Task.FromResult(0);

    public Task<bool> FileExistsAsync(string filePath, string entityType, int entityId, string? subFolder = null)
        => Task.FromResult(false);

    public string GetFileUrl(string relativePath) => relativePath;

    public Task<List<string>> ListFilesAsync(string entityType, int entityId, string? subFolder = null)
        => Task.FromResult(new List<string>());

    public Task<string> UploadFileAsync(IFormFile file, string entityType, int entityId, string? subFolder = null)
        => Task.FromResult("test.jpg");

    public Task<List<string>> UploadMultipleFilesAsync(
        List<IFormFile> files,
        string entityType,
        int entityId,
        string? subFolder = null)
        => Task.FromResult(new List<string>());
}
