using Api_Vapp.Configuration;
using Api_Vapp.Interfaces;
using Api_Vapp.Services.Zohal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace Api_Vapp.Extensions
{
    public static class ZohalServiceExtensions
    {
        public static IServiceCollection AddZohalIntegration(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var settings = configuration
                .GetSection(ZohalApiSettings.SectionName)
                .Get<ZohalApiSettings>() ?? new ZohalApiSettings();

            ZohalApiTokenConfiguration.Apply(configuration, settings);

            services.Configure<ZohalApiSettings>(opts =>
            {
                configuration.GetSection(ZohalApiSettings.SectionName).Bind(opts);
                ZohalApiTokenConfiguration.Apply(configuration, opts);
            });

            if (!settings.Enabled)
            {
                services.AddScoped<IZohalShahkarService, DisabledZohalShahkarService>();
                return services;
            }

            services.AddHttpClient<IZohalShahkarService, ZohalShahkarClient>(client =>
            {
                var baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl)
                    ? "https://service.zohal.io/api/v0"
                    : settings.BaseUrl.TrimEnd('/');

                client.BaseAddress = new Uri(baseUrl + "/");
                client.Timeout = TimeSpan.FromSeconds(Math.Max(5, settings.TimeoutSeconds + 5));

                if (!client.DefaultRequestHeaders.Contains("Accept"))
                {
                    client.DefaultRequestHeaders.Add("Accept", "application/json");
                }

                if (!client.DefaultRequestHeaders.Contains("User-Agent"))
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Vapp-DotNet/1.0");
                }
            })
            .AddPolicyHandler((sp, _) => GetRetryPolicy(sp.GetService<ILogger<ZohalShahkarClient>>()))
            .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(
                TimeSpan.FromSeconds(Math.Max(5, settings.TimeoutSeconds))));

            return services;
        }

        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(ILogger? logger)
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .Or<TimeoutRejectedException>()
                .WaitAndRetryAsync(
                    2,
                    retryAttempt => TimeSpan.FromMilliseconds(500 * retryAttempt),
                    onRetry: (outcome, timespan, retryAttempt, _) =>
                    {
                        logger?.LogWarning(
                            "Zohal Shahkar retry {Attempt}/2 after {Delay}ms — {Reason}",
                            retryAttempt,
                            timespan.TotalMilliseconds,
                            outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                    });
        }
    }
}
