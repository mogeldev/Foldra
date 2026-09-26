using System.IO;
using System.Runtime.InteropServices;

namespace FileExplorer.Interop;

/// <summary>
/// Thin wrappers around the shell namespace: parsing paths into shell items / PIDLs and
/// reading the display names the Explorer itself shows.
/// </summary>
internal static class ShellHelper
{
    /// <summary>
    /// A failed shell HRESULT. The runtime maps well-known codes to specific exceptions rather
    /// than COMException - a missing path (0x80070002) arrives as FileNotFoundException,
    /// E_ACCESSDENIED as UnauthorizedAccessException, E_INVALIDARG as ArgumentException.
    /// </summary>
    internal static bool IsShellFailure(Exception ex)
        => ex is COMException or IOException or UnauthorizedAccessException or ArgumentException;

    internal static IShellItem? CreateItem(string path)
    {
        try
        {
            NativeMethods.SHCreateItemFromParsingName(path, IntPtr.Zero, ShellGuids.IShellItem, out var item);
            return item;
        }
        catch (Exception ex) when (IsShellFailure(ex))
        {
            return null;
        }
    }

    internal static IntPtr CreatePidl(string path)
    {
        try
        {
            NativeMethods.SHParseDisplayName(path, IntPtr.Zero, out var pidl, 0, out _);
            return pidl;
        }
        catch (Exception ex) when (IsShellFailure(ex))
        {
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// Localized display name as shown in Explorer ("Dokumente" instead of "Documents",
    /// "Windows (C:)" instead of "C:\").
    /// </summary>
    internal static string GetDisplayName(string path, SIGDN form = SIGDN.NormalDisplay)
    {
        var item = CreateItem(path);
        if (item is null)
            return Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)) is { Length: > 0 } n ? n : path;

        try
        {
            item.GetDisplayName(form, out var ptr);
            try
            {
                return Marshal.PtrToStringUni(ptr) ?? path;
            }
            finally
            {
                Marshal.FreeCoTaskMem(ptr);
            }
        }
        catch (Exception ex) when (ShellHelper.IsShellFailure(ex))
        {
            return path;
        }
        finally
        {
            Marshal.ReleaseComObject(item);
        }
    }

    /// <summary>Localized file type description, e.g. "Textdokument".</summary>
    internal static string GetTypeName(string path, bool isDirectory)
    {
        var info = new SHFILEINFO();
        var attributes = isDirectory ? 0x10u : 0x80u; // FILE_ATTRIBUTE_DIRECTORY / _NORMAL

        // Drives need the real item ("Local Disk"); faking attributes yields "File folder".
        var flags = IsVolumeRoot(path) ? SHGFI.TypeName : SHGFI.TypeName | SHGFI.UseFileAttributes;
        var result = NativeMethods.SHGetFileInfo(
            path, attributes, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);

        return result == IntPtr.Zero ? string.Empty : info.szTypeName;
    }

    /// <summary>Whether <paramref name="path"/> is a drive or share root such as <c>C:\</c>.</summary>
    internal static bool IsVolumeRoot(string path)
    {
        try
        {
            var root = Path.GetPathRoot(path);
            return !string.IsNullOrEmpty(root) &&
                string.Equals(root.TrimEnd(Path.DirectorySeparatorChar), path.TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Opens Explorer at the parent folder with <paramref name="path"/> selected.</summary>
    internal static void RevealInExplorer(string path)
    {
        var pidl = CreatePidl(path);
        if (pidl == IntPtr.Zero)
            return;

        try
        {
            NativeMethods.SHOpenFolderAndSelectItems(pidl, 0, null, 0);
        }
        catch (Exception ex) when (ShellHelper.IsShellFailure(ex))
        {
            // Nothing sensible to do; the caller already has the path open.
        }
        finally
        {
            NativeMethods.ILFree(pidl);
        }
    }
}
