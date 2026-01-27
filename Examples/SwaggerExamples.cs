using Api_Vapp.DTOs.Auth;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json;

namespace Api_Vapp.Examples
{
    /// <summary>
    /// Operation Filter برای اضافه کردن Examples به Swagger
    /// این Filter به صورت خودکار Examples را برای endpoint های مشخص شده اضافه می‌کند
    /// </summary>
    public class SwaggerExamplesFilter : IOperationFilter
    {
        /// <summary>
        /// تبدیل JSON string به IOpenApiAny
        /// </summary>
        private IOpenApiAny ConvertJsonToOpenApiAny(string json)
        {
            using var document = JsonDocument.Parse(json);
            return ConvertJsonElementToOpenApiAny(document.RootElement);
        }

        /// <summary>
        /// تبدیل JsonElement به IOpenApiAny
        /// </summary>
        private IOpenApiAny ConvertJsonElementToOpenApiAny(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => ConvertJsonObjectToOpenApiObject(element),
                JsonValueKind.Array => ConvertJsonArrayToOpenApiArray(element),
                JsonValueKind.String => new OpenApiString(element.GetString() ?? string.Empty),
                JsonValueKind.Number => element.TryGetInt32(out var intValue) 
                    ? new OpenApiInteger(intValue) 
                    : new OpenApiDouble(element.GetDouble()),
                JsonValueKind.True => new OpenApiBoolean(true),
                JsonValueKind.False => new OpenApiBoolean(false),
                JsonValueKind.Null => new OpenApiNull(),
                _ => new OpenApiString(element.ToString())
            };
        }

        /// <summary>
        /// تبدیل JsonObject به OpenApiObject
        /// </summary>
        private OpenApiObject ConvertJsonObjectToOpenApiObject(JsonElement element)
        {
            var obj = new OpenApiObject();
            foreach (var property in element.EnumerateObject())
            {
                obj[property.Name] = ConvertJsonElementToOpenApiAny(property.Value);
            }
            return obj;
        }

        /// <summary>
        /// تبدیل JsonArray به OpenApiArray
        /// </summary>
        private OpenApiArray ConvertJsonArrayToOpenApiArray(JsonElement element)
        {
            var array = new OpenApiArray();
            foreach (var item in element.EnumerateArray())
            {
                array.Add(ConvertJsonElementToOpenApiAny(item));
            }
            return array;
        }

        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            // Example برای Register endpoint
            if (context.MethodInfo.Name == "Register" && context.ApiDescription.RelativePath?.Contains("register") == true)
            {
                var registerExample = new RegisterDto
                {
                    FullName = "علی احمدی",
                    PhoneNumber = "09123456789",
                    NationalId = "1234567890"
                };

                if (operation.RequestBody?.Content != null)
                {
                    var json = JsonSerializer.Serialize(registerExample);
                    var example = ConvertJsonToOpenApiAny(json);
                    foreach (var content in operation.RequestBody.Content.Values)
                    {
                        content.Example = example;
                    }
                }

                // Response Example
                var responseExample = new SendOtpResponseDto
                {
                    StatusCode = 200,
                    Success = true,
                    Message = "کد OTP با موفقیت ارسال شد",
                    ExpiresInSeconds = 300
                };

                if (operation.Responses.TryGetValue("200", out var response200))
                {
                    if (response200.Content != null)
                    {
                        var responseJson = JsonSerializer.Serialize(responseExample);
                        var example = ConvertJsonToOpenApiAny(responseJson);
                        foreach (var content in response200.Content.Values)
                        {
                            content.Example = example;
                        }
                    }
                }
            }

            // Example برای Login endpoint
            if (context.MethodInfo.Name == "Login" && context.ApiDescription.RelativePath?.Contains("login") == true)
            {
                var loginExample = new LoginDto
                {
                    PhoneNumber = "09123456789"
                };

                if (operation.RequestBody?.Content != null)
                {
                    var json = JsonSerializer.Serialize(loginExample);
                    var example = ConvertJsonToOpenApiAny(json);
                    foreach (var content in operation.RequestBody.Content.Values)
                    {
                        content.Example = example;
                    }
                }

                // Response Example
                var responseExample = new SendOtpResponseDto
                {
                    StatusCode = 200,
                    Success = true,
                    Message = "کد OTP با موفقیت ارسال شد",
                    ExpiresInSeconds = 300
                };

                if (operation.Responses.TryGetValue("200", out var response200))
                {
                    if (response200.Content != null)
                    {
                        var responseJson = JsonSerializer.Serialize(responseExample);
                        var example = ConvertJsonToOpenApiAny(responseJson);
                        foreach (var content in response200.Content.Values)
                        {
                            content.Example = example;
                        }
                    }
                }
            }

            // Example برای VerifyLogin endpoint
            if (context.MethodInfo.Name == "VerifyLogin" && context.ApiDescription.RelativePath?.Contains("verify-login") == true)
            {
                var verifyExample = new VerifyOtpDto
                {
                    PhoneNumber = "09123456789",
                    OtpCode = "123456"
                };

                if (operation.RequestBody?.Content != null)
                {
                    var json = JsonSerializer.Serialize(verifyExample);
                    var example = ConvertJsonToOpenApiAny(json);
                    foreach (var content in operation.RequestBody.Content.Values)
                    {
                        content.Example = example;
                    }
                }

                // Response Example
                var responseExample = new AuthResponseDto
                {
                    StatusCode = 200,
                    Success = true,
                    Message = "ورود با موفقیت انجام شد",
                    Tokens = new TokenResponseDto
                    {
                        AccessToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
                        RefreshToken = "refresh_token_example_here",
                        ExpiresAt = DateTime.UtcNow.AddHours(1),
                        RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(1)
                    },
                    User = new UserInfoDto
                    {
                        Id = 1,
                        FullName = "علی احمدی",
                        PhoneNumber = "09123456789",
                        IsPhoneVerified = true
                    }
                };

                if (operation.Responses.TryGetValue("200", out var response200))
                {
                    if (response200.Content != null)
                    {
                        var responseJson = JsonSerializer.Serialize(responseExample);
                        var example = ConvertJsonToOpenApiAny(responseJson);
                        foreach (var content in response200.Content.Values)
                        {
                            content.Example = example;
                        }
                    }
                }
            }
        }
    }
}

