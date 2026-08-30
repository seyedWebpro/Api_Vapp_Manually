using Microsoft.Extensions.Configuration;

namespace Api_Vapp.Configuration;

/// <summary>
/// Resolves the SQL Server connection string from <c>DatabaseProvider</c> and <c>ConnectionStrings</c>.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Local</b> — SQL Server on the same machine (Windows Integrated Security or local instance). Uses <c>LocalConnection</c>.</item>
/// <item><b>LocalDocker</b> — SQL Server runs in Docker; API runs on the host (e.g. <c>localhost,1436</c>). Uses <c>LocalDockerHostConnection</c>.</item>
/// <item><b>Docker</b> — API runs inside a container; SQL hostname is the compose service name. Uses <c>DockerConnection</c>. Production: database name <c>DbVapp</c> only.</item>
/// </list>
/// Legacy top-level keys <c>defultConnection</c> / <c>localConnection</c> are not supported.
/// </remarks>
public static class SqlServerConnectionConfiguration
{
    public const string ProviderLocal = "Local";
    public const string ProviderLocalDocker = "LocalDocker";
    public const string ProviderDocker = "Docker";

    public static string GetConnectionString(IConfiguration configuration)
    {
        var databaseProvider = configuration["DatabaseProvider"];
        if (string.IsNullOrWhiteSpace(databaseProvider))
        {
            throw new InvalidOperationException(
                "No database connection configured. Set 'DatabaseProvider' to " +
                $"'{ProviderLocal}', '{ProviderLocalDocker}', or '{ProviderDocker}', " +
                "and provide the matching entry under 'ConnectionStrings' " +
                "(LocalConnection / LocalDockerHostConnection / DockerConnection). " +
                "Legacy keys 'defultConnection' / 'localConnection' are removed.");
        }

        var connectionStringKey = ResolveConnectionStringKey(databaseProvider);
        var connectionString = configuration.GetConnectionString(connectionStringKey);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{connectionStringKey}' was not found. " +
                $"Set 'DatabaseProvider' to '{ProviderLocal}' (SQL on Windows), '{ProviderLocalDocker}' (SQL in Docker, API on host), or '{ProviderDocker}' (API in Docker). " +
                $"Ensure the matching entry exists under 'ConnectionStrings' in appsettings or environment variables.");
        }

        return connectionString;
    }

    public static string ResolveConnectionStringKey(string databaseProvider) =>
        databaseProvider switch
        {
            ProviderDocker => "DockerConnection",
            ProviderLocalDocker => "LocalDockerHostConnection",
            _ => "LocalConnection"
        };
}
