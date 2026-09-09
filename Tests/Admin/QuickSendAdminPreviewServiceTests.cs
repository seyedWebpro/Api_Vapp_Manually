using Api_Vapp.Constants;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.BusinessCard;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Public;
using Api_Vapp.DTOs.UserForm;
using Api_Vapp.Interfaces;
using Api_Vapp.Services.Admin;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Api_Vapp.Tests.Admin;

public class QuickSendAdminPreviewServiceTests
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 4096 });
    private readonly QuickSendPreviewRateLimiter _rateLimiter;
    private readonly FakeApprovalService _approvalService = new();
    private readonly FakeFormPublicService _formPublicService = new();
    private readonly FakeBusinessCardPublicService _cardPublicService = new();
    private readonly FakeHttpContextAccessor _httpContextAccessor = new();

    public QuickSendAdminPreviewServiceTests()
    {
        _rateLimiter = new QuickSendPreviewRateLimiter(_cache);
    }

    private QuickSendAdminPreviewService CreateService() =>
        new(
            _cache,
            _approvalService,
            _formPublicService,
            _cardPublicService,
            null!,
            null!,
            _rateLimiter,
            _httpContextAccessor,
            NullLogger<QuickSendAdminPreviewService>.Instance);

    [Fact]
    public async Task CreatePreviewTokenAsync_UserForm_ReturnsTokenAndPath()
    {
        _approvalService.Item = BuildApprovalItem(QuickSendItemTypes.UserForm, 42, "Pending");
        _formPublicService.Form = new FormPublicDto { Title = "فرم تست", Slug = "form-test" };

        var service = CreateService();
        var result = await service.CreatePreviewTokenAsync(QuickSendItemTypes.UserForm, 42, adminUserId: 1);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(32, result.Data!.Token.Length);
        Assert.StartsWith("/preview/", result.Data.PreviewPath);
    }

    [Fact]
    public async Task CreatePreviewTokenAsync_LuckyWheel_ReturnsToken()
    {
        _approvalService.Item = BuildApprovalItem(QuickSendItemTypes.LuckyWheel, 1, "Pending");
        var service = CreateService();
        var result = await service.CreatePreviewTokenAsync(QuickSendItemTypes.LuckyWheel, 1, adminUserId: 1);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.StartsWith("/preview/", result.Data!.PreviewPath);
    }

    [Fact]
    public async Task GetPreviewByTokenAsync_InvalidFormat_ReturnsTokenInvalid()
    {
        var service = CreateService();
        var result = await service.GetPreviewByTokenAsync("short");

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(ErrorCodes.TokenInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task GetPreviewByTokenAsync_UnknownToken_Returns404()
    {
        var unknown = new string('a', 32);
        var service = CreateService();
        var result = await service.GetPreviewByTokenAsync(unknown);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal(ErrorCodes.TokenInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task GetPreviewByTokenAsync_ValidToken_ReturnsFormContent()
    {
        _approvalService.Item = BuildApprovalItem(QuickSendItemTypes.UserForm, 7, "Pending");
        _formPublicService.Form = new FormPublicDto
        {
            Title = "فرم E2E",
            Slug = "form-e2e",
            Fields =
            [
                new FormPublicFieldDto
                {
                    FieldKey = "name",
                    FieldType = "text",
                    Label = "نام",
                    IsRequired = true,
                    DisplayOrder = 0
                }
            ]
        };

        var service = CreateService();
        var tokenResult = await service.CreatePreviewTokenAsync(QuickSendItemTypes.UserForm, 7, adminUserId: 9);
        var token = tokenResult.Data!.Token;

        var preview = await service.GetPreviewByTokenAsync(token);

        Assert.True(preview.Success);
        Assert.Equal(200, preview.StatusCode);
        Assert.True(preview.Data!.IsAdminPreview);
        Assert.Equal("UserForm", preview.Data.ItemType);
        Assert.Equal("فرم E2E", preview.Data.Form!.Title);
        Assert.Single(preview.Data.Form.Fields);
    }

    [Fact]
    public async Task GetPreviewByTokenAsync_ExcessiveReads_ReturnsRateLimited()
    {
        _approvalService.Item = BuildApprovalItem(QuickSendItemTypes.UserForm, 8, "Pending");
        _formPublicService.Form = new FormPublicDto { Title = "فرم", Slug = "x" };

        var service = CreateService();
        var token = (await service.CreatePreviewTokenAsync(QuickSendItemTypes.UserForm, 8, adminUserId: 1)).Data!.Token;

        ApiResponse<QuickSendPreviewContentDto>? last = null;
        for (var i = 0; i < 125; i++)
        {
            last = await service.GetPreviewByTokenAsync(token);
            if (last.StatusCode == 429)
            {
                break;
            }
        }

        Assert.NotNull(last);
        Assert.Equal(429, last!.StatusCode);
        Assert.Equal(ErrorCodes.RateLimited, last.ErrorCode);
    }

    private static QuickSendApprovalResponseDto BuildApprovalItem(string itemType, int id, string status) =>
        new()
        {
            ItemType = itemType,
            ItemTypeTitle = itemType == QuickSendItemTypes.UserForm ? "فرم" : "کارت ویزیت",
            Id = id,
            Title = "عنوان تست",
            ApprovalStatus = status,
            IsActive = true,
            UserId = 100,
            CreatedAt = DateTime.UtcNow
        };

    private sealed class FakeApprovalService : IAdminQuickSendApprovalService
    {
        public QuickSendApprovalResponseDto? Item { get; set; }

        public Task<ApiResponse<PagedResponse<QuickSendApprovalResponseDto>>> GetPendingAsync(
            string? itemType,
            int page,
            int pageSize) =>
            throw new NotImplementedException();

        public Task<ApiResponse<PagedResponse<QuickSendApprovalResponseDto>>> GetAllAsync(
            string? status,
            string? itemType,
            int page,
            int pageSize) =>
            throw new NotImplementedException();

        public Task<ApiResponse<QuickSendApprovalResponseDto>> GetByIdAsync(string itemType, int id)
        {
            if (Item == null || Item.Id != id)
            {
                return Task.FromResult(ApiResponse<QuickSendApprovalResponseDto>.NotFound());
            }

            return Task.FromResult(ApiResponse<QuickSendApprovalResponseDto>.CreateSuccess(Item));
        }

        public Task<ApiResponse<bool>> ApproveAsync(string itemType, int id, int adminUserId) =>
            throw new NotImplementedException();

        public Task<ApiResponse<bool>> RejectAsync(
            string itemType,
            int id,
            int adminUserId,
            RejectApprovalDto dto) =>
            throw new NotImplementedException();

        public Task<int> CountPendingAsync() => Task.FromResult(0);
    }

    private sealed class FakeFormPublicService : IUserFormPublicService
    {
        public FormPublicDto? Form { get; set; }

        public Task<ApiResponse<FormPublicDto>> GetPublicFormAsync(string slug) =>
            throw new NotImplementedException();

        public Task<ApiResponse<FormPublicDto>> GetAdminPreviewByIdAsync(int id) =>
            Task.FromResult(
                Form == null
                    ? ApiResponse<FormPublicDto>.NotFound()
                    : ApiResponse<FormPublicDto>.CreateSuccess(Form));

        public Task<ApiResponse<RegisterPublicParticipantResponseDto>> RegisterAsync(
            string slug,
            RegisterPublicParticipantDto dto) =>
            throw new NotImplementedException();

        public Task<ApiResponse<PublicParticipantOtpResponseDto>> VerifyOtpAsync(
            string slug,
            VerifyPublicParticipantOtpDto dto) =>
            throw new NotImplementedException();

        public Task<ApiResponse<PublicParticipantOtpResponseDto>> ResendOtpAsync(
            string slug,
            ResendPublicParticipantOtpDto dto) =>
            throw new NotImplementedException();

        public Task<ApiResponse<SubmitFormPublicResponseDto>> SubmitFormAsync(
            string slug,
            SubmitFormPublicDto dto) =>
            throw new NotImplementedException();
    }

    private sealed class FakeBusinessCardPublicService : IBusinessCardPublicService
    {
        public Task<ApiResponse<BusinessCardPublicDto>> GetPublicCardAsync(string slug) =>
            throw new NotImplementedException();

        public Task<ApiResponse<BusinessCardPublicDto>> GetAdminPreviewByIdAsync(int id) =>
            Task.FromResult(ApiResponse<BusinessCardPublicDto>.NotFound());
    }

    private sealed class FakeHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = new DefaultHttpContext();
    }
}
