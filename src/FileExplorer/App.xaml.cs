using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using FileExplorer.Interop;


namespace FileExplorer;

public partial class App : Application
{
    /// <summary>Index within Resources.MergedDictionaries that holds the accent-derived
    /// brushes; appended once, then replaced in place so it always wins over the base
    /// theme's own keys (WPF resolves duplicate keys from the last merged dictionary).</summary>
    private int _accentDictionaryIndex = -1;

    /// <summary>Whether the dark palette is currently active; the main window needs this to
    /// also paint its native title bar dark, which isn't covered by WPF resources.</summary>
    public bool IsDarkTheme { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Follow the Windows light/dark setting and accent color, like Explorer does.
        Services.Diagnostics.Log("startup: applying theme");
        RefreshTheme();
        Services.Diagnostics.Log("startup: theme applied");

        DispatcherUnhandledException += OnUnhandledException;
    }

    /// <summary>Re-reads the OS light/dark setting and accent color and reapplies both. Safe to
    /// call repeatedly, e.g. when Windows notifies the main window that either changed.</summary>
    public void RefreshTheme()
    {
        ApplyTheme();
        ApplyAccent();
    }

    /// <summary>
    /// Swaps in the light or dark palette depending on Windows' app mode setting. The setting
    /// is the same registry value Explorer itself reads; if it is missing (older builds,
    /// locked-down policies) the light palette is used, which is Windows 10's default.
    /// </summary>
    private void ApplyTheme()
    {
        var light = true;

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            if (key?.GetValue("AppsUseLightTheme") is int value)
                light = value != 0;
        }
        catch (Exception)
        {
            // An unreadable setting is not worth failing the start over.
        }

        IsDarkTheme = !light;
        var themeFile = light ? "Views/Theme.Light.xaml" : "Views/Theme.Dark.xaml";
        Resources.MergedDictionaries[0] = new ResourceDictionary { Source = new Uri(themeFile, UriKind.Relative) };
    }

    /// <summary>
    /// Overrides the theme's selection/hover/ribbon-tab colors with ones derived from the
    /// user's Windows accent color, the same way Windows 11 tints its own highlights. Reads
    /// the DWM colorization color, which exists unchanged on both Windows 10 and 11.
    /// </summary>
    private void ApplyAccent()
    {
        Color accent;

        try
        {
            NativeMethods.DwmGetColorizationColor(out var argb, out _);
            accent = Color.FromRgb((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
        }
        catch (Exception)
        {
            // No DWM (e.g. composition disabled) - keep the theme's own fixed accent.
            return;
        }

        Color WithAlpha(byte alpha) => Color.FromArgb(alpha, accent.R, accent.G, accent.B);

        var accentDictionary = new ResourceDictionary
        {
            { "HoverBrush", new SolidColorBrush(WithAlpha(0x26)) },
            { "HoverBorderBrush", new SolidColorBrush(WithAlpha(0x66)) },
            { "SelectedBrush", new SolidColorBrush(WithAlpha(0x4D)) },
            { "SelectedBorderBrush", new SolidColorBrush(accent) },
            { "PressedBrush", new SolidColorBrush(WithAlpha(0x59)) },
            { "FileTabBrush", new SolidColorBrush(accent) },
            { "FileTabHoverBrush", new SolidColorBrush(Darken(accent, 0.85)) },
        };

        if (_accentDictionaryIndex >= 0)
            Resources.MergedDictionaries[_accentDictionaryIndex] = accentDictionary;
        else
        {
            Resources.MergedDictionaries.Add(accentDictionary);
            _accentDictionaryIndex = Resources.MergedDictionaries.Count - 1;
        }
    }

    private static Color Darken(Color color, double factor) => Color.FromRgb(
        (byte)(color.R * factor), (byte)(color.G * factor), (byte)(color.B * factor));

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogError(e.Exception);

        // A misbehaving shell extension should show a message, not kill the window.
        MessageBox.Show(
            e.Exception.Message,
            "Unexpected error",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        e.Handled = true;
    }

    /// <summary>Appends the full exception to %APPDATA%\Foldra\error.log for diagnosis.</summary>
    private static void LogError(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(Services.SessionService.SettingsDirectory);
            File.AppendAllText(
                Path.Combine(Services.SessionService.SettingsDirectory, "error.log"),
                $"{DateTime.Now:u}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // Diagnostics must never become the failure themselves.
        }
    }
}


