using Api_Vapp.Constants;
using Api_Vapp.DTOs.Admin;
using Api_Vapp.DTOs.Common;
using Api_Vapp.DTOs.NumberSeeker;
using Api_Vapp.Interfaces;
using Api_Vapp.Utilities;

namespace Api_Vapp.Services
{
    public class AdminScraperTokenService : IAdminScraperTokenService
    {
        private static readonly string[] PlatformOrder = { "divar", "sheypoor" };

        private readonly INumberScraperClient _scraper;
        private readonly ILogger<AdminScraperTokenService> _logger;

        public AdminScraperTokenService(
            INumberScraperClient scraper,
            ILogger<AdminScraperTokenService> logger)
        {
            _scraper = scraper;
            _logger = logger;
        }

        public async Task<ApiResponse<AdminScraperTokensOverviewDto>> GetOverviewAsync()
        {
            try
            {
                if (!_scraper.IsEnabled)
                {
                    return ApiResponse<AdminScraperTokensOverviewDto>.CreateSuccess(
                        new AdminScraperTokensOverviewDto
                        {
                            ScraperEnabled = false,
                            ScraperReachable = false,
                            Hint = "سرویس اسکرپر در تنظیمات API غیرفعال است."
                        });
                }

                ScraperPlatformTokenListRaw tokens;
                try
                {
                    tokens = await _scraper.GetPlatformTokensAsync();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Failed to load scraper platform tokens");
                    return ApiResponse<AdminScraperTokensOverviewDto>.CreateSuccess(
                        new AdminScraperTokensOverviewDto
                        {
                            ScraperEnabled = true,
                            ScraperReachable = false,
                            Hint = "اسکرپر در دسترس نیست. اتصال و کلید API را بررسی کنید."
                        });
                }

                var platforms = MapPlatforms(tokens.Platforms);
                List<AdminScraperTokenAlertDto> alerts;
                try
                {
                    var alertsRaw = await _scraper.GetPlatformTokenAlertsAsync();
                    alerts = MapAlerts(alertsRaw.Alerts);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // مسیر /alerts گاهی با روت {platform} تداخل دارد — از وضعیت پلتفرم‌ها هشدار بساز
                    _logger.LogWarning(ex, "Platform token alerts endpoint failed; deriving from statuses");
                    alerts = DeriveAlertsFromPlatforms(platforms);
                }

                return ApiResponse<AdminScraperTokensOverviewDto>.CreateSuccess(
                    new AdminScraperTokensOverviewDto
                    {
                        ScraperEnabled = true,
                        ScraperReachable = true,
                        Platforms = platforms,
                        Alerts = alerts,
                        Hint = null
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin scraper token overview failed");
                return ApiResponse<AdminScraperTokensOverviewDto>.InternalServerError(
                    ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<AdminScraperTokenSaveResultDto>> SaveDivarAsync(SaveDivarTokenDto dto)
        {
            try
            {
                if (!_scraper.IsEnabled)
                    return ScraperDisabledSaveResult();

                var saved = await _scraper.SaveDivarTokenAsync(
                    dto.Token.Trim(),
                    dto.RefreshToken,
                    dto.FrontToken);

                return ApiResponse<AdminScraperTokenSaveResultDto>.CreateSuccess(
                    new AdminScraperTokenSaveResultDto
                    {
                        Platform = "divar",
                        Message = string.IsNullOrWhiteSpace(saved.Message)
                            ? "توکن دیوار ذخیره شد"
                            : saved.Message,
                        Status = saved.Status == null ? null : MapStatus(saved.Status)
                    },
                    message: "توکن دیوار ذخیره شد");
            }
            catch (ArgumentException)
            {
                return ApiResponse<AdminScraperTokenSaveResultDto>.BadRequest(
                    "توکن دیوار معتبر نیست. مقدار را دوباره بررسی کنید.",
                    errorCode: ErrorCodes.InvalidInput);
            }
            catch (InvalidOperationException ex) when (ex.Message == "SCRAPER_DISABLED")
            {
                return ScraperDisabledSaveResult();
            }
            catch (UnauthorizedAccessException)
            {
                return ApiResponse<AdminScraperTokenSaveResultDto>.BadRequest(
                    "احراز هویت اسکرپر ناموفق بود. کلید API را بررسی کنید.",
                    errorCode: ErrorCodes.InvalidInput);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Save Divar token via scraper failed");
                return ApiResponse<AdminScraperTokenSaveResultDto>.BadRequest(
                    "ذخیره توکن دیوار انجام نشد. اتصال اسکرپر را بررسی کنید.",
                    errorCode: ErrorCodes.InvalidInput);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Save Divar token unexpected error");
                return ApiResponse<AdminScraperTokenSaveResultDto>.InternalServerError(
                    ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<AdminScraperTokenSaveResultDto>> SaveSheypoorAsync(SaveSheypoorTokenDto dto)
        {
            try
            {
                if (!_scraper.IsEnabled)
                    return ScraperDisabledSaveResult();

                var saved = await _scraper.SaveSheypoorTokenAsync(
                    dto.AccessToken.Trim(),
                    dto.RefreshToken);

                return ApiResponse<AdminScraperTokenSaveResultDto>.CreateSuccess(
                    new AdminScraperTokenSaveResultDto
                    {
                        Platform = "sheypoor",
                        Message = string.IsNullOrWhiteSpace(saved.Message)
                            ? "توکن شیپور ذخیره شد"
                            : saved.Message,
                        Status = saved.Status == null ? null : MapStatus(saved.Status)
                    },
                    message: "توکن شیپور ذخیره شد");
            }
            catch (ArgumentException)
            {
                return ApiResponse<AdminScraperTokenSaveResultDto>.BadRequest(
                    "توکن شیپور معتبر نیست. مقدار را دوباره بررسی کنید.",
                    errorCode: ErrorCodes.InvalidInput);
            }
            catch (InvalidOperationException ex) when (ex.Message == "SCRAPER_DISABLED")
            {
                return ScraperDisabledSaveResult();
            }
            catch (UnauthorizedAccessException)
            {
                return ApiResponse<AdminScraperTokenSaveResultDto>.BadRequest(
                    "احراز هویت اسکرپر ناموفق بود. کلید API را بررسی کنید.",
                    errorCode: ErrorCodes.InvalidInput);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Save Sheypoor token via scraper failed");
                return ApiResponse<AdminScraperTokenSaveResultDto>.BadRequest(
                    "ذخیره توکن شیپور انجام نشد. اتصال اسکرپر را بررسی کنید.",
                    errorCode: ErrorCodes.InvalidInput);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Save Sheypoor token unexpected error");
                return ApiResponse<AdminScraperTokenSaveResultDto>.InternalServerError(
                    ControlledErrorHelper.Unexpected);
            }
        }

        public async Task<ApiResponse<AdminScraperTokenMaintenanceDto>> RunMaintenanceAsync(
            bool forceSheypoorRefresh = false,
            bool forceDivarRefresh = false)
        {
            try
            {
                if (!_scraper.IsEnabled)
                {
                    return ApiResponse<AdminScraperTokenMaintenanceDto>.BadRequest(
                        "سرویس اسکرپر غیرفعال است.",
                        errorCode: ErrorCodes.InvalidInput);
                }

                var raw = await _scraper.RunTokenMaintenanceAsync(forceSheypoorRefresh, forceDivarRefresh);
                var platforms = raw.Platforms == null
                    ? new List<AdminScraperTokenStatusDto>()
                    : MapPlatforms(raw.Platforms);

                var refreshed = raw.Refreshed ?? new List<string>();
                var message = refreshed.Count > 0
                    ? $"تمدید انجام شد: {string.Join("، ", refreshed.Select(DisplayName))}"
                    : "نگهداری توکن‌ها اجرا شد؛ تمدید جدیدی لازم نبود.";

                return ApiResponse<AdminScraperTokenMaintenanceDto>.CreateSuccess(
                    new AdminScraperTokenMaintenanceDto
                    {
                        Refreshed = refreshed,
                        Alerts = MapAlerts(raw.Alerts),
                        Platforms = platforms,
                        Message = message
                    },
                    message: message);
            }
            catch (InvalidOperationException ex) when (ex.Message == "SCRAPER_DISABLED")
            {
                return ApiResponse<AdminScraperTokenMaintenanceDto>.BadRequest(
                    "سرویس اسکرپر غیرفعال است.",
                    errorCode: ErrorCodes.InvalidInput);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Token maintenance via scraper failed");
                return ApiResponse<AdminScraperTokenMaintenanceDto>.BadRequest(
                    "اجرای نگهداری توکن‌ها ناموفق بود. اتصال اسکرپر را بررسی کنید.",
                    errorCode: ErrorCodes.InvalidInput);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token maintenance unexpected error");
                return ApiResponse<AdminScraperTokenMaintenanceDto>.InternalServerError(
                    ControlledErrorHelper.Unexpected);
            }
        }

        private static ApiResponse<AdminScraperTokenSaveResultDto> ScraperDisabledSaveResult()
            => ApiResponse<AdminScraperTokenSaveResultDto>.BadRequest(
                "سرویس اسکرپر غیرفعال است.",
                errorCode: ErrorCodes.InvalidInput);

        private static List<AdminScraperTokenStatusDto> MapPlatforms(
            Dictionary<string, ScraperPlatformTokenStatusRaw>? platforms)
        {
            if (platforms == null || platforms.Count == 0)
                return new List<AdminScraperTokenStatusDto>();

            var list = new List<AdminScraperTokenStatusDto>();
            foreach (var key in PlatformOrder)
            {
                if (platforms.TryGetValue(key, out var status))
                    list.Add(MapStatus(status));
            }

            foreach (var (key, status) in platforms)
            {
                if (PlatformOrder.Contains(key, StringComparer.OrdinalIgnoreCase))
                    continue;
                list.Add(MapStatus(status));
            }

            return list;
        }

        private static AdminScraperTokenStatusDto MapStatus(ScraperPlatformTokenStatusRaw raw)
        {
            var platform = (raw.Platform ?? string.Empty).Trim().ToLowerInvariant();
            return new AdminScraperTokenStatusDto
            {
                Platform = platform,
                DisplayName = DisplayName(platform),
                Configured = raw.Configured,
                Ready = raw.Ready,
                IsExpired = raw.IsExpired,
                RefreshExpired = raw.RefreshExpired,
                ExpiresAt = raw.ExpiresAt,
                DaysRemaining = raw.DaysRemaining,
                MaskedToken = raw.MaskedToken,
                AlertLevel = string.IsNullOrWhiteSpace(raw.AlertLevel) ? "none" : raw.AlertLevel,
                AutoRefreshSupported = raw.AutoRefreshSupported,
                Tokens = (raw.Tokens ?? new List<ScraperTokenDetailRaw>())
                    .Select(t => new AdminScraperTokenDetailDto
                    {
                        Name = t.Name,
                        Configured = t.Configured,
                        Masked = t.Masked,
                        ExpiresAt = t.ExpiresAt,
                        IsExpired = t.IsExpired,
                        DaysRemaining = t.DaysRemaining
                    })
                    .ToList()
            };
        }

        private static List<AdminScraperTokenAlertDto> MapAlerts(List<ScraperTokenAlertRaw>? alerts)
        {
            if (alerts == null || alerts.Count == 0)
                return new List<AdminScraperTokenAlertDto>();

            return alerts.Select(a => new AdminScraperTokenAlertDto
            {
                Platform = a.Platform,
                Level = a.Level,
                Code = a.Code,
                Message = a.Message
            }).ToList();
        }

        private static List<AdminScraperTokenAlertDto> DeriveAlertsFromPlatforms(
            List<AdminScraperTokenStatusDto> platforms)
        {
            var alerts = new List<AdminScraperTokenAlertDto>();
            foreach (var p in platforms)
            {
                var level = (p.AlertLevel ?? "none").Trim().ToLowerInvariant();
                if (level is "none" or "")
                    continue;

                var name = DisplayName(p.Platform);
                string message;
                string code;
                if (!p.Configured)
                {
                    code = "not_configured";
                    message = $"توکن {name} تنظیم نشده است.";
                }
                else if (p.IsExpired == true)
                {
                    code = "expired";
                    message = $"توکن {name} منقضی شده — مقدار جدید را ذخیره کنید.";
                }
                else if (!p.Ready)
                {
                    code = "not_ready";
                    message = $"توکن {name} آماده نیست — مقدار را بررسی کنید.";
                }
                else
                {
                    code = level;
                    message = $"هشدار توکن {name} ({level}).";
                }

                alerts.Add(new AdminScraperTokenAlertDto
                {
                    Platform = p.Platform,
                    Level = level,
                    Code = code,
                    Message = message
                });
            }

            return alerts;
        }

        private static string DisplayName(string platform) => platform.Trim().ToLowerInvariant() switch
        {
            "divar" => "دیوار",
            "sheypoor" => "شیپور",
            _ => platform
        };
    }
}
