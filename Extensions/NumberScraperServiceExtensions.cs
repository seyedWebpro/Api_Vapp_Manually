using System.Net;
using Api_Vapp.Configuration;
using Api_Vapp.Interfaces;
using Api_Vapp.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace Api_Vapp.Extensions
{
    public static class NumberScraperServiceExtensions
    {
        public static IServiceCollection AddNumberScraperIntegration(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var settings = configuration
                .GetSection(NumberScraperApiSettings.SectionName)
                .Get<NumberScraperApiSettings>() ?? new NumberScraperApiSettings();

            NumberScraperApiKeyConfiguration.Apply(configuration, settings);

            services.Configure<NumberScraperApiSettings>(opts =>
            {
                configuration.GetSection(NumberScraperApiSettings.SectionName).Bind(opts);
                NumberScraperApiKeyConfiguration.Apply(configuration, opts);
            });

            if (!settings.Enabled)
            {
                services.AddScoped<INumberScraperClient, DisabledNumberScraperClient>();
                return services;
            }

            services.AddHttpClient<INumberScraperClient, NumberScraperClient>(client =>
            {
                if (!string.IsNullOrWhiteSpace(settings.BaseUrl))
                {
                    client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");
                }

                client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds + 10);

                if (!client.DefaultRequestHeaders.Contains("Accept"))
                {
                    client.DefaultRequestHeaders.Add("Accept", "application/json");
                }

                if (!client.DefaultRequestHeaders.Contains("User-Agent"))
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Vapp-DotNet/1.0");
                }

                if (!string.IsNullOrWhiteSpace(settings.ApiKey) &&
                    !client.DefaultRequestHeaders.Contains("X-API-Key"))
                {
                    client.DefaultRequestHeaders.Add("X-API-Key", settings.ApiKey);
                }
            })
            .AddPolicyHandler((sp, _) => GetRetryPolicy(settings.RetryPolicy, sp.GetService<ILogger<NumberScraperClient>>()))
            .AddPolicyHandler((sp, _) => GetCircuitBreakerPolicy(settings.CircuitBreaker, sp.GetService<ILogger<NumberScraperClient>>()))
            .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(
                TimeSpan.FromSeconds(settings.TimeoutSeconds)));

            return services;
        }

        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(
            RetryPolicySettings settings,
            ILogger? logger)
        {
            var maxRetries = settings?.MaxRetries ?? 3;
            var retryDelayMs = settings?.RetryDelayMs ?? 1000;
            var useExponentialBackoff = settings?.UseExponentialBackoff ?? true;

            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<TimeoutRejectedException>()
                .OrResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests)
                .WaitAndRetryAsync(
                    maxRetries,
                    retryAttempt =>
                    {
                        var delay = useExponentialBackoff
                            ? TimeSpan.FromMilliseconds(retryDelayMs * Math.Pow(2, retryAttempt - 1))
                            : TimeSpan.FromMilliseconds(retryDelayMs);
                        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500));
                        return delay + jitter;
                    },
                    onRetry: (outcome, timespan, retryAttempt, _) =>
                    {
                        logger?.LogWarning(
                            "NumberScraper retry {Attempt}/{Max} after {Delay}ms — {Reason}",
                            retryAttempt,
                            maxRetries,
                            timespan.TotalMilliseconds,
                            outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                    });
        }

        private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(
            CircuitBreakerSettings settings,
            ILogger? logger)
        {
            var failuresBeforeBreaking = settings?.FailuresBeforeBreaking ?? 5;
            var durationOfBreakSeconds = settings?.DurationOfBreakSeconds ?? 30;

            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<TimeoutRejectedException>()
                .CircuitBreakerAsync(
                    failuresBeforeBreaking,
                    TimeSpan.FromSeconds(durationOfBreakSeconds),
                    onBreak: (outcome, breakDelay) =>
                    {
                        logger?.LogError(
                            "NumberScraper circuit OPEN for {Seconds}s — {Reason}",
                            breakDelay.TotalSeconds,
                            outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                    },
                    onReset: () => logger?.LogInformation("NumberScraper circuit CLOSED"),
                    onHalfOpen: () => logger?.LogInformation("NumberScraper circuit HALF-OPEN"));
        }
    }
}
