using Api_Vapp.Configuration;
using Api_Vapp.Interfaces;
using Api_Vapp.Services.Audit;
using Api_Vapp.Services.BackgroundServices;
using Microsoft.Extensions.DependencyInjection;

namespace Api_Vapp.Extensions
{
    public static class AuditServiceExtensions
    {
        public static IServiceCollection AddAuditLogging(this IServiceCollection services)
        {
            services.AddOptions<AuditOptions>()
                .BindConfiguration(AuditOptions.SectionName)
                .Validate(o => o.RetentionDays >= o.MinRetentionDays, "Audit:RetentionDays must be >= MinRetentionDays")
                .Validate(o => o.MinRetentionDays >= 30, "Audit:MinRetentionDays must be >= 30")
                .ValidateOnStart();

            services.AddScoped<IAuditContext, HttpAuditContext>();
            services.AddScoped<IAuditService, AuditService>();
            services.AddScoped<IAuditQueryService, AuditQueryService>();

            services.AddHostedService<AuditRetentionBackgroundService>();
            services.AddHostedService<AuditAdminLoginFailAlertBackgroundService>();
            return services;
        }
    }
}
