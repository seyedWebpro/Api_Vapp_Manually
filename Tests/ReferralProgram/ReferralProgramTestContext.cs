using Api_Vapp.Data;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.ReferralProgram;
using Api_Vapp.DTOs.Sms;
using Api_Vapp.Interfaces;
using Api_Vapp.Models;
using Api_Vapp.Repositories;
using Api_Vapp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api_Vapp.Tests.ReferralProgram;

internal sealed class ReferralProgramTestContext : IDisposable
{
    private static readonly SemaphoreSlim MigrationLock = new(1, 1);
    private static bool _migrationsApplied;

    private readonly Api_Context _context;

    private ReferralProgramTestContext(Api_Context context)
    {
        _context = context;
    }

    public IReferralProgramService Service { get; private set; } = null!;

    public Api_Context Context => _context;

    public int OwnerUserId { get; private set; }

    public int OtherUserId { get; private set; }

    public int NotebookId { get; private set; }

    public int ContactId { get; private set; }

    public int ContactId2 { get; private set; }

    public int ContactId3 { get; private set; }

    public static async Task<ReferralProgramTestContext> CreateAsync()
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

        var testContext = new ReferralProgramTestContext(context);
        await testContext.InitializeAsync();
        return testContext;
    }

    public ReferralStep1Dto BuildStep1Dto(Action<ReferralStep1Dto>? configure = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var dto = new ReferralStep1Dto
        {
            Title = $"برنامه تست {suffix}",
            IsActive = true,
            RewardType = ReferralRewardTypes.FixedAmount,
            ReferrerRewardValue = 50_000m,
            IsCustomerRewardActive = true,
            CustomerRewardValue = 10_000m
        };

        configure?.Invoke(dto);
        return dto;
    }

    public SaveReferralStep3SettingsDto BuildStep3Settings(Action<SaveReferralStep3SettingsDto>? configure = null)
    {
        var dto = new SaveReferralStep3SettingsDto
        {
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(30)
        };

        configure?.Invoke(dto);
        return dto;
    }

    public async Task<(int ProgramId, string PublicCode)> CreateConfirmedProgramAsync(
        Action<ReferralStep1Dto>? configureStep1 = null)
    {
        var step1 = BuildStep1Dto(configureStep1);
        var step1Result = await Service.ValidateStep1Async(OwnerUserId, step1);
        if (!step1Result.Success || step1Result.Data?.DraftId == null)
        {
            throw new InvalidOperationException($"Step1 failed: {step1Result.StatusCode} {step1Result.Message}");
        }

        var draftId = step1Result.Data.DraftId;
        var step2Result = await Service.ValidateStep2Async(OwnerUserId, new ReferralStep2Dto
        {
            DraftId = draftId,
            TargetAudience = ReferralTargetAudience.All
        });
        if (!step2Result.Success)
        {
            throw new InvalidOperationException($"Step2 failed: {step2Result.StatusCode} {step2Result.Message}");
        }

        var step3Result = await Service.SaveStep3SettingsAsync(OwnerUserId, new SaveReferralStep3RequestDto
        {
            DraftId = draftId,
            Settings = BuildStep3Settings()
        });
        if (!step3Result.Success)
        {
            throw new InvalidOperationException($"Step3 failed: {step3Result.StatusCode} {step3Result.Message}");
        }

        var confirmResult = await Service.ConfirmAsync(OwnerUserId, new ConfirmReferralProgramDto
        {
            DraftId = draftId
        });
        if (!confirmResult.Success || confirmResult.Data?.Program == null)
        {
            throw new InvalidOperationException($"Confirm failed: {confirmResult.StatusCode} {confirmResult.Message}");
        }

        return (confirmResult.Data.Program.Id, confirmResult.Data.Program.PublicCode);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private async Task InitializeAsync()
    {
        Service = CreateService(_context);
        await SeedAsync();
    }

    private static IReferralProgramService CreateService(Api_Context context)
    {
            return new ReferralProgramService(
                context,
                new ReferralProgramRepository(context),
                new ReferralProgramDraftRepository(context),
                new ReferralUsageRepository(context),
                new FakeUserSmsBillingService(),
                new Api_Vapp.Tests.Shared.NoOpAuditService(),
                NullLogger<ReferralProgramService>.Instance);
    }

    private async Task SeedAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var owner = new User
        {
            PhoneNumber = $"0915{suffix[..7]}",
            PasswordHash = "hash-owner",
            FullName = "کاربر پاداش",
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
            Name = $"دفترچه پاداش {suffix}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.ContactNotebooks.Add(notebook);
        await _context.SaveChangesAsync();
        NotebookId = notebook.Id;

        var contact = new Contact
        {
            ContactNotebookId = NotebookId,
            MobileNumber = $"0917{suffix[..7]}",
            FullName = "مخاطب تست",
            CreatedAt = DateTime.UtcNow
        };

        var contact2 = new Contact
        {
            ContactNotebookId = NotebookId,
            MobileNumber = $"0918{suffix[..7]}",
            FullName = "مخاطب دوم",
            CreatedAt = DateTime.UtcNow
        };

        var contact3 = new Contact
        {
            ContactNotebookId = NotebookId,
            MobileNumber = $"0919{suffix[..7]}",
            FullName = "مخاطب سوم",
            CreatedAt = DateTime.UtcNow
        };

        _context.Contacts.AddRange(contact, contact2, contact3);
        await _context.SaveChangesAsync();
        ContactId = contact.Id;
        ContactId2 = contact2.Id;
        ContactId3 = contact3.Id;
    }

    private sealed class FakeUserSmsBillingService : IUserSmsBillingService
    {
        public Task<(decimal Cost, int PartsCount)> EstimateCostAsync(
            string message,
            CancellationToken cancellationToken = default) =>
            Task.FromResult((160m, 1));

        public Task<UserSmsSendResult> TrySendAsync(
            int userId,
            string mobile,
            string message,
            string sourceModule,
            string walletTitle,
            string? walletDescription = null,
            int? sourceEntityId = null,
            string? sourceEntityLabel = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(UserSmsSendResult.Success(1, 160m, 1, 0));

        public Task<UserSmsSendResult> TrySendOtpAsync(
            int userId,
            string mobile,
            string otpCode,
            string templateType,
            string sourceModule,
            string walletTitle,
            string? walletDescription = null,
            int? sourceEntityId = null,
            string? sourceEntityLabel = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(UserSmsSendResult.Success(1, 160m, 1, 0));
    }
}
