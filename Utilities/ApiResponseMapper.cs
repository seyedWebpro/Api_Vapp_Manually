using Api_Vapp.DTOs.Common;

namespace Api_Vapp.Utilities
{
    public static class ApiResponseMapper
    {
        public static ApiResponse<TTarget> MapError<TSource, TTarget>(ApiResponse<TSource> source) =>
            new()
            {
                StatusCode = source.StatusCode,
                Success = false,
                Message = source.Message,
                ErrorCode = source.ErrorCode,
                Errors = source.Errors
            };
    }
}
