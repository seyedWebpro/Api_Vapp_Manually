using Api_Vapp.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Api_Vapp.Data
{
    /// <summary>
    /// Factory برای ایجاد DbContext در زمان طراحی (Design-Time)
    /// مورد استفاده توسط EF Core Migrations
    /// </summary>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<Api_Context>
    {
        public Api_Context CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddJsonFile(
                    OperatingSystem.IsWindows()
                        ? "appsettings.Development.Windows.json"
                        : "appsettings.Development.Mac.json",
                    optional: true)
                .AddEnvironmentVariables()
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<Api_Context>();
            optionsBuilder.UseSqlServer(SqlServerConnectionConfiguration.GetConnectionString(configuration));

            return new Api_Context(optionsBuilder.Options);
        }
    }
}





