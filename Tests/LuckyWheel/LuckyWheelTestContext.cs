using Api_Vapp.Data;
using Api_Vapp.DTOs.LuckyWheel;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Repositories;
using Api_Vapp.Services;
using Api_Vapp.Tests.UserForm;
using Api_Vapp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Api_Vapp.Tests.LuckyWheel;

internal sealed class LuckyWheelTestContext : IDisposable
{
    private static readonly SemaphoreSlim MigrationLock = new(1, 1);
    private static bool _migrationsApplied;

    private readonly Api_Context _context;
    private IDbContextTransaction? _transaction;

    private LuckyWheelTestContext(Api_Context context)
    {
        _context = context;
    }

    public ILuckyWheelService Service { get; private set; } = null!;

    public int OwnerUserId { get; private set; }

    public int OtherUserId { get; private set; }

    public int NotebookId { get; private set; }

    public static async Task<LuckyWheelTestContext> CreateAsync()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("VAPP_TEST_CONNECTION")
            ?? "Server=localhost,1436;Database=DbVapp_LuckyWheelTests;User Id=sa;Password=Vapp@Secure2025!;TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=true";

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

        var testContext = new LuckyWheelTestContext(context);
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

    public static List<LuckyWheelItemDto> SampleItems(decimal thirdProbability = 40m)
    {
        return
        [
            new LuckyWheelItemDto { Name = "۱۰٪ تخفیف", Probability = 30, DisplayOrder = 1 },
            new LuckyWheelItemDto { Name = "۲۰٪ تخفیف", Probability = 30, DisplayOrder = 2 },
            new LuckyWheelItemDto { Name = "پوچ", Probability = thirdProbability, DisplayOrder = 3 }
        ];
    }

    public CreateLuckyWheelDto BuildCreateDto(Action<CreateLuckyWheelDto>? configure = null)
    {
        var dto = new CreateLuckyWheelDto
        {
            Title = "گردونه نوروز",
            Description = "شانس خود را امتحان کنید"
        };

        configure?.Invoke(dto);
        return dto;
    }

    public async Task<int> CreateDraftAsync(Action<CreateLuckyWheelDto>? configure = null)
    {
        var result = await Service.CreateDraftAsync(OwnerUserId, BuildCreateDto(configure));
        if (!result.Success || result.StatusCode != 201 || result.Data == null)
        {
            throw new InvalidOperationException($"Expected draft create success, got {result.StatusCode}: {result.Message}");
        }

        return result.Data.Id;
    }

    public async Task<int> CreateWheelWithItemsAsync()
    {
        var wheelId = await CreateDraftAsync();
        var update = await Service.UpdateAsync(wheelId, OwnerUserId, new UpdateLuckyWheelDto
        {
            Items = SampleItems()
        });

        if (!update.Success)
        {
            throw new InvalidOperationException($"Expected items update success, got {update.StatusCode}: {update.Message}");
        }

        return wheelId;
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }

    private async Task InitializeAsync()
    {
        Service = CreateService(_context);
        await SeedAsync();
    }

    private static ILuckyWheelService CreateService(Api_Context context)
    {
        var repository = new LuckyWheelRepository(context);
        var options = Options.Create(new LuckyWheelOptions
        {
            PublicBaseUrl = "https://app.com/wheel"
        });

        return new LuckyWheelService(
            repository,
            context,
            options,
            new FakeFileUploadService(),
            NullLogger<LuckyWheelService>.Instance);
    }

    private async Task SeedAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var owner = new User
        {
            PhoneNumber = $"0915{suffix[..7]}",
            PasswordHash = "hash-owner",
            FullName = "کاربر اصلی",
            IsActive = true,
            IsPhoneVerified = true,
            CreatedAt = DateTime.UtcNow
        };

        var other = new User
        {
            PhoneNumber = $"0916{suffix[..7]}",
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
