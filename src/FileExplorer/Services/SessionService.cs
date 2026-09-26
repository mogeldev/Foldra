using System.IO;
using System.Text.Json;
using FileExplorer.Models;

namespace FileExplorer.Services;

/// <summary>
/// Persists the session (window, open tabs, favorites) to
/// <c>%APPDATA%\Foldra\session.json</c>.
/// </summary>
public static class SessionService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    public static string SettingsDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Foldra");

    private static string SessionFile => Path.Combine(SettingsDirectory, "session.json");

    public static SessionState Load()
    {
        try
        {
            if (!File.Exists(SessionFile))
                return CreateDefault();

            var json = File.ReadAllText(SessionFile);
            var state = JsonSerializer.Deserialize<SessionState>(json, Options);
            if (state is null || state.Tabs.Count == 0)
                return CreateDefault();

            return state;
        }
        catch (Exception)
        {
            // A corrupt settings file must never block startup.
            return CreateDefault();
        }
    }

    public static void Save(SessionState state)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            File.WriteAllText(SessionFile, JsonSerializer.Serialize(state, Options));
        }
        catch (Exception)
        {
            // Losing the session is not worth an error dialog on shutdown.
        }
    }

    private static SessionState CreateDefault()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return new SessionState
        {
            Tabs = [new TabState { Path = home, History = [home], HistoryIndex = 0 }],
            Favorites =
            [
                new FavoriteState { Path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) },
                new FavoriteState { Path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) },
                new FavoriteState { Path = Path.Combine(home, "Downloads") },
                new FavoriteState { Path = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) },
            ],
        };
    }
}
