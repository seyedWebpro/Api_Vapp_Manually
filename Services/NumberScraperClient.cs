using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api_Vapp.Configuration;
using Api_Vapp.DTOs.NumberSeeker;
using Api_Vapp.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api_Vapp.Services
{
    /// <summary>
    /// ارتباط HTTP با FastAPI Number Scraper — Retry/CircuitBreaker در HttpClientExtensions.
    /// </summary>
    public class NumberScraperClient : INumberScraperClient
    {
        private readonly HttpClient _httpClient;
        private readonly NumberScraperApiSettings _settings;
        private readonly ILogger<NumberScraperClient> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public NumberScraperClient(
            HttpClient httpClient,
            IOptions<NumberScraperApiSettings> settings,
            ILogger<NumberScraperClient> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;

            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                _logger.LogWarning("NumberScraperApi:ApiKey is empty — scraper auth will fail");
            }
        }

        public bool IsEnabled => _settings.Enabled;

        public async Task<NumberSeekerTaskCreatedDto> StartScrapeAsync(
            StartNumberSeekerScrapeDto request,
            CancellationToken cancellationToken = default)
        {
            EnsureEnabled();

            var payload = new
            {
                source = request.Source.Trim().ToLowerInvariant(),
                city = request.City.Trim(),
                category = request.Category.Trim(),
                max_phones = request.MaxPhones,
                headless = request.Headless
            };

            using var response = await _httpClient.PostAsJsonAsync(
                "api/scrape",
                payload,
                JsonOptions,
                cancellationToken);

            await EnsureSuccessOrThrowAsync(response, cancellationToken);

            var result = await response.Content.ReadFromJsonAsync<NumberSeekerTaskCreatedDto>(JsonOptions, cancellationToken);
            if (result == null || string.IsNullOrWhiteSpace(result.TaskId))
            {
                throw new InvalidOperationException("پاسخ نامعتبر از سرویس اسکرپ دریافت شد.");
            }

            return result;
        }

        public async Task<NumberSeekerTaskStatusDto> GetTaskStatusAsync(
            string taskId,
            CancellationToken cancellationToken = default)
        {
            EnsureEnabled();

            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/task/{Uri.EscapeDataString(taskId)}");
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new KeyNotFoundException($"تسک {taskId} در سرویس اسکرپ یافت نشد.");
            }

            await EnsureSuccessOrThrowAsync(response, cancellationToken);

            var result = await response.Content.ReadFromJsonAsync<NumberSeekerTaskStatusDto>(JsonOptions, cancellationToken);
            if (result == null)
            {
                throw new InvalidOperationException("پاسخ نامعتبر از سرویس اسکرپ دریافت شد.");
            }

            return result;
        }

        public async Task<NumberSeekerCancelResultDto> CancelTaskAsync(
            string taskId,
            CancellationToken cancellationToken = default)
        {
            EnsureEnabled();

            using var response = await _httpClient.DeleteAsync(
                $"api/task/{Uri.EscapeDataString(taskId)}",
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new KeyNotFoundException($"تسک {taskId} در سرویس اسکرپ یافت نشد.");
            }

            await EnsureSuccessOrThrowAsync(response, cancellationToken);

            var body = await response.Content.ReadFromJsonAsync<ScraperMessageResponse>(JsonOptions, cancellationToken);
            return new NumberSeekerCancelResultDto
            {
                TaskId = taskId,
                Message = body?.Message ?? "تسک لغو شد."
            };
        }

        public async Task<NumberSeekerHealthDto> GetHealthAsync(
            CancellationToken cancellationToken = default)
        {
            if (!_settings.Enabled)
            {
                return new NumberSeekerHealthDto
                {
                    Status = "disabled",
                    ScraperReachable = false,
                    Timestamp = DateTime.UtcNow.ToString("O")
                };
            }

            try
            {
                using var response = await _httpClient.GetAsync("health", cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return new NumberSeekerHealthDto
                    {
                        Status = "unreachable",
                        ScraperReachable = false,
                        Timestamp = DateTime.UtcNow.ToString("O")
                    };
                }

                var health = await response.Content.ReadFromJsonAsync<NumberSeekerHealthDto>(JsonOptions, cancellationToken);
                if (health == null)
                {
                    return new NumberSeekerHealthDto
                    {
                        Status = "unknown",
                        ScraperReachable = false,
                        Timestamp = DateTime.UtcNow.ToString("O")
                    };
                }

                health.ScraperReachable = true;
                return health;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Number scraper health check failed");
                return new NumberSeekerHealthDto
                {
                    Status = "unreachable",
                    ScraperReachable = false,
                    Timestamp = DateTime.UtcNow.ToString("O")
                };
            }
        }

        public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        {
            var health = await GetHealthAsync(cancellationToken);
            return health.ScraperReachable &&
                   !string.Equals(health.Status, "disabled", StringComparison.OrdinalIgnoreCase);
        }

        private void EnsureEnabled()
        {
            if (!_settings.Enabled)
            {
                throw new InvalidOperationException("سرویس شماره‌جو غیرفعال است.");
            }
        }

        private static async Task EnsureSuccessOrThrowAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode)
                return;

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var message = TryExtractDetail(body) ?? $"خطای سرویس اسکرپ: {(int)response.StatusCode}";

            throw response.StatusCode switch
            {
                HttpStatusCode.TooManyRequests => new InvalidOperationException("محدودیت نرخ درخواست سرویس اسکرپ — لطفاً کمی بعد تلاش کنید."),
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new UnauthorizedAccessException("احراز هویت سرویس اسکرپ ناموفق بود."),
                HttpStatusCode.BadRequest => new ArgumentException(message),
                _ => new HttpRequestException(message, null, response.StatusCode)
            };
        }

        private static string? TryExtractDetail(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("detail", out var detail))
                {
                    return detail.ValueKind == JsonValueKind.String
                        ? detail.GetString()
                        : detail.ToString();
                }

                if (doc.RootElement.TryGetProperty("message", out var message))
                {
                    return message.GetString();
                }
            }
            catch (JsonException)
            {
                return body.Length > 300 ? body[..300] : body;
            }

            return null;
        }

        private sealed class ScraperMessageResponse
        {
            public string? Message { get; set; }
        }
    }
}
