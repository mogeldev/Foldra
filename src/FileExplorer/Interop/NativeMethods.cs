using System.Runtime.InteropServices;

namespace FileExplorer.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct NativeSize
{
    public int Width;
    public int Height;

    public NativeSize(int width, int height)
    {
        Width = width;
        Height = height;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativePoint
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeRect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MONITORINFO
{
    public int cbSize;
    public NativeRect rcMonitor;
    public NativeRect rcWork;
    public uint dwFlags;
}

[Flags]
internal enum CMF : uint
{
    Normal = 0x00000000,
    DefaultOnly = 0x00000001,
    VerbsOnly = 0x00000002,
    Explore = 0x00000004,
    NoVerbs = 0x00000008,
    CanRename = 0x00000010,
    NoDefault = 0x00000020,
    IncludeStatic = 0x00000040,
    ItemMenu = 0x00000080,
    ExtendedVerbs = 0x00000100,
    DisabledVerbs = 0x00000200,
    AsyncVerbState = 0x00000400,
    OptimizeForInvoke = 0x00000800,
    SyncCascadeMenu = 0x00001000,
    DoNotPickDefault = 0x00002000,
}

internal enum GCS : uint
{
    VerbA = 0x00000000,
    HelpTextA = 0x00000001,
    ValidateA = 0x00000002,
    VerbW = 0x00000004,
    HelpTextW = 0x00000005,
    ValidateW = 0x00000006,
}

[Flags]
internal enum FOF : uint
{
    MultiDestFiles = 0x0001,
    ConfirmMouse = 0x0002,
    Silent = 0x0004,
    RenameOnCollision = 0x0008,
    NoConfirmation = 0x0010,
    WantMappingHandle = 0x0020,
    AllowUndo = 0x0040,
    FilesOnly = 0x0080,
    SimpleProgress = 0x0100,
    NoConfirmMkDir = 0x0200,
    NoErrorUI = 0x0400,
    NoCopySecurityAttribs = 0x0800,
    NoRecursion = 0x1000,
    NoConnectedElements = 0x2000,
    WantNukeWarning = 0x4000,
    NoSkipJunctions = 0x00010000,
    PreferHardLink = 0x00020000,
    ShowElevationPrompt = 0x00040000,
    RecycleOnDelete = 0x00080000,
    EarlyFailure = 0x00100000,
    PreserveFileExtensions = 0x00200000,
    KeepNewerFile = 0x00400000,
    NoCopyHooks = 0x00800000,
    NoMinimizeBox = 0x01000000,
    MoveAclsAcrossVolumes = 0x02000000,
    DontDisplaySourcePath = 0x04000000,
    DontDisplayDestPath = 0x08000000,
    RequireElevation = 0x10000000,
    AddUndoRecord = 0x20000000,
    CopyAsDownload = 0x40000000,
    DontDisplayLocations = 0x80000000,
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
internal struct CMINVOKECOMMANDINFOEX
{
    public int cbSize;
    public uint fMask;
    public IntPtr hwnd;
    public IntPtr lpVerb;
    [MarshalAs(UnmanagedType.LPStr)] public string? lpParameters;
    [MarshalAs(UnmanagedType.LPStr)] public string? lpDirectory;
    public int nShow;
    public uint dwHotKey;
    public IntPtr hIcon;
    [MarshalAs(UnmanagedType.LPStr)] public string? lpTitle;
    public IntPtr lpVerbW;
    [MarshalAs(UnmanagedType.LPWStr)] public string? lpParametersW;
    [MarshalAs(UnmanagedType.LPWStr)] public string? lpDirectoryW;
    [MarshalAs(UnmanagedType.LPWStr)] public string? lpTitleW;
    public NativePoint ptInvoke;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct SHFILEINFO
{
    public IntPtr hIcon;
    public int iIcon;
    public uint dwAttributes;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
}

[Flags]
internal enum SHGFI : uint
{
    Icon = 0x000000100,
    DisplayName = 0x000000200,
    TypeName = 0x000000400,
    Attributes = 0x000000800,
    IconLocation = 0x000001000,
    ExeType = 0x000002000,
    SysIconIndex = 0x000004000,
    LinkOverlay = 0x000008000,
    Selected = 0x000010000,
    AttrSpecified = 0x000020000,
    LargeIcon = 0x000000000,
    SmallIcon = 0x000000001,
    OpenIcon = 0x000000002,
    ShellIconSize = 0x000000004,
    PIDL = 0x000000008,
    UseFileAttributes = 0x000000010,
    AddOverlays = 0x000000020,
    OverlayIndex = 0x000000040,
}

internal static class ShellGuids
{
    internal static readonly Guid IShellItem = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");
    internal static readonly Guid IShellItemArray = new("b63ea76d-1f85-456f-a19c-48159efa858b");
    internal static readonly Guid IShellItemImageFactory = new("bcc18b79-ba16-442f-80c4-8a59c30c463b");
    internal static readonly Guid IContextMenu = new("000214e4-0000-0000-c000-000000000046");
    internal static readonly Guid IShellFolder = new("000214e6-0000-0000-c000-000000000046");
    internal static readonly Guid IFileOperation = new("947aab5f-0a5c-4c13-b4d6-4bf7836fc9f8");

    /// <summary>BHID_SFUIObject &mdash; binds a shell item (array) to its UI object, e.g. IContextMenu.</summary>
    internal static readonly Guid BHID_SFUIObject = new("3981e225-f559-11d3-8e3a-00c04f6837d5");

    /// <summary>BHID_SFObject &mdash; binds a shell item to IShellFolder.</summary>
    internal static readonly Guid BHID_SFObject = new("3981e224-f559-11d3-8e3a-00c04f6837d5");

    internal static readonly Guid CLSID_FileOperation = new("3ad05575-8857-4850-9277-11b85bdb8e09");
}

internal static class NativeMethods
{
    internal const uint MF_STRING = 0x00000000;
    internal const uint MF_GRAYED = 0x00000001;
    internal const uint MF_SEPARATOR = 0x00000800;

    internal const uint TPM_LEFTALIGN = 0x0000;
    internal const uint TPM_RETURNCMD = 0x0100;
    internal const uint TPM_RIGHTBUTTON = 0x0002;

    internal const uint WM_INITMENUPOPUP = 0x0117;
    internal const uint WM_DRAWITEM = 0x002B;
    internal const uint WM_MEASUREITEM = 0x002C;
    internal const uint WM_MENUCHAR = 0x0120;

    internal const uint CMIC_MASK_UNICODE = 0x00004000;
    internal const uint CMIC_MASK_PTINVOKE = 0x20000000;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    internal static extern void SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        in Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    internal static extern void SHParseDisplayName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszName,
        IntPtr pbc,
        out IntPtr ppidl,
        uint sfgaoIn,
        out uint psfgaoOut);

    [DllImport("shell32.dll", PreserveSig = false)]
    internal static extern void SHCreateShellItemArrayFromIDLists(
        uint cidl,
        [In, MarshalAs(UnmanagedType.LPArray)] IntPtr[] rgpidl,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemArray ppsiItemArray);

    [DllImport("shell32.dll", PreserveSig = false)]
    internal static extern void SHBindToParent(
        IntPtr pidl,
        in Guid riid,
        out IntPtr ppv,
        out IntPtr ppidlLast);

    [DllImport("shell32.dll", PreserveSig = false)]
    internal static extern void SHGetDesktopFolder(
        [MarshalAs(UnmanagedType.Interface)] out IShellFolder ppshf);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbFileInfo,
        SHGFI uFlags);

    [DllImport("shell32.dll")]
    internal static extern void ILFree(IntPtr pidl);

    [DllImport("shell32.dll", PreserveSig = false)]
    internal static extern void SHOpenFolderAndSelectItems(
        IntPtr pidlFolder,
        uint cidl,
        [In, MarshalAs(UnmanagedType.LPArray)] IntPtr[]? apidl,
        uint dwFlags);

    [DllImport("ole32.dll")]
    internal static extern int CoCreateInstance(
        in Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        in Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out object ppv);

    internal const uint MONITOR_DEFAULTTONEAREST = 2;

    [DllImport("user32.dll")]
    internal static extern IntPtr MonitorFromRect(in NativeRect lprc, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    internal static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    internal static extern uint TrackPopupMenuEx(
        IntPtr hMenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

    [DllImport("user32.dll")]
    internal static extern int GetMenuItemCount(IntPtr hMenu);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "AppendMenuW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AppendMenu(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteObjectGdi(IntPtr hObject);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>Only recognized from Windows 11 onward; ignored (HRESULT error, no throw) on
    /// Windows 10, which has no corner preference to set in the first place.</summary>
    internal const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    internal const int DWMWCP_DEFAULT = 0;

    /// <summary>Tells DWM to paint the non-client title bar dark. 20 is the value shipped from
    /// Windows 10 2004 onward and all of Windows 11; 19 is the older, pre-2004 value some
    /// Windows 10 builds still need. Both are harmless no-ops where unsupported.</summary>
    internal const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    internal const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;

    [DllImport("dwmapi.dll")]
    internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    /// <summary>Returns the current DWM colorization color (0xAARRGGBB) - the same accent
    /// color Windows uses for title bars and highlights. Works unchanged on Windows 10 and 11.</summary>
    [DllImport("dwmapi.dll", PreserveSig = false)]
    internal static extern void DwmGetColorizationColor(out uint pcrColorization, [MarshalAs(UnmanagedType.Bool)] out bool pfOpaqueBlend);

    /// <summary>Posted to every top-level window when the DWM colorization/accent color changes.</summary>
    internal const int WM_DWMCOLORIZATIONCOLORCHANGED = 0x0320;

    /// <summary>Posted (among other things) when the light/dark app theme changes; lParam names
    /// the setting, e.g. "ImmersiveColorSet".</summary>
    internal const int WM_SETTINGCHANGE = 0x001A;
}
