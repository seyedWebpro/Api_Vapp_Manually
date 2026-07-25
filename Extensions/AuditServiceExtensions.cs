using Api_Vapp.Interfaces;
using Api_Vapp.Services.Audit;
using Microsoft.Extensions.DependencyInjection;

namespace Api_Vapp.Extensions
{
    public static class AuditServiceExtensions
    {
        public static IServiceCollection AddAuditLogging(this IServiceCollection services)
        {
            services.AddScoped<IAuditContext, HttpAuditContext>();
            services.AddScoped<IAuditService, AuditService>();
            services.AddScoped<IAuditQueryService, AuditQueryService>();
            return services;
        }
    }
}
