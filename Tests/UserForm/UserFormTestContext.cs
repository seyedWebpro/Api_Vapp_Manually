using Api_Vapp.Data;
using Api_Vapp.DTOs.File;
using Api_Vapp.DTOs.UserForm;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Repositories;
using Api_Vapp.Services;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Api_Vapp.Tests.UserForm;

internal sealed class UserFormTestContext : IDisposable
{
    private static readonly SemaphoreSlim MigrationLock = new(1, 1);
    private static bool _migrationsApplied;

    private readonly Api_Context _context;
    private IDbContextTransaction? _transaction;

    private UserFormTestContext(Api_Context context)
    {
        _context = context;
    }

    public IUserFormService Service { get; private set; } = null!;

    public FakeFileUploadService FileUploadService { get; private set; } = null!;

    public int OwnerUserId { get; private set; }

    public int OtherUserId { get; private set; }

    public int NotebookId { get; private set; }

    public Api_Context Context => _context;

    public static async Task<UserFormTestContext> CreateAsync()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("VAPP_TEST_CONNECTION")
            ?? "Server=localhost,1436;Database=DbVapp_UserFormTests;User Id=sa;Password=Vapp@Secure2025!;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=true";

        var options = new DbContextOptionsBuilder<Api_Context>()
            .UseSqlServer(connectionString)
            .Options;

        var context = new Api_Context(options);

        await MigrationLock.WaitAsync();
        try
        {
            if (!_migrationsApplied)
            {
                await context.Database.MigrateAsync();
                _migrationsApplied = true;
            }
        }
        finally
        {
            MigrationLock.Release();
        }

        var testContext = new UserFormTestContext(context);
        await testContext.InitializeAsync();
        return testContext;
    }

    public async Task BeginTestTransactionAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task RollbackTestTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public static List<UserFormFieldDto> SampleFields(bool includeMobile = true)
    {
        var fields = new List<UserFormFieldDto>
        {
            new()
            {
                FieldKey = "full_name",
                FieldType = "text",
                Label = "نام و نام خانوادگی",
                Placeholder = "مثلا علی رضایی",
                DisplayOrder = 1,
                IsActive = true,
                IsRequired = true
            }
        };

        if (includeMobile)
        {
            fields.Add(new UserFormFieldDto
            {
                FieldKey = "mobile",
                FieldType = "mobile",
                Label = "شماره موبایل",
                Placeholder = "مثلا 0912...",
                DisplayOrder = 2,
                IsActive = true,
                IsRequired = true
            });
        }

        return fields;
    }

    public CreateUserFormDto BuildCreateDto(Action<CreateUserFormDto>? configure = null)
    {
        var dto = new CreateUserFormDto
        {
            TemplateKey = "recruitment",
            Title = "درخواست استخدام",
            Description = "لطفا اطلاعات را کامل وارد کنید",
            Fields = SampleFields()
        };

        configure?.Invoke(dto);
        return dto;
    }

    public async Task<int> CreateDraftAsync(Action<CreateUserFormDto>? configure = null)
    {
        var result = await Service.CreateDraftAsync(OwnerUserId, BuildCreateDto(configure));
        if (!result.Success || result.StatusCode != 201 || result.Data == null)
        {
            throw new InvalidOperationException($"Expected draft create success, got {result.StatusCode}: {result.Message}");
        }

        return result.Data.Id;
    }

    public async Task<int> CreatePublishedFormAsync(string? slug = "job-alpha")
    {
        var formId = await CreateDraftAsync();
        var publish = await Service.PublishAsync(formId, OwnerUserId, new PublishUserFormDto { Slug = slug });
        if (!publish.Success)
        {
            throw new InvalidOperationException($"Expected publish success, got {publish.StatusCode}: {publish.Message}");
        }

        return formId;
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }

    private async Task InitializeAsync()
    {
        FileUploadService = new FakeFileUploadService();
        Service = CreateService(_context, FileUploadService);
        await SeedAsync();
    }

    private static IUserFormService CreateService(Api_Context context, FakeFileUploadService fileUploadService)
    {
        var repository = new UserFormRepository(context);
        var options = Options.Create(new FormBuilderOptions
        {
            PublicBaseUrl = "https://app.com/form"
        });

        return new UserFormService(
            repository,
            context,
            options,
            fileUploadService,
            NullLogger<UserFormService>.Instance);
    }

    private async Task SeedAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var owner = new User
        {
            PhoneNumber = $"0913{suffix[..7]}",
            PasswordHash = "hash-owner",
            FullName = "کاربر اصلی",
            IsActive = true,
            IsPhoneVerified = true,
            CreatedAt = DateTime.UtcNow
        };

        var other = new User
        {
            PhoneNumber = $"0914{suffix[..7]}",
            PasswordHash = "hash-other",
            FullName = "کاربر دیگر",
            IsActive = true,
            IsPhoneVerified = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.AddRange(owner, other);
        await _context.SaveChangesAsync();

        OwnerUserId = owner.Id;
        OtherUserId = other.Id;

        var notebook = new ContactNotebook
        {
            UserId = OwnerUserId,
            Name = $"دفترچه تست {suffix}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.ContactNotebooks.Add(notebook);
        await _context.SaveChangesAsync();
        NotebookId = notebook.Id;
    }
}
