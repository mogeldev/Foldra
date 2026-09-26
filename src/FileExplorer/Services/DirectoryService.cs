using System.IO;
using FileExplorer.Interop;
using FileExplorer.Models;

namespace FileExplorer.Services;

/// <summary>Sentinel paths for the virtual locations that have no file system path.</summary>
public static class KnownLocations
{
    public const string ThisPc = "::ThisPC";

    public static bool IsVirtual(string path) => path == ThisPc;

    public static string DisplayName(string path)
    {
        if (path == ThisPc)
            return "This PC";

        var name = ShellHelper.GetDisplayName(path);
        return string.IsNullOrEmpty(name) ? path : name;
    }
}

public sealed record DirectoryListing(string Path, IReadOnlyList<FileSystemEntry> Entries, string? Error);

public static class DirectoryService
{
    /// <summary>
    /// Enumerates a folder off the UI thread. Access errors are reported as part of the result
    /// rather than thrown, because an unreadable folder is a normal thing to click on.
    /// </summary>
    public static Task<DirectoryListing> ListAsync(string path, bool showHidden, CancellationToken token)
        => Task.Run(() => List(path, showHidden, token), token);

    private static DirectoryListing List(string path, bool showHidden, CancellationToken token)
    {
        if (path == KnownLocations.ThisPc)
            return new DirectoryListing(path, ListDrives(), null);

        try
        {
            var entries = new List<FileSystemEntry>();
            var directory = new DirectoryInfo(path);

            // IgnoreInaccessible would also swallow "access denied" on the folder itself and
            // show it as empty; without recursion no other entry is ever opened, so it is off.
            foreach (var info in directory.EnumerateFileSystemInfos("*", new EnumerationOptions
            {
                IgnoreInaccessible = false,
                AttributesToSkip = 0,
                RecurseSubdirectories = false,
            }))
            {
                token.ThrowIfCancellationRequested();

                // One entry that vanishes mid-enumeration, or a reparse point the shell
                // cannot follow, must not cost the whole listing - skip just that row.
                try
                {
                    var attributes = info.Attributes;
                    if (!showHidden && (attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                        continue;

                    var isDirectory = (attributes & FileAttributes.Directory) != 0;
                    var size = isDirectory ? -1 : ((FileInfo)info).Length;

                    entries.Add(new FileSystemEntry(
                        info.FullName, info.Name,
                        isDirectory ? EntryKind.Directory : EntryKind.File,
                        size, info.LastWriteTime, attributes));
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            return new DirectoryListing(path, entries, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            return new DirectoryListing(path, [], "Access denied.");
        }
        catch (DirectoryNotFoundException)
        {
            return new DirectoryListing(path, [], "The folder does not exist (anymore).");
        }
        catch (IOException ex)
        {
            return new DirectoryListing(path, [], ex.Message);
        }
    }

    private static List<FileSystemEntry> ListDrives()
    {
        var entries = new List<FileSystemEntry>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            string name;
            try
            {
                name = ShellHelper.GetDisplayName(drive.RootDirectory.FullName);
            }
            catch (Exception)
            {
                name = drive.Name;
            }

            entries.Add(new FileSystemEntry(
                drive.RootDirectory.FullName, name, EntryKind.Drive, -1, default, FileAttributes.Directory));
        }

        return entries;
    }

    /// <summary>Subfolders for the navigation tree; unreadable folders yield an empty list.</summary>
    public static IReadOnlyList<(string Path, string Name)> GetSubFolders(string path, bool showHidden)
    {
        if (path == KnownLocations.ThisPc)
        {
            return DriveInfo.GetDrives()
                .Select(d => (d.RootDirectory.FullName, ShellHelper.GetDisplayName(d.RootDirectory.FullName)))
                .ToList();
        }

        try
        {
            return new DirectoryInfo(path)
                .EnumerateDirectories("*", new EnumerationOptions { IgnoreInaccessible = true })
                .Where(d => showHidden || (d.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                .OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(d => (d.FullName, d.Name))
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    public static bool HasSubFolders(string path, bool showHidden)
    {
        if (path == KnownLocations.ThisPc)
            return true;

        try
        {
            return new DirectoryInfo(path)
                .EnumerateDirectories("*", new EnumerationOptions { IgnoreInaccessible = true })
                .Any(d => showHidden || (d.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Parent folder for the "Up" command, or <c>null</c> at the root.</summary>
    public static string? GetParent(string path)
    {
        if (path == KnownLocations.ThisPc)
            return null;

        try
        {
            // At a volume root the parent is "This PC", not another folder.
            if (string.Equals(path, Path.GetPathRoot(path), StringComparison.OrdinalIgnoreCase))
                return KnownLocations.ThisPc;

            var parent = Path.GetDirectoryName(path.TrimEnd(Path.DirectorySeparatorChar));
            return string.IsNullOrEmpty(parent) ? KnownLocations.ThisPc : parent;
        }
        catch (Exception)
        {
            return KnownLocations.ThisPc;
        }
    }
}
