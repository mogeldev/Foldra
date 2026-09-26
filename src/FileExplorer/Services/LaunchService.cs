using System.Diagnostics;
using System.Windows;

namespace FileExplorer.Services;

/// <summary>Opens files with their registered handler, i.e. a plain ShellExecute.</summary>
public static class LaunchService
{
    public static void Open(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Could not open", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public static void OpenWithDialog(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("rundll32.exe", $"shell32.dll,OpenAs_RunDLL \"{path}\"")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Open with", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public static void OpenTerminal(string folder)
    {
        try
        {
            Process.Start(new ProcessStartInfo("powershell.exe")
            {
                WorkingDirectory = folder,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Terminal", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
