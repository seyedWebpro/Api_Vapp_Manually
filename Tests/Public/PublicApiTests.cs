using Api_Vapp.Configuration;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.LuckyWheel;
using Api_Vapp.DTOs.Public;
using Api_Vapp.DTOs.Sms;
using Api_Vapp.DTOs.UserForm;
using Api_Vapp.Interfaces;
using Api_Vapp.Repositories;
using Api_Vapp.Services;
using Api_Vapp.Tests.LuckyWheel;
using Api_Vapp.Tests.UserForm;
using Api_Vapp.Utilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api_Vapp.Tests.Public;

public class PublicApiTests : IAsyncLifetime
{
    private UserFormTestContext _formCtx = null!;
    private LuckyWheelTestContext _wheelCtx = null!;
    private IUserFormPublicService _formPublicService = null!;
    private ILuckyWheelPublicService _wheelPublicService = null!;
    private FakeSmsService _sms = null!;

    public async Task InitializeAsync()
    {
        _formCtx = await UserFormTestContext.CreateAsync();
        _wheelCtx = await LuckyWheelTestContext.CreateAsync();
        _sms = new FakeSmsService();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "VappShop_SuperSecretKey_2024_MustBeAtLeast32CharactersLongForHMACSHA256"
            })
            .Build();

        var options = Options.Create(new PublicParticipantOptions { SessionMinutes = 120 });
        var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 10_000 });

        var formSessionService = new PublicParticipantSessionService(
            _formCtx.Context,
            options,
            configuration,
            NullLogger<PublicParticipantSessionService>.Instance);
        var formOtpService = new PublicParticipantOtpService(
            _formCtx.Context,
            cache,
            _sms,
            new PublicApiFakeUserSmsBillingService(),
            new FakeHostEnvironment(),
            NullLogger<PublicParticipantOtpService>.Instance);

        _formPublicService = new UserFormPublicService(
            new UserFormRepository(_formCtx.Context),
            _formCtx.Context,
            new PublicPhonebookService(_formCtx.Context),
            formSessionService,
            formOtpService,
            NullLogger<UserFormPublicService>.Instance);

        var wheelSessionService = new PublicParticipantSessionService(
            _wheelCtx.Context,
            options,
            configuration,
            NullLogger<PublicParticipantSessionService>.Instance);
        var wheelOtpService = new PublicParticipantOtpService(
            _wheelCtx.Context,
            cache,
            _sms,
            new PublicApiFakeUserSmsBillingService(),
            new FakeHostEnvironment(),
            NullLogger<PublicParticipantOtpService>.Instance);

        _wheelPublicService = new LuckyWheelPublicService(
            new LuckyWheelRepository(_wheelCtx.Context),
            _wheelCtx.Context,
            new PublicPhonebookService(_wheelCtx.Context),
            wheelSessionService,
            wheelOtpService,
            new FakeHostEnvironment(),
            NullLogger<LuckyWheelPublicService>.Instance);
    }

    public Task DisposeAsync()
    {
        _formCtx.Dispose();
        _wheelCtx.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetPublicForm_PendingApproval_Returns403()
    {
        var slug = $"pending-{Guid.NewGuid():N}"[..20];
        await _formCtx.CreatePublishedFormAsync(slug, approveForPublic: false);

        var result = await _formPublicService.GetPublicFormAsync(slug);

        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(ErrorCodes.ContentPendingApproval, result.ErrorCode);
        Assert.Contains("منتشر نشده", result.Message);
    }

    [Fact]
    public async Task GetPublicForm_ValidSlug_Returns200()
    {
        var slug = $"contact-{Guid.NewGuid():N}"[..20];
        await _formCtx.CreatePublishedFormAsync(slug);

        var result = await _formPublicService.GetPublicFormAsync(slug);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.NotEmpty(result.Data!.Fields);
    }

    [Fact]
    public async Task SubmitPublicForm_WithRegisterAndOtp_Returns201()
    {
        var slug = $"submit-{Guid.NewGuid():N}"[..20];
        await _formCtx.CreatePublishedFormAsync(slug);

        var register = await _formPublicService.RegisterAsync(slug, new RegisterPublicParticipantDto
        {
            FirstName = "علی",
            LastName = "رضایی",
            ParticipantMobile = "09121112233"
        });

        Assert.True(register.Success, $"Register failed: status={register.StatusCode}, code={register.ErrorCode}, message={register.Message}");
        Assert.False(string.IsNullOrWhiteSpace(register.Data!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(register.Data.OtpCode));

        var verify = await _formPublicService.VerifyOtpAsync(slug, new VerifyPublicParticipantOtpDto
        {
            AccessToken = register.Data.AccessToken,
            OtpCode = register.Data.OtpCode!
        });
        Assert.True(verify.Success);
        Assert.True(verify.Data!.IsPhoneVerified);

        var result = await _formPublicService.SubmitFormAsync(slug, new SubmitFormPublicDto
        {
            AccessToken = register.Data.AccessToken,
            Values = new Dictionary<string, string?>()
        });

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
    }

    [Fact]
    public async Task VerifyPublicFormOtp_WrongCode_ReturnsOtpIncorrect()
    {
        var slug = $"badotp-{Guid.NewGuid():N}"[..20];
        await _formCtx.CreatePublishedFormAsync(slug);

        var register = await _formPublicService.RegisterAsync(slug, new RegisterPublicParticipantDto
        {
            FirstName = "علی",
            LastName = "رضایی",
            ParticipantMobile = "09121115566"
        });
        Assert.True(register.Success, $"Register failed: status={register.StatusCode}, code={register.ErrorCode}, message={register.Message}");

        var verify = await _formPublicService.VerifyOtpAsync(slug, new VerifyPublicParticipantOtpDto
        {
            AccessToken = register.Data!.AccessToken,
            OtpCode = "0000"
        });

        Assert.False(verify.Success);
        Assert.Equal(400, verify.StatusCode);
        Assert.Equal(ErrorCodes.OtpIncorrect, verify.ErrorCode);
        Assert.Equal(ControlledErrorHelper.OtpIncorrect, verify.Message);
    }

    [Fact]
    public async Task SubmitPublicForm_WithoutOtp_Returns400()
    {
        var slug = $"nootp-{Guid.NewGuid():N}"[..20];
        await _formCtx.CreatePublishedFormAsync(slug);

        var register = await _formPublicService.RegisterAsync(slug, new RegisterPublicParticipantDto
        {
            FirstName = "علی",
            LastName = "رضایی",
            ParticipantMobile = "09121116677"
        });
        Assert.True(register.Success, $"Register failed: status={register.StatusCode}, code={register.ErrorCode}, message={register.Message}");

        var result = await _formPublicService.SubmitFormAsync(slug, new SubmitFormPublicDto
        {
            AccessToken = register.Data!.AccessToken,
            Values = new Dictionary<string, string?>()
        });

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task SubmitPublicForm_DuplicateMobile_Returns400()
    {
        var slug = $"dupform-{Guid.NewGuid():N}"[..20];
        await _formCtx.CreatePublishedFormAsync(slug);

        var firstRegister = await _formPublicService.RegisterAsync(slug, new RegisterPublicParticipantDto
        {
            FirstName = "علی",
            LastName = "رضایی",
            ParticipantMobile = "09121113344"
        });
        Assert.True(firstRegister.Success, $"First register failed: status={firstRegister.StatusCode}, code={firstRegister.ErrorCode}, message={firstRegister.Message}");

        var verify = await _formPublicService.VerifyOtpAsync(slug, new VerifyPublicParticipantOtpDto
        {
            AccessToken = firstRegister.Data!.AccessToken,
            OtpCode = firstRegister.Data.OtpCode!
        });
        Assert.True(verify.Success);

        var firstSubmit = await _formPublicService.SubmitFormAsync(slug, new SubmitFormPublicDto
        {
            AccessToken = firstRegister.Data.AccessToken,
            Values = new Dictionary<string, string?>()
        });
        Assert.True(firstSubmit.Success);

        var secondRegister = await _formPublicService.RegisterAsync(slug, new RegisterPublicParticipantDto
        {
            FirstName = "علی",
            LastName = "رضایی",
            ParticipantMobile = "09121113344"
        });

        Assert.False(secondRegister.Success);
        Assert.Equal(400, secondRegister.StatusCode);
    }

    [Fact]
    public async Task GetPublicWheel_PendingApproval_Returns403()
    {
        var slug = $"pend-wh-{Guid.NewGuid():N}"[..20];
        var wheelId = await _wheelCtx.CreateWheelWithItemsAsync();
        await _wheelCtx.Service.PublishAsync(wheelId, _wheelCtx.OwnerUserId, new PublishLuckyWheelDto { Slug = slug });

        var result = await _wheelPublicService.GetPublicWheelAsync(slug);

        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(ErrorCodes.ContentPendingApproval, result.ErrorCode);
        Assert.Contains("منتشر نشده", result.Message);
    }

    [Fact]
    public async Task GetPublicWheel_ValidSlug_Returns200()
    {
        var slug = $"wheel-{Guid.NewGuid():N}"[..20];
        var wheelId = await _wheelCtx.CreateWheelWithItemsAsync();
        await _wheelCtx.Service.PublishAsync(wheelId, _wheelCtx.OwnerUserId, new PublishLuckyWheelDto
        {
            Slug = slug
        });
        await _wheelCtx.ApproveWheelAsync(wheelId);

        var result = await _wheelPublicService.GetPublicWheelAsync(slug);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.NotEmpty(result.Data!.Items);
    }

    [Fact]
    public async Task SpinPublicWheel_WithRegisterAndOtp_Returns201()
    {
        var slug = $"spin-{Guid.NewGuid():N}"[..20];
        var wheelId = await _wheelCtx.CreateWheelWithItemsAsync();
        await _wheelCtx.Service.PublishAsync(wheelId, _wheelCtx.OwnerUserId, new PublishLuckyWheelDto
        {
            Slug = slug
        });
        await _wheelCtx.ApproveWheelAsync(wheelId);

        var register = await _wheelPublicService.RegisterAsync(slug, new RegisterPublicParticipantDto
        {
            FirstName = "سارا",
            LastName = "محمدی",
            ParticipantMobile = "09123334455"
        });
        Assert.True(register.Success, $"Register failed: status={register.StatusCode}, code={register.ErrorCode}, message={register.Message}");
        Assert.False(string.IsNullOrWhiteSpace(register.Data!.OtpCode));

        var verify = await _wheelPublicService.VerifyOtpAsync(slug, new VerifyPublicParticipantOtpDto
        {
            AccessToken = register.Data.AccessToken,
            OtpCode = register.Data.OtpCode!
        });
        Assert.True(verify.Success);
        Assert.True(verify.Data!.IsPhoneVerified);

        var result = await _wheelPublicService.SpinAsync(slug, new SpinLuckyWheelPublicDto
        {
            AccessToken = register.Data.AccessToken
        });

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Data!.WonItemName));
        Assert.False(string.IsNullOrWhiteSpace(result.Data.PrizeCode));
        Assert.StartsWith("LW-", result.Data.PrizeCode);
    }

    [Fact]
    public async Task SpinPublicWheel_WithoutOtp_Returns400()
    {
        var slug = $"nootp-{Guid.NewGuid():N}"[..20];
        var wheelId = await _wheelCtx.CreateWheelWithItemsAsync();
        await _wheelCtx.Service.PublishAsync(wheelId, _wheelCtx.OwnerUserId, new PublishLuckyWheelDto
        {
            Slug = slug
        });
        await _wheelCtx.ApproveWheelAsync(wheelId);

        var register = await _wheelPublicService.RegisterAsync(slug, new RegisterPublicParticipantDto
        {
            FirstName = "سارا",
            LastName = "محمدی",
            ParticipantMobile = "09123339900"
        });
        Assert.True(register.Success, $"Register failed: status={register.StatusCode}, code={register.ErrorCode}, message={register.Message}");

        var result = await _wheelPublicService.SpinAsync(slug, new SpinLuckyWheelPublicDto
        {
            AccessToken = register.Data!.AccessToken
        });

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task SpinPublicWheel_DuplicateMobile_Returns400()
    {
        var slug = $"once-{Guid.NewGuid():N}"[..20];
        var wheelId = await _wheelCtx.CreateWheelWithItemsAsync();
        await _wheelCtx.Service.PublishAsync(wheelId, _wheelCtx.OwnerUserId, new PublishLuckyWheelDto
        {
            Slug = slug
        });
        await _wheelCtx.ApproveWheelAsync(wheelId);

        var registerDto = new RegisterPublicParticipantDto
        {
            FirstName = "رضا",
            LastName = "کریمی",
            ParticipantMobile = "09124445566"
        };

        var firstRegister = await _wheelPublicService.RegisterAsync(slug, registerDto);
        Assert.True(firstRegister.Success, $"First register failed: status={firstRegister.StatusCode}, code={firstRegister.ErrorCode}, message={firstRegister.Message}");

        var verify = await _wheelPublicService.VerifyOtpAsync(slug, new VerifyPublicParticipantOtpDto
        {
            AccessToken = firstRegister.Data!.AccessToken,
            OtpCode = firstRegister.Data.OtpCode!
        });
        Assert.True(verify.Success);

        var first = await _wheelPublicService.SpinAsync(slug, new SpinLuckyWheelPublicDto
        {
            AccessToken = firstRegister.Data.AccessToken
        });
        Assert.True(first.Success);

        var secondRegister = await _wheelPublicService.RegisterAsync(slug, registerDto);
        Assert.False(secondRegister.Success);
        Assert.Equal(400, secondRegister.StatusCode);
    }

    private sealed class FakeHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Microsoft.Extensions.Hosting.Environments.Production;
        public string ApplicationName { get; set; } = "Api_Vapp.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private sealed class PublicApiFakeUserSmsBillingService : IUserSmsBillingService
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

    private sealed class FakeSmsService : ISmsService
    {
        public Task<bool> SendOtpAsync(string phoneNumber, string otpCode, string templateType = "VerifyOtp") =>
            Task.FromResult(true);

        public Task<string> GenerateOtpAsync() => Task.FromResult("1234");

        public Task<ApiResponse<SendSmsResponseDto>> SendSmsAsync(SendSmsRequestDto request) =>
            Task.FromResult(ApiResponse<SendSmsResponseDto>.CreateSuccess(new SendSmsResponseDto
            {
                Sid = 1,
                Status = 1,
                Message = "sent"
            }));

        public Task<ApiResponse<SendBulkResponseDto>> SendBulkSmsAsync(SendBulkRequestDto request) =>
            Task.FromResult(ApiResponse<SendBulkResponseDto>.CreateSuccess(new SendBulkResponseDto()));

        public Task<ApiResponse<SendArrayResponseDto>> SendArraySmsAsync(SendArrayRequestDto request) =>
            Task.FromResult(ApiResponse<SendArrayResponseDto>.CreateSuccess(new SendArrayResponseDto()));

        public Task<ApiResponse<DeliveryResponseDto>> GetDeliveryStatusAsync(long sid) =>
            Task.FromResult(ApiResponse<DeliveryResponseDto>.CreateSuccess(new DeliveryResponseDto()));

        public Task<ApiResponse<InboxResponseDto>> GetInboxAsync(InboxRequestDto request) =>
            Task.FromResult(ApiResponse<InboxResponseDto>.CreateSuccess(new InboxResponseDto()));

        public Task<ApiResponse<InfoResponseDto>> GetWalletInfoAsync() =>
            Task.FromResult(ApiResponse<InfoResponseDto>.CreateSuccess(new InfoResponseDto()));
    }
}
