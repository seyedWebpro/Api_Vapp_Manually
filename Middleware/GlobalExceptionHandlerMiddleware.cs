using Api_Vapp.DTOs.Common;
using Api_Vapp.Exceptions;
using Api_Vapp.Utilities;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Api_Vapp.Middleware
{
    /// <summary>
    /// Middleware برای مدیریت خطاهای سراسری و برگرداندن Response استاندارد
    /// </summary>
    public class GlobalExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

        public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                var traceId = ControlledErrorHelper.GetTraceId(context);
                _logger.LogError(ex, "Unhandled exception. Path: {Path}, TraceId: {TraceId}", context.Request.Path, traceId);
                await HandleExceptionAsync(context, ex, traceId);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception, string traceId)
        {
            var response = context.Response;
            response.ContentType = "application/json";

            int statusCode;
            string errorMessage;
            string errorCode;
            List<string>? errors;

            if (exception is AppException appEx)
            {
                statusCode = appEx.StatusCode;
                errorMessage = appEx.Message;
                errorCode = appEx.ErrorCode;
                errors = appEx.Errors ?? (statusCode == 500 ? null : new List<string> { errorMessage });
            }
            else
            {
                var httpStatusCode = exception switch
                {
                    ArgumentNullException => HttpStatusCode.BadRequest,
                    ArgumentException => HttpStatusCode.BadRequest,
                    UnauthorizedAccessException => HttpStatusCode.Unauthorized,
                    KeyNotFoundException => HttpStatusCode.NotFound,
                    DbUpdateException => HttpStatusCode.BadRequest,
                    _ => HttpStatusCode.InternalServerError
                };

                statusCode = (int)httpStatusCode;
                errorCode = httpStatusCode switch
                {
                    HttpStatusCode.InternalServerError => ErrorCodes.Unexpected,
                    HttpStatusCode.Unauthorized => ErrorCodes.Unauthorized,
                    HttpStatusCode.NotFound => ErrorCodes.NotFound,
                    HttpStatusCode.BadRequest when exception is DbUpdateException => ErrorCodes.DatabaseError,
                    HttpStatusCode.BadRequest => ErrorCodes.InvalidInput,
                    _ => ErrorCodes.Unexpected
                };

                errorMessage = httpStatusCode switch
                {
                    HttpStatusCode.InternalServerError => ControlledErrorHelper.Unexpected,
                    HttpStatusCode.Unauthorized => ControlledErrorHelper.SanitizeArgumentMessage(
                        exception.Message, ControlledErrorHelper.Unauthorized),
                    HttpStatusCode.NotFound => ControlledErrorHelper.NotFound,
                    HttpStatusCode.BadRequest when exception is DbUpdateException => ControlledErrorHelper.Database,
                    HttpStatusCode.BadRequest => ControlledErrorHelper.SanitizeArgumentMessage(
                        exception.Message, ControlledErrorHelper.InvalidInput),
                    _ => ControlledErrorHelper.Unexpected
                };

                errors = httpStatusCode == HttpStatusCode.InternalServerError ? null : new List<string> { errorMessage };
            }

            response.StatusCode = statusCode;

            var errorResponse = new ApiResponse<object>
            {
                StatusCode = statusCode,
                Success = false,
                Message = errorMessage,
                ErrorCode = errorCode,
                Errors = errors,
                TraceId = traceId
            };

            var json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            return response.WriteAsync(json);
        }
    }
}
