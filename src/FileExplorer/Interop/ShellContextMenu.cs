using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace FileExplorer.Interop;

/// <summary>
/// Shows the real Windows shell context menu &mdash; including third party entries such as
/// 7-Zip, Git or "Open with" &mdash; for a selection of paths or for a folder background.
/// </summary>
/// <remarks>
/// Owner-drawn submenus (the "New" and "Open with" cascades) only populate if the shell
/// extension receives WM_INITMENUPOPUP and friends, so the owner window is hooked for the
/// lifetime of the popup.
/// </remarks>
internal sealed class ShellContextMenu : IDisposable
{
    // Shell command ids start high so the app's own entries can occupy the low range.
    private const uint IdCmdFirst = 0x1000;
    private const uint IdCmdLast = 0x7FFF;

    private const uint IdPaste = 1;
    private const uint IdRefresh = 2;
    private const uint IdPin = 3;
    private const uint IdUnpin = 4;

    /// <summary>Verbs reported for the app's own entries, which the shell knows nothing about.</summary>
    internal const string PasteVerb = "app:paste";

    internal const string RefreshVerb = "app:refresh";

    internal const string PinVerb = "app:pin";

    internal const string UnpinVerb = "app:unpin";

    private readonly HwndSource _source;
    private readonly HwndSourceHook _hook;

    /// <summary>Verbs the caller carries out itself; they are reported but not invoked.</summary>
    private IReadOnlyCollection<string> _appVerbs = [];

    private IContextMenu? _menu;
    private IContextMenu2? _menu2;
    private IContextMenu3? _menu3;
    private IntPtr _hMenu;
    private bool _hooked;

    private ShellContextMenu(HwndSource source)
    {
        _source = source;
        _hook = WndProc;
    }

    /// <summary>
    /// Shows the context menu for the given paths at a screen position and returns the verb
    /// that was invoked (e.g. <c>delete</c>, <c>paste</c>, <c>rename</c>), or <c>null</c>
    /// when the user dismissed the menu.
    /// </summary>
    /// <param name="pinState">
    /// <c>true</c> adds "Pin to Quick access", <c>false</c> adds "Unpin from Quick access",
    /// <c>null</c> adds neither - the shell knows nothing about this app's sidebar.
    /// </param>
    /// <param name="appVerbs">
    /// Verbs the caller performs itself (e.g. "open" on a folder, which the shell would open in
    /// a new Explorer window); they are returned without being passed to the shell.
    /// </param>
    internal static string? ShowForItems(
        HwndSource source, IReadOnlyList<string> paths, int screenX, int screenY, bool extended,
        bool? pinState = null, IReadOnlyCollection<string>? appVerbs = null)
    {
        if (paths.Count == 0)
            return null;

        using var instance = new ShellContextMenu(source) { _appVerbs = appVerbs ?? [] };
        return instance.Run(() => instance.CreateItemMenu(paths), screenX, screenY,
            CMF.Normal | CMF.CanRename | (extended ? CMF.ExtendedVerbs : CMF.Normal),
            ownItems: menu => AppendPinItem(menu, pinState));
    }

    /// <summary>
    /// Shows the menu Explorer offers when right-clicking empty space inside a folder.
    /// </summary>
    /// <remarks>
    /// The folder's own IContextMenu only contributes shell extensions, "New" and
    /// "Properties". In Explorer, Paste and Refresh come from the folder *view*
    /// (CDefView), which this app does not host, so they are prepended here.
    /// </remarks>
    internal static string? ShowForFolderBackground(
        HwndSource source, string folderPath, int screenX, int screenY, bool extended, bool canPaste, bool? pinState)
    {
        using var instance = new ShellContextMenu(source);
        return instance.Run(() => instance.CreateBackgroundMenu(folderPath), screenX, screenY,
            CMF.Normal | (extended ? CMF.ExtendedVerbs : CMF.Normal),
            ownItems: menu =>
            {
                AppendViewItems(menu, canPaste);
                AppendPinItem(menu, pinState);
            });
    }

    private string? Run(Func<bool> build, int screenX, int screenY, CMF flags, Action<IntPtr>? ownItems = null)
    {
        if (!build() || _menu is null)
            return null;

        _hMenu = NativeMethods.CreatePopupMenu();
        if (_hMenu == IntPtr.Zero)
            return null;

        ownItems?.Invoke(_hMenu);

        if (NativeMethods.GetMenuItemCount(_hMenu) > 0)
            NativeMethods.AppendMenu(_hMenu, NativeMethods.MF_SEPARATOR, UIntPtr.Zero, null);

        var shellIndex = (uint)NativeMethods.GetMenuItemCount(_hMenu);
        var hr = _menu.QueryContextMenu(_hMenu, shellIndex, IdCmdFirst, IdCmdLast, flags);
        if (hr < 0)
            return null;

        _menu2 = _menu as IContextMenu2;
        _menu3 = _menu as IContextMenu3;

        _source.AddHook(_hook);
        _hooked = true;

        uint selected;
        try
        {
            selected = NativeMethods.TrackPopupMenuEx(
                _hMenu,
                NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_RIGHTBUTTON | NativeMethods.TPM_LEFTALIGN,
                screenX, screenY, _source.Handle, IntPtr.Zero);
        }
        finally
        {
            _source.RemoveHook(_hook);
            _hooked = false;
        }

        return selected switch
        {
            0 => null,
            IdPaste => PasteVerb,
            IdRefresh => RefreshVerb,
            IdPin => PinVerb,
            IdUnpin => UnpinVerb,
            _ => InvokeShellCommand(selected - IdCmdFirst, screenX, screenY),
        };
    }

    private string? InvokeShellCommand(uint commandId, int screenX, int screenY)
    {
        var verb = GetVerb(commandId);
        if (verb is not null && _appVerbs.Contains(verb, StringComparer.OrdinalIgnoreCase))
            return verb;

        Invoke(commandId, screenX, screenY);
        return verb;
    }

    /// <summary>Paste and Refresh, which in Explorer come from the folder view.</summary>
    private static void AppendViewItems(IntPtr hMenu, bool canPaste)
    {
        NativeMethods.AppendMenu(
            hMenu,
            NativeMethods.MF_STRING | (canPaste ? 0 : NativeMethods.MF_GRAYED),
            (UIntPtr)IdPaste,
            "Paste\tCtrl+V");

        NativeMethods.AppendMenu(hMenu, NativeMethods.MF_STRING, (UIntPtr)IdRefresh, "Refresh\tF5");
    }

    /// <summary>The sidebar entry, which only this app knows about.</summary>
    private static void AppendPinItem(IntPtr hMenu, bool? pinState)
    {
        if (pinState is not { } pin)
            return;

        NativeMethods.AppendMenu(
            hMenu,
            NativeMethods.MF_STRING,
            (UIntPtr)(pin ? IdPin : IdUnpin),
            pin ? "Pin to Quick access\tCtrl+D" : "Unpin from Quick access");
    }

    private bool CreateItemMenu(IReadOnlyList<string> paths)
    {
        var pidls = new IntPtr[paths.Count];
        try
        {
            for (var i = 0; i < paths.Count; i++)
            {
                pidls[i] = ShellHelper.CreatePidl(paths[i]);
                if (pidls[i] == IntPtr.Zero)
                    return false;
            }

            NativeMethods.SHCreateShellItemArrayFromIDLists((uint)pidls.Length, pidls, out var array);
            try
            {
                array.BindToHandler(null, ShellGuids.BHID_SFUIObject, ShellGuids.IContextMenu, out var ptr);
                _menu = (IContextMenu)Marshal.GetObjectForIUnknown(ptr);
                Marshal.Release(ptr);
                return true;
            }
            finally
            {
                Marshal.ReleaseComObject(array);
            }
        }
        catch (Exception ex) when (ShellHelper.IsShellFailure(ex))
        {
            return false;
        }
        finally
        {
            foreach (var pidl in pidls)
            {
                if (pidl != IntPtr.Zero)
                    NativeMethods.ILFree(pidl);
            }
        }
    }

    private bool CreateBackgroundMenu(string folderPath)
    {
        var item = ShellHelper.CreateItem(folderPath);
        if (item is null)
            return false;

        try
        {
            item.BindToHandler(null, ShellGuids.BHID_SFObject, ShellGuids.IShellFolder, out var folderPtr);
            var folder = (IShellFolder)Marshal.GetObjectForIUnknown(folderPtr);
            Marshal.Release(folderPtr);

            try
            {
                folder.CreateViewObject(_source.Handle, ShellGuids.IContextMenu, out var menuPtr);
                _menu = (IContextMenu)Marshal.GetObjectForIUnknown(menuPtr);
                Marshal.Release(menuPtr);
                return true;
            }
            finally
            {
                Marshal.ReleaseComObject(folder);
            }
        }
        catch (Exception ex) when (ShellHelper.IsShellFailure(ex))
        {
            return false;
        }
        finally
        {
            Marshal.ReleaseComObject(item);
        }
    }

    private string? GetVerb(uint commandId)
    {
        if (_menu is null)
            return null;

        var buffer = Marshal.AllocCoTaskMem(512);
        try
        {
            var hr = _menu.GetCommandString((IntPtr)commandId, GCS.VerbW, IntPtr.Zero, buffer, 256);
            return hr < 0 ? null : Marshal.PtrToStringUni(buffer);
        }
        catch (Exception ex) when (ShellHelper.IsShellFailure(ex))
        {
            return null;
        }
        finally
        {
            Marshal.FreeCoTaskMem(buffer);
        }
    }

    private void Invoke(uint commandId, int screenX, int screenY)
    {
        if (_menu is null)
            return;

        var info = new CMINVOKECOMMANDINFOEX
        {
            cbSize = Marshal.SizeOf<CMINVOKECOMMANDINFOEX>(),
            fMask = NativeMethods.CMIC_MASK_UNICODE | NativeMethods.CMIC_MASK_PTINVOKE,
            hwnd = _source.Handle,
            lpVerb = (IntPtr)(commandId & 0xFFFF),
            lpVerbW = (IntPtr)(commandId & 0xFFFF),
            nShow = 1, // SW_SHOWNORMAL
            ptInvoke = new NativePoint { X = screenX, Y = screenY },
        };

        try
        {
            _menu.InvokeCommand(ref info);
        }
        catch (Exception ex) when (ShellHelper.IsShellFailure(ex))
        {
            // A refusing shell extension must not take the app down.
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        var message = (uint)msg;
        if (message is not (NativeMethods.WM_INITMENUPOPUP or NativeMethods.WM_DRAWITEM
            or NativeMethods.WM_MEASUREITEM or NativeMethods.WM_MENUCHAR))
        {
            return IntPtr.Zero;
        }

        if (_menu3 is not null)
        {
            if (_menu3.HandleMenuMsg2(message, wParam, lParam, out var result) == 0)
            {
                handled = true;
                return result;
            }
        }
        else if (_menu2 is not null)
        {
            if (_menu2.HandleMenuMsg(message, wParam, lParam) == 0)
            {
                handled = true;
                return IntPtr.Zero;
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_hooked)
            _source.RemoveHook(_hook);

        if (_hMenu != IntPtr.Zero)
        {
            NativeMethods.DestroyMenu(_hMenu);
            _hMenu = IntPtr.Zero;
        }

        if (_menu is not null)
        {
            Marshal.ReleaseComObject(_menu);
            _menu = null;
            _menu2 = null;
            _menu3 = null;
        }
    }
}
