using System.Collections.Specialized;
using System.IO;
using System.Windows;

namespace FileExplorer.Services;

/// <summary>
/// Clipboard exchange in the format Explorer uses: CF_HDROP plus the "Preferred DropEffect"
/// blob that distinguishes Ausschneiden from Kopieren. This makes copy/paste work both ways
/// between this app and the real Explorer.
/// </summary>
public static class ClipboardService
{
    private const string PreferredDropEffect = "Preferred DropEffect";

    public static void SetFiles(IReadOnlyList<string> paths, bool cut)
    {
        if (paths.Count == 0)
            return;

        var files = new StringCollection();
        foreach (var path in paths)
            files.Add(path);

        var data = new DataObject();
        data.SetFileDropList(files);

        var effect = cut ? DragDropEffects.Move : DragDropEffects.Copy;
        data.SetData(PreferredDropEffect, new MemoryStream(BitConverter.GetBytes((int)effect)));

        try
        {
            Clipboard.SetDataObject(data, true);
        }
        catch (Exception)
        {
            // Another process may hold the clipboard open; the user can simply retry.
        }
    }

    public static (IReadOnlyList<string> Paths, bool IsCut) GetFiles()
    {
        try
        {
            if (!Clipboard.ContainsFileDropList())
                return ([], false);

            var list = Clipboard.GetFileDropList().Cast<string?>()
                .Where(p => !string.IsNullOrEmpty(p))
                .Select(p => p!)
                .ToList();

            var cut = false;
            if (Clipboard.GetData(PreferredDropEffect) is MemoryStream stream)
            {
                var buffer = new byte[4];
                if (stream.Read(buffer, 0, 4) == 4)
                    cut = ((DragDropEffects)BitConverter.ToInt32(buffer)).HasFlag(DragDropEffects.Move);
            }

            return (list, cut);
        }
        catch (Exception)
        {
            return ([], false);
        }
    }

    public static bool HasFiles()
    {
        try
        {
            return Clipboard.ContainsFileDropList();
        }
        catch (Exception)
        {
            return false;
        }
    }
}
