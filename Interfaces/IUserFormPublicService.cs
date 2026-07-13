using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.UserForm;

namespace Api_Vapp.Interfaces
{
    public interface IUserFormPublicService
    {
        Task<ApiResponse<FormPublicDto>> GetPublicFormAsync(string slug);

        Task<ApiResponse<SubmitFormPublicResponseDto>> SubmitFormAsync(string slug, SubmitFormPublicDto dto);
    }
}
