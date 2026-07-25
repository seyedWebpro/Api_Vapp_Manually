using Api_Vapp.DTOs.BusinessCard;
using Api_Vapp.DTOs.Common;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// API عمومی کارت ویزیت — بدون احراز هویت
    /// </summary>
    [ApiController]
    [Route("api/BusinessCardPublic")]
    [AllowAnonymous]
    [Produces("application/json")]
    public class BusinessCardPublicController : VappControllerBase
    {
        private readonly IBusinessCardPublicService _businessCardPublicService;

        public BusinessCardPublicController(
            IBusinessCardPublicService businessCardPublicService,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _businessCardPublicService = businessCardPublicService;
        }

        /// <summary>
        /// دریافت schema کارت ویزیت منتشرشده
        /// </summary>
        [HttpGet("{slug}")]
        [ProducesResponseType(typeof(ApiResponse<BusinessCardPublicDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<BusinessCardPublicDto>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<BusinessCardPublicDto>>> GetCard(string slug)
        {
            var result = await _businessCardPublicService.GetPublicCardAsync(slug);
            return StatusCode(result.StatusCode, result);
        }
    }
}
