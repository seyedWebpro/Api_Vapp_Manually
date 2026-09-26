using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.Role;
using Api_Vapp.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_Vapp.Controller
{
    /// <summary>
    /// مدیریت نقش‌ها — فقط ادمین
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "AdminOnly")]
    [Produces("application/json")]
    public class RoleController : VappControllerBase
    {
        private readonly IRoleService _roleService;

        public RoleController(
            IRoleService roleService,
            IConfiguration configuration,
            IUserRepository userRepository)
            : base(configuration, userRepository)
        {
            _roleService = roleService;
        }

        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<RoleResponseDto>>> CreateRole([FromBody] CreateRoleDto createRoleDto)
        {
            var invalid = InvalidModelStateResponse<RoleResponseDto>();
            if (invalid != null) return invalid;

            var result = await _roleService.CreateRoleAsync(createRoleDto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<RoleResponseDto>>> GetRoleById(int id)
        {
            var result = await _roleService.GetRoleByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<RoleListResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<RoleListResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<RoleListResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<RoleListResponseDto>>> GetRoles(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] bool? isActive = null,
            [FromQuery] bool? isDeleted = null)
        {
            var result = await _roleService.GetRolesAsync(pageNumber, pageSize, isActive, isDeleted);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("active")]
        [ProducesResponseType(typeof(ApiResponse<List<RoleResponseDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<List<RoleResponseDto>>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<RoleResponseDto>>>> GetActiveRoles()
        {
            var result = await _roleService.GetActiveRolesAsync();
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id}/update")]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<RoleResponseDto>>> UpdateRole(int id, [FromBody] UpdateRoleDto updateRoleDto)
        {
            var invalid = InvalidModelStateResponse<RoleResponseDto>();
            if (invalid != null) return invalid;

            var result = await _roleService.UpdateRoleAsync(id, updateRoleDto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id}/delete")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteRole(int id)
        {
            var result = await _roleService.DeleteRoleAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id}/hard-delete")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> HardDeleteRole(int id)
        {
            var result = await _roleService.HardDeleteRoleAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id}/toggle-active")]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<RoleResponseDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<RoleResponseDto>>> ToggleRoleActiveStatus(
            int id,
            [FromBody] bool isActive)
        {
            var result = await _roleService.ToggleRoleActiveStatusAsync(id, isActive);
            return StatusCode(result.StatusCode, result);
        }
    }
}
