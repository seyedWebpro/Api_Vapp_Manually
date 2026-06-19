using Api_Vapp.DTOs.Common;
using Api_Vapp.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Api_Vapp.Filters
{
    /// <summary>
    /// افزودن TraceId به تمام پاسخ‌های ApiResponse
    /// </summary>
    public class ApiTraceIdResultFilter : IAsyncResultFilter
    {
        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            if (context.Result is ObjectResult { Value: not null } objectResult)
            {
                var traceId = ControlledErrorHelper.GetTraceId(context.HttpContext);
                SetTraceIdIfEmpty(objectResult.Value, traceId);
            }

            await next();
        }

        private static void SetTraceIdIfEmpty(object value, string traceId)
        {
            var type = value.GetType();
            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(ApiResponse<>))
                return;

            var traceIdProperty = type.GetProperty(nameof(ApiResponse<object>.TraceId));
            if (traceIdProperty == null)
                return;

            if (traceIdProperty.GetValue(value) is null or "")
                traceIdProperty.SetValue(value, traceId);
        }
    }
}
