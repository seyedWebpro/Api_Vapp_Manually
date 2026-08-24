using Api_Vapp.Attributes;
using Api_Vapp.Constants;
using Api_Vapp.DTOs.BankAccount;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Message;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// کنترلر مدیریت شماره حساب برای ارسال سریع
    /// </summary>
    /// <remarks>
    /// CRUD شماره حساب + تنظیم پیش‌فرض + ارسال سریع SMS به مخاطب.
    /// مسیرها: GET/POST /api/BankAccount ، GET /{id} ، POST /{id}/update|delete|set-default ، POST /quick-send
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [RequireSubscriptionFeature(SubscriptionFeatureCodes.FreeQuickSend)]
    [Produces("application/json")]
    public class BankAccountController : VappControllerBase
    {
        private readonly IBankAccountService _bankAccountService;

        public BankAccountController(
            IBankAccountService bankAccountService,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _bankAccountService = bankAccountService;
        }

        /// <summary>
        /// دریافت لیست شماره حساب‌ها با pagination
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<BankAccountListResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<BankAccountListResponseDto>), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<ApiResponse<BankAccountListResponseDto>>> GetBankAccounts(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _bankAccountService.GetBankAccountsAsync(userId, pageNumber, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// دریافت جزئیات یک شماره حساب
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ApiResponse<BankAccountResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<BankAccountResponseDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<BankAccountResponseDto>>> GetBankAccountById(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _bankAccountService.GetBankAccountByIdAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ایجاد شماره حساب جدید
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<BankAccountResponseDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<BankAccountResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<BankAccountResponseDto>>> CreateBankAccount(
            [FromBody] CreateBankAccountDto? createDto)
        {
            if (createDto == null)
            {
                return StatusCode(400, ApiResponse<BankAccountResponseDto>.BadRequest(
                    "داده‌های ورودی نامعتبر است",
                    errorCode: ErrorCodes.ValidationFailed));
            }

            var invalid = InvalidModelStateResponse<BankAccountResponseDto>();
            if (invalid != null)
                return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _bankAccountService.CreateBankAccountAsync(userId, createDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// به‌روزرسانی شماره حساب
        /// </summary>
        [HttpPost("{id:int}/update")]
        [ProducesResponseType(typeof(ApiResponse<BankAccountResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<BankAccountResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<BankAccountResponseDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<BankAccountResponseDto>>> UpdateBankAccount(
            int id,
            [FromBody] UpdateBankAccountDto? updateDto)
        {
            if (updateDto == null)
            {
                return StatusCode(400, ApiResponse<BankAccountResponseDto>.BadRequest(
                    "داده‌های ورودی نامعتبر است",
                    errorCode: ErrorCodes.ValidationFailed));
            }

            var invalid = InvalidModelStateResponse<BankAccountResponseDto>();
            if (invalid != null)
                return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _bankAccountService.UpdateBankAccountAsync(id, userId, updateDto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// حذف نرم شماره حساب
        /// </summary>
        [HttpPost("{id:int}/delete")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteBankAccount(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _bankAccountService.DeleteBankAccountAsync(id, userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// تنظیم شماره حساب به‌عنوان پیش‌فرض
        /// </summary>
        [HttpPost("{id:int}/set-default")]
        [ProducesResponseType(typeof(ApiResponse<BankAccountResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<BankAccountResponseDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<BankAccountResponseDto>>> SetDefaultBankAccount(int id)
        {
            var userId = await GetCurrentUserIdAsync();
            var result = await _bankAccountService.SetUserDefaultBankAccountAsync(userId, id);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// ارسال سریع شماره حساب به یک مخاطب (SMS)
        /// </summary>
        [HttpPost("quick-send")]
        [ProducesResponseType(typeof(ApiResponse<DirectSendResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<DirectSendResultDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<DirectSendResultDto>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<DirectSendResultDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<DirectSendResultDto>>> QuickSendBankAccount(
            [FromBody] QuickSendBankAccountDto? quickSendDto)
        {
            if (quickSendDto == null)
            {
                return StatusCode(400, ApiResponse<DirectSendResultDto>.BadRequest(
                    "داده‌های ورودی نامعتبر است",
                    errorCode: ErrorCodes.ValidationFailed));
            }

            var invalid = InvalidModelStateResponse<DirectSendResultDto>();
            if (invalid != null)
                return invalid;

            var userId = await GetCurrentUserIdAsync();
            var result = await _bankAccountService.QuickSendBankAccountAsync(userId, quickSendDto);
            return StatusCode(result.StatusCode, result);
        }
    }
}
