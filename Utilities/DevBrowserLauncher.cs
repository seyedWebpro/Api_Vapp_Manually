namespace Api_Vapp.Utilities;

/// <summary>
/// Opens Swagger in the default browser during local Development (dotnet watch on macOS/ Linux).
/// </summary>
public static class DevBrowserLauncher
{
    private static readonly string DebounceFile = Path.Combine(Path.GetTempPath(), "vapp-dev-browser.lock");
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromSeconds(45);

    public static void Register(WebApplication app, string path = "/swagger")
    {
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(800);

                if (WasOpenedRecently())
                    return;

                var baseUrl = app.Urls.FirstOrDefault(u =>
                        u.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    ?? app.Urls.FirstOrDefault()
                    ?? "http://localhost:5054";

                TryOpen($"{baseUrl.TrimEnd('/')}{path}");
            });
        });
    }

    private static bool WasOpenedRecently()
    {
        try
        {
            if (!File.Exists(DebounceFile))
                return false;

            return DateTime.UtcNow - File.GetLastWriteTimeUtc(DebounceFile) < DebounceWindow;
        }
        catch
        {
            return false;
        }
    }

    private static void TryOpen(string url)
    {
        try
        {
            File.WriteAllText(DebounceFile, DateTime.UtcNow.ToString("O"));

            if (OperatingSystem.IsMacOS())
                System.Diagnostics.Process.Start("open", url);
            else if (OperatingSystem.IsWindows())
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            else if (OperatingSystem.IsLinux())
                System.Diagnostics.Process.Start("xdg-open", url);
        }
        catch
        {
            // Dev convenience only — ignore if the OS blocks browser launch.
        }
    }
}
