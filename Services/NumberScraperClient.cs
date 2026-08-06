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
                throw new InvalidOperationException("SCRAPER_INVALID_RESPONSE");
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
                throw new KeyNotFoundException("SCRAPER_NOT_FOUND");
            }

            await EnsureSuccessOrThrowAsync(response, cancellationToken);

            var result = await response.Content.ReadFromJsonAsync<NumberSeekerTaskStatusDto>(JsonOptions, cancellationToken);
            if (result == null)
            {
                throw new InvalidOperationException("SCRAPER_INVALID_RESPONSE");
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
                throw new KeyNotFoundException("SCRAPER_NOT_FOUND");
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
                    ApiKeyValid = false,
                    IntegrationReady = false,
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
                        ApiKeyValid = false,
                        IntegrationReady = false,
                        Timestamp = DateTime.UtcNow.ToString("O")
                    };
                }

                var health = await response.Content.ReadFromJsonAsync<NumberSeekerHealthDto>(
                    JsonOptions,
                    cancellationToken);
                if (health == null)
                {
                    return new NumberSeekerHealthDto
                    {
                        Status = "unknown",
                        ScraperReachable = false,
                        ApiKeyValid = false,
                        IntegrationReady = false,
                        Timestamp = DateTime.UtcNow.ToString("O")
                    };
                }

                health.ScraperReachable = true;

                // Round-trip auth check — proves shared X-API-Key matches
                var (apiKeyValid, ping) = await ProbeApiKeyAsync(cancellationToken);
                health.ApiKeyValid = apiKeyValid;
                if (ping != null)
                {
                    if (!health.ApiKeyConfigured)
                        health.ApiKeyConfigured = ping.ApiKeyConfigured;
                    if (!health.WebhookConfigured)
                        health.WebhookConfigured = ping.WebhookConfigured;
                }

                var hasCriticalTokenAlert = health.TokenAlerts.Any(a =>
                    string.Equals(a.Level, "critical", StringComparison.OrdinalIgnoreCase));

                health.IntegrationReady =
                    health.ScraperReachable
                    && health.ApiKeyValid
                    && !string.Equals(health.Status, "disabled", StringComparison.OrdinalIgnoreCase)
                    && !hasCriticalTokenAlert
                    && (string.Equals(health.Database, "connected", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(health.Database, "disabled", StringComparison.OrdinalIgnoreCase)
                        || string.IsNullOrEmpty(health.Database));

                return health;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Number scraper health check failed");
                return new NumberSeekerHealthDto
                {
                    Status = "unreachable",
                    ScraperReachable = false,
                    ApiKeyValid = false,
                    IntegrationReady = false,
                    Timestamp = DateTime.UtcNow.ToString("O")
                };
            }
        }

        public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        {
            var health = await GetHealthAsync(cancellationToken);
            return health.ScraperReachable &&
                   health.ApiKeyValid &&
                   !string.Equals(health.Status, "disabled", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<ScraperPlatformTokenListRaw> GetPlatformTokensAsync(
            CancellationToken cancellationToken = default)
        {
            EnsureEnabled();
            using var response = await _httpClient.GetAsync("api/platform-tokens", cancellationToken);
            await EnsureSuccessOrThrowAsync(response, cancellationToken);
            var data = await response.Content.ReadFromJsonAsync<ScraperPlatformTokenListRaw>(
                JsonOptions,
                cancellationToken);
            return data ?? new ScraperPlatformTokenListRaw();
        }

        public async Task<ScraperTokenAlertsRaw> GetPlatformTokenAlertsAsync(
            CancellationToken cancellationToken = default)
        {
            EnsureEnabled();
            using var response = await _httpClient.GetAsync("api/platform-tokens/alerts", cancellationToken);
            await EnsureSuccessOrThrowAsync(response, cancellationToken);
            var data = await response.Content.ReadFromJsonAsync<ScraperTokenAlertsRaw>(
                JsonOptions,
                cancellationToken);
            return data ?? new ScraperTokenAlertsRaw();
        }

        public async Task<ScraperTokenSavedRaw> SaveDivarTokenAsync(
            string token,
            string? refreshToken,
            string? frontToken,
            CancellationToken cancellationToken = default)
        {
            EnsureEnabled();
            using var response = await _httpClient.PutAsJsonAsync(
                "api/platform-tokens/divar",
                new
                {
                    token,
                    refresh_token = string.IsNullOrWhiteSpace(refreshToken) ? null : refreshToken.Trim(),
                    front_token = string.IsNullOrWhiteSpace(frontToken) ? null : frontToken.Trim()
                },
                JsonOptions,
                cancellationToken);
            await EnsureSuccessOrThrowAsync(response, cancellationToken);
            var data = await response.Content.ReadFromJsonAsync<ScraperTokenSavedRaw>(
                JsonOptions,
                cancellationToken);
            return data ?? new ScraperTokenSavedRaw { Message = "توکن دیوار ذخیره شد", Platform = "divar" };
        }

        public async Task<ScraperTokenSavedRaw> SaveSheypoorTokenAsync(
            string accessToken,
            string? refreshToken,
            CancellationToken cancellationToken = default)
        {
            EnsureEnabled();
            using var response = await _httpClient.PutAsJsonAsync(
                "api/platform-tokens/sheypoor",
                new
                {
                    access_token = accessToken,
                    refresh_token = string.IsNullOrWhiteSpace(refreshToken) ? null : refreshToken.Trim()
                },
                JsonOptions,
                cancellationToken);
            await EnsureSuccessOrThrowAsync(response, cancellationToken);
            var data = await response.Content.ReadFromJsonAsync<ScraperTokenSavedRaw>(
                JsonOptions,
                cancellationToken);
            return data ?? new ScraperTokenSavedRaw { Message = "توکن شیپور ذخیره شد", Platform = "sheypoor" };
        }

        public async Task<ScraperTokenMaintenanceRaw> RunTokenMaintenanceAsync(
            bool forceSheypoorRefresh = false,
            bool forceDivarRefresh = false,
            CancellationToken cancellationToken = default)
        {
            EnsureEnabled();
            var url =
                $"api/platform-tokens/maintenance?force_sheypoor_refresh={forceSheypoorRefresh.ToString().ToLowerInvariant()}" +
                $"&force_divar_refresh={forceDivarRefresh.ToString().ToLowerInvariant()}";
            using var response = await _httpClient.PostAsync(url, null, cancellationToken);
            await EnsureSuccessOrThrowAsync(response, cancellationToken);
            var data = await response.Content.ReadFromJsonAsync<ScraperTokenMaintenanceRaw>(
                JsonOptions,
                cancellationToken);
            return data ?? new ScraperTokenMaintenanceRaw();
        }

        private async Task<(bool Valid, IntegrationPingResponse? Ping)> ProbeApiKeyAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                using var response = await _httpClient.GetAsync("api/integration/ping", cancellationToken);
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    _logger.LogError("ALERT scraper API key rejected on /api/integration/ping");
                    return (false, null);
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Scraper integration ping returned HTTP {StatusCode}",
                        (int)response.StatusCode);
                    return (false, null);
                }

                var ping = await response.Content.ReadFromJsonAsync<IntegrationPingResponse>(
                    JsonOptions,
                    cancellationToken);
                return (ping?.Ok == true, ping);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Scraper integration ping failed");
                return (false, null);
            }
        }

        private void EnsureEnabled()
        {
            if (!_settings.Enabled)
            {
                throw new InvalidOperationException("SCRAPER_DISABLED");
            }
        }

        private async Task EnsureSuccessOrThrowAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode)
                return;

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var detail = TryExtractDetail(body);
            // جزئیات فنی فقط در لاگ — هرگز به لایه بالاتر برای نمایش کاربر نرود
            _logger.LogWarning(
                "Scraper HTTP {StatusCode} body={Body}",
                (int)response.StatusCode,
                string.IsNullOrWhiteSpace(body) ? "(empty)" : (body.Length > 500 ? body[..500] : body));

            throw response.StatusCode switch
            {
                HttpStatusCode.TooManyRequests => new InvalidOperationException("RATE_LIMITED"),
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new UnauthorizedAccessException("SCRAPER_AUTH"),
                HttpStatusCode.BadRequest => new ArgumentException(
                    IsLikelyUserValidationDetail(detail) ? "INVALID_INPUT" : "SCRAPER_BAD_REQUEST"),
                HttpStatusCode.NotFound => new KeyNotFoundException("SCRAPER_NOT_FOUND"),
                _ => new HttpRequestException("SCRAPER_UNAVAILABLE", null, response.StatusCode)
            };
        }

        private static bool IsLikelyUserValidationDetail(string? detail)
        {
            if (string.IsNullOrWhiteSpace(detail))
                return true;

            // پیام‌های validation فارسی اسکرپر (منبع/شهر/دسته نامعتبر)
            var d = detail.Trim();
            return d.Contains("نامعتبر") || d.Contains("الزامی") || d.Contains("مجاز") ||
                   d.Contains("باید") || d.Contains("بین") || d.Contains("source") ||
                   d.Contains("شهر") || d.Contains("دسته") || d.Contains("منبع");
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
                return null;
            }

            return null;
        }

        private sealed class ScraperMessageResponse
        {
            public string? Message { get; set; }
        }

        private sealed class IntegrationPingResponse
        {
            public bool Ok { get; set; }
            public bool ApiKeyValid { get; set; }
            public bool ApiKeyConfigured { get; set; }
            public bool WebhookConfigured { get; set; }
        }
    }
}
