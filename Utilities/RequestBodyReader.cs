using System.Text.Json;
using Api_Vapp.DTOs.Common;
using Microsoft.AspNetCore.Http;

namespace Api_Vapp.Utilities
{
    /// <summary>
    /// خواندن امن body/form بدون try/catch داخل اکشن کنترلر — خطا به‌صورت ApiResponse کنترل‌شده برمی‌گردد.
    /// </summary>
    public static class RequestBodyReader
    {
        public static async Task<(IFormCollection? Form, ApiResponse<T>? Error)> TryReadFormAsync<T>(
            HttpRequest request,
            string failureMessage)
        {
            try
            {
                var form = await request.ReadFormAsync();
                return (form, null);
            }
            catch (Exception)
            {
                return (null, ApiResponse<T>.BadRequest(failureMessage, errorCode: ErrorCodes.ValidationFailed));
            }
        }

        public static async Task<(TDto? Dto, ApiResponse<TResponse>? Error)> TryReadJsonAsync<TDto, TResponse>(
            HttpRequest request,
            JsonSerializerOptions options,
            string emptyMessage,
            string invalidFormatMessage,
            string failureMessage)
        {
            try
            {
                using var reader = new StreamReader(request.Body);
                var raw = await reader.ReadToEndAsync();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return (default, ApiResponse<TResponse>.BadRequest(emptyMessage, errorCode: ErrorCodes.ValidationFailed));
                }

                var parsed = JsonSerializer.Deserialize<TDto>(raw, options);
                if (parsed == null)
                {
                    return (default, ApiResponse<TResponse>.BadRequest(invalidFormatMessage, errorCode: ErrorCodes.ValidationFailed));
                }

                return (parsed, null);
            }
            catch (Exception)
            {
                return (default, ApiResponse<TResponse>.BadRequest(failureMessage, errorCode: ErrorCodes.ValidationFailed));
            }
        }
    }
}
