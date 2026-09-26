using System.IO;

namespace FileExplorer.Services;

/// <summary>
/// Minimal startup/error trace written next to the session file. Enabled by setting the
/// environment variable <c>FOLDRA_TRACE=1</c>, so normal runs stay silent.
/// </summary>
public static class Diagnostics
{
    private static readonly bool Enabled =
        Environment.GetEnvironmentVariable("FOLDRA_TRACE") == "1";

    private static readonly object Gate = new();

    public static void Log(string message)
    {
        if (!Enabled)
            return;

        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(SessionService.SettingsDirectory);
                File.AppendAllText(
                    Path.Combine(SessionService.SettingsDirectory, "trace.log"),
                    $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
        }
        catch (Exception)
        {
            // Tracing must never influence the run it observes.
        }
    }
}
