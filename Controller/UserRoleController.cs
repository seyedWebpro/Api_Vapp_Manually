using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.UserRole;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// مدیریت روابط کاربر-نقش — فقط ادمین
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "AdminOnly")]
    [Produces("application/json")]
    public class UserRoleController : VappControllerBase
    {
        private readonly IUserRoleService _userRoleService;

        public UserRoleController(
            IUserRoleService userRoleService,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _userRoleService = userRoleService;
        }

        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<UserRoleResponseDto>>> CreateUserRole([FromBody] CreateUserRoleDto createUserRoleDto)
        {
            var invalid = InvalidModelStateResponse<UserRoleResponseDto>();
            if (invalid != null) return invalid;

            var result = await _userRoleService.CreateUserRoleAsync(createUserRoleDto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<UserRoleResponseDto>>> GetUserRoleById(int id)
        {
            var result = await _userRoleService.GetUserRoleByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("user/{userId}")]
        [ProducesResponseType(typeof(ApiResponse<List<UserRoleResponseDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<List<UserRoleResponseDto>>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<List<UserRoleResponseDto>>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<UserRoleResponseDto>>>> GetUserRoles(int userId)
        {
            var result = await _userRoleService.GetUserRolesAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("role/{roleId}")]
        [ProducesResponseType(typeof(ApiResponse<List<UserRoleResponseDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<List<UserRoleResponseDto>>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<List<UserRoleResponseDto>>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<UserRoleResponseDto>>>> GetRoleUsers(int roleId)
        {
            var result = await _userRoleService.GetRoleUsersAsync(roleId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<UserRoleListResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserRoleListResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<UserRoleListResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<UserRoleListResponseDto>>> GetUserRoles(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] int? userId = null,
            [FromQuery] int? roleId = null,
            [FromQuery] bool? isActive = null)
        {
            var result = await _userRoleService.GetUserRolesListAsync(pageNumber, pageSize, userId, roleId, isActive);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id}/update")]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<UserRoleResponseDto>>> UpdateUserRole(int id, [FromBody] UpdateUserRoleDto updateUserRoleDto)
        {
            var invalid = InvalidModelStateResponse<UserRoleResponseDto>();
            if (invalid != null) return invalid;

            var result = await _userRoleService.UpdateUserRoleAsync(id, updateUserRoleDto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id}/delete")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteUserRole(int id)
        {
            var result = await _userRoleService.DeleteUserRoleAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id}/hard-delete")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> HardDeleteUserRole(int id)
        {
            var result = await _userRoleService.HardDeleteUserRoleAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id}/toggle-active")]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<UserRoleResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<UserRoleResponseDto>>> ToggleUserRoleActiveStatus(
            int id,
            [FromBody] bool isActive)
        {
            var result = await _userRoleService.ToggleUserRoleActiveStatusAsync(id, isActive);
            return StatusCode(result.StatusCode, result);
        }
    }
}
