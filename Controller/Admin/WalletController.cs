using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Wallet;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller.Admin
{
    /// <summary>
    /// مدیریت کیف پول کاربران از پنل ادمین (موجودی، تاریخچه، شارژ دستی)
    /// </summary>
    [ApiController]
    [Route("api/Admin/[controller]")]
    [Authorize(Policy = "AdminOnly")]
    [Produces("application/json")]
    public class WalletController : VappControllerBase
    {
        private readonly IAdminWalletService _service;

        public WalletController(
            IAdminWalletService service,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _service = service;
        }

        /// <summary>
        /// دریافت موجودی کیف پول یک کاربر
        /// </summary>
        [HttpGet("{userId:int}/balance")]
        public async Task<ActionResult<ApiResponse<AdminWalletBalanceDto>>> GetBalance(int userId)
        {
            var result = await _service.GetBalanceAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// تاریخچه تراکنش‌های کیف پول کاربر
        /// </summary>
        [HttpGet("{userId:int}/transactions")]
        public async Task<ActionResult<ApiResponse<WalletTransactionListDto>>> GetTransactions(
            int userId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _service.GetTransactionsAsync(userId, pageNumber, pageSize);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// شارژ دستی کیف پول کاربر توسط ادمین
        /// </summary>
        [HttpPost("{userId:int}/manual-charge")]
        public async Task<ActionResult<ApiResponse<AdminManualChargeResponseDto>>> ManualCharge(
            int userId,
            [FromBody] AdminManualChargeRequestDto dto)
        {
            var invalid = InvalidModelStateResponse<AdminManualChargeResponseDto>();
            if (invalid != null) return invalid;

            var adminUserId = await GetCurrentUserIdAsync();
            var result = await _service.ManualChargeAsync(adminUserId, userId, dto);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// کسر دستی کیف پول کاربر توسط ادمین
        /// </summary>
        [HttpPost("{userId:int}/manual-deduct")]
        public async Task<ActionResult<ApiResponse<AdminManualChargeResponseDto>>> ManualDeduct(
            int userId,
            [FromBody] AdminManualChargeRequestDto dto)
        {
            var invalid = InvalidModelStateResponse<AdminManualChargeResponseDto>();
            if (invalid != null) return invalid;

            var adminUserId = await GetCurrentUserIdAsync();
            var result = await _service.ManualDeductAsync(adminUserId, userId, dto);
            return StatusCode(result.StatusCode, result);
        }
    }
}
