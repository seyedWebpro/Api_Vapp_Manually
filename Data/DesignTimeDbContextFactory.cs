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
            // خواندن تنظیمات از appsettings.json
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<Api_Context>();
            
            // استفاده از connection string از تنظیمات
            var connectionString = configuration["defultConnection"] ?? configuration.GetConnectionString("DefaultConnection");
            
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Connection string 'defultConnection' or 'DefaultConnection' not found in appsettings.json");
            }

            optionsBuilder.UseSqlServer(connectionString);

            return new Api_Context(optionsBuilder.Options);
        }
    }
}





