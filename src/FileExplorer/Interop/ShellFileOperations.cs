using System.Runtime.InteropServices;

namespace FileExplorer.Interop;

/// <summary>
/// File operations performed by the shell itself, so the user gets the familiar Windows
/// progress dialog, conflict prompts ("Replace file?"), elevation prompts and recycle bin
/// behaviour instead of a home-grown copy loop.
/// </summary>
internal static class ShellFileOperations
{
    private const uint CLSCTX_INPROC_SERVER = 1;

    private const FOF DefaultFlags =
        FOF.AllowUndo | FOF.NoConfirmMkDir | FOF.ShowElevationPrompt;

    private static IFileOperation CreateOperation(IntPtr ownerHwnd, FOF flags)
    {
        var hr = NativeMethods.CoCreateInstance(
            ShellGuids.CLSID_FileOperation, IntPtr.Zero, CLSCTX_INPROC_SERVER,
            ShellGuids.IFileOperation, out var raw);

        if (hr < 0 || raw is not IFileOperation operation)
            throw new COMException("IFileOperation could not be created.", hr);

        operation.SetOwnerWindow(ownerHwnd);
        operation.SetOperationFlags(flags);
        return operation;
    }

    /// <summary>
    /// Runs the queued operations. A cancel in the progress or conflict dialog, or a failure the
    /// shell has already reported to the user (invalid name, locked file), surfaces as an error
    /// HRESULT; that is a normal outcome here, not an exception worth an error dialog.
    /// </summary>
    private static bool Perform(IFileOperation operation)
    {
        try
        {
            operation.PerformOperations();
            operation.GetAnyOperationsAborted(out var aborted);
            return !aborted;
        }
        catch (Exception ex) when (ShellHelper.IsShellFailure(ex))
        {
            return false;
        }
    }

    internal static bool Copy(IntPtr owner, IReadOnlyList<string> sources, string destinationFolder)
        => Transfer(owner, sources, destinationFolder, move: false);

    internal static bool Move(IntPtr owner, IReadOnlyList<string> sources, string destinationFolder)
        => Transfer(owner, sources, destinationFolder, move: true);

    private static bool Transfer(IntPtr owner, IReadOnlyList<string> sources, string destinationFolder, bool move)
    {
        if (sources.Count == 0)
            return false;

        var destination = ShellHelper.CreateItem(destinationFolder);
        if (destination is null)
            return false;

        var operation = CreateOperation(owner, DefaultFlags);
        try
        {
            foreach (var source in sources)
            {
                var item = ShellHelper.CreateItem(source);
                if (item is null)
                    continue;

                try
                {
                    if (move)
                        operation.MoveItem(item, destination, null, null);
                    else
                        operation.CopyItem(item, destination, null, null);
                }
                finally
                {
                    Marshal.ReleaseComObject(item);
                }
            }

            return Perform(operation);
        }
        catch (Exception ex) when (ShellHelper.IsShellFailure(ex))
        {
            return false;
        }
        finally
        {
            Marshal.ReleaseComObject(operation);
            Marshal.ReleaseComObject(destination);
        }
    }

    /// <summary>Deletes to the recycle bin, or permanently when <paramref name="permanent"/> is set.</summary>
    internal static bool Delete(IntPtr owner, IReadOnlyList<string> paths, bool permanent)
    {
        if (paths.Count == 0)
            return false;

        var flags = permanent
            ? FOF.WantNukeWarning | FOF.ShowElevationPrompt
            : DefaultFlags | FOF.RecycleOnDelete;

        var operation = CreateOperation(owner, flags);
        try
        {
            foreach (var path in paths)
            {
                var item = ShellHelper.CreateItem(path);
                if (item is null)
                    continue;

                try
                {
                    operation.DeleteItem(item, null);
                }
                finally
                {
                    Marshal.ReleaseComObject(item);
                }
            }

            return Perform(operation);
        }
        catch (Exception ex) when (ShellHelper.IsShellFailure(ex))
        {
            return false;
        }
        finally
        {
            Marshal.ReleaseComObject(operation);
        }
    }

    internal static bool Rename(IntPtr owner, string path, string newName)
    {
        var item = ShellHelper.CreateItem(path);
        if (item is null)
            return false;

        var operation = CreateOperation(owner, DefaultFlags);
        try
        {
            operation.RenameItem(item, newName, null);
            return Perform(operation);
        }
        catch (Exception ex) when (ShellHelper.IsShellFailure(ex))
        {
            return false;
        }
        finally
        {
            Marshal.ReleaseComObject(operation);
            Marshal.ReleaseComObject(item);
        }
    }

    /// <summary>Creates a folder or an empty file and returns its full path.</summary>
    internal static string? CreateNewItem(IntPtr owner, string parentFolder, string name, bool isFolder)
    {
        var parent = ShellHelper.CreateItem(parentFolder);
        if (parent is null)
            return null;

        var sink = new NewItemSink();
        var operation = CreateOperation(owner, DefaultFlags);
        try
        {
            const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
            const uint FILE_ATTRIBUTE_NORMAL = 0x80;

            operation.NewItem(parent, isFolder ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL, name, null, sink);
            Perform(operation);
            return sink.CreatedPath;
        }
        catch (Exception ex) when (ShellHelper.IsShellFailure(ex))
        {
            return null;
        }
        finally
        {
            Marshal.ReleaseComObject(operation);
            Marshal.ReleaseComObject(parent);
        }
    }

    /// <summary>Captures the path the shell actually used, which may differ if the name collided.</summary>
    private sealed class NewItemSink : IFileOperationProgressSink
    {
        internal string? CreatedPath { get; private set; }

        public void PostNewItem(uint dwFlags, IShellItem psiDestinationFolder, string? pszNewName,
            string? pszTemplateName, uint dwFileAttributes, int hrNew, IShellItem? psiNewItem)
        {
            if (hrNew < 0 || psiNewItem is null)
                return;

            psiNewItem.GetDisplayName(SIGDN.FileSysPath, out var ptr);
            try
            {
                CreatedPath = Marshal.PtrToStringUni(ptr);
            }
            finally
            {
                Marshal.FreeCoTaskMem(ptr);
            }
        }

        public void StartOperations() { }
        public void FinishOperations(int hrResult) { }
        public void PreRenameItem(uint dwFlags, IShellItem psiItem, string? pszNewName) { }
        public void PostRenameItem(uint dwFlags, IShellItem psiItem, string? pszNewName, int hrRename, IShellItem? psiNewlyCreated) { }
        public void PreMoveItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, string? pszNewName) { }
        public void PostMoveItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, string? pszNewName, int hrMove, IShellItem? psiNewlyCreated) { }
        public void PreCopyItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, string? pszNewName) { }
        public void PostCopyItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, string? pszNewName, int hrCopy, IShellItem? psiNewlyCreated) { }
        public void PreDeleteItem(uint dwFlags, IShellItem psiItem) { }
        public void PostDeleteItem(uint dwFlags, IShellItem psiItem, int hrDelete, IShellItem? psiNewlyCreated) { }
        public void PreNewItem(uint dwFlags, IShellItem psiDestinationFolder, string? pszNewName) { }
        public void UpdateProgress(uint iWorkTotal, uint iWorkSoFar) { }
        public void ResetTimer() { }
        public void PauseTimer() { }
        public void ResumeTimer() { }
    }
}
