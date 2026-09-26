using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace FileExplorer.Interop;

internal enum SIGDN : uint
{
    NormalDisplay = 0x00000000,
    ParentRelativeParsing = 0x80018001,
    DesktopAbsoluteParsing = 0x80028000,
    ParentRelativeEditing = 0x80031001,
    DesktopAbsoluteEditing = 0x8004c000,
    FileSysPath = 0x80058000,
    Url = 0x80068000,
    ParentRelativeForAddressBar = 0x8007c001,
    ParentRelative = 0x80080001,
}

[Flags]
internal enum SIIGBF
{
    ResizeToFit = 0x00,
    BiggerSizeOk = 0x01,
    MemoryOnly = 0x02,
    IconOnly = 0x04,
    ThumbnailOnly = 0x08,
    InCacheOnly = 0x10,
    ScaleUp = 0x100,
}

[ComImport]
[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItem
{
    void BindToHandler(IBindCtx? pbc, in Guid bhid, in Guid riid, out IntPtr ppv);
    void GetParent(out IShellItem ppsi);
    void GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
    void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
    void Compare(IShellItem psi, uint hint, out int piOrder);
}

[ComImport]
[Guid("b63ea76d-1f85-456f-a19c-48159efa858b")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItemArray
{
    void BindToHandler(IBindCtx? pbc, in Guid bhid, in Guid riid, out IntPtr ppvOut);
    void GetPropertyStore(int flags, in Guid riid, out IntPtr ppv);
    void GetPropertyDescriptionList(IntPtr keyType, in Guid riid, out IntPtr ppv);
    void GetAttributes(int attribFlags, uint sfgaoMask, out uint psfgaoAttribs);
    void GetCount(out uint pdwNumItems);
    void GetItemAt(uint dwIndex, out IShellItem ppsi);
    void EnumItems(out IntPtr ppenumShellItems);
}

[ComImport]
[Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItemImageFactory
{
    [PreserveSig]
    int GetImage(NativeSize size, SIIGBF flags, out IntPtr phbm);
}

[ComImport]
[Guid("000214e4-0000-0000-c000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IContextMenu
{
    [PreserveSig]
    int QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, CMF uFlags);

    [PreserveSig]
    int InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);

    [PreserveSig]
    int GetCommandString(IntPtr idCmd, GCS uType, IntPtr pReserved, IntPtr pszName, uint cchMax);
}

[ComImport]
[Guid("000214f4-0000-0000-c000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IContextMenu2
{
    [PreserveSig]
    int QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, CMF uFlags);

    [PreserveSig]
    int InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);

    [PreserveSig]
    int GetCommandString(IntPtr idCmd, GCS uType, IntPtr pReserved, IntPtr pszName, uint cchMax);

    [PreserveSig]
    int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
}

[ComImport]
[Guid("bcfce0a0-ec17-11d0-8d10-00a0c90f2719")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IContextMenu3
{
    [PreserveSig]
    int QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, CMF uFlags);

    [PreserveSig]
    int InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);

    [PreserveSig]
    int GetCommandString(IntPtr idCmd, GCS uType, IntPtr pReserved, IntPtr pszName, uint cchMax);

    [PreserveSig]
    int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);

    [PreserveSig]
    int HandleMenuMsg2(uint uMsg, IntPtr wParam, IntPtr lParam, out IntPtr plResult);
}

[ComImport]
[Guid("000214e6-0000-0000-c000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellFolder
{
    void ParseDisplayName(IntPtr hwnd, IntPtr pbc, [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName,
        ref uint pchEaten, out IntPtr ppidl, ref uint pdwAttributes);

    void EnumObjects(IntPtr hwnd, int grfFlags, out IntPtr ppenumIDList);

    void BindToObject(IntPtr pidl, IntPtr pbc, in Guid riid, out IntPtr ppv);

    void BindToStorage(IntPtr pidl, IntPtr pbc, in Guid riid, out IntPtr ppv);

    [PreserveSig]
    int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);

    void CreateViewObject(IntPtr hwndOwner, in Guid riid, out IntPtr ppv);

    void GetAttributesOf(uint cidl, [In] IntPtr[] apidl, ref uint rgfInOut);

    void GetUIObjectOf(IntPtr hwndOwner, uint cidl, [In] IntPtr[] apidl, in Guid riid, IntPtr rgfReserved, out IntPtr ppv);

    void GetDisplayNameOf(IntPtr pidl, uint uFlags, IntPtr pName);

    void SetNameOf(IntPtr hwnd, IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] string pszName, uint uFlags, out IntPtr ppidlOut);
}

[ComImport]
[Guid("947aab5f-0a5c-4c13-b4d6-4bf7836fc9f8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileOperation
{
    void Advise(IFileOperationProgressSink pfops, out uint pdwCookie);
    void Unadvise(uint dwCookie);
    void SetOperationFlags(FOF dwOperationFlags);
    void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string pszMessage);
    void SetProgressDialog([MarshalAs(UnmanagedType.IUnknown)] object popd);
    void SetProperties([MarshalAs(UnmanagedType.IUnknown)] object pproparray);
    void SetOwnerWindow(IntPtr hwndParent);
    void ApplyPropertiesToItem(IShellItem psiItem);
    void ApplyPropertiesToItems([MarshalAs(UnmanagedType.IUnknown)] object punkItems);
    void RenameItem(IShellItem psiItem, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, IFileOperationProgressSink? pfopsItem);
    void RenameItems([MarshalAs(UnmanagedType.IUnknown)] object pUnkItems, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
    void MoveItem(IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName, IFileOperationProgressSink? pfopsItem);
    void MoveItems([MarshalAs(UnmanagedType.IUnknown)] object punkItems, IShellItem psiDestinationFolder);
    void CopyItem(IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? pszCopyName, IFileOperationProgressSink? pfopsItem);
    void CopyItems([MarshalAs(UnmanagedType.IUnknown)] object punkItems, IShellItem psiDestinationFolder);
    void DeleteItem(IShellItem psiItem, IFileOperationProgressSink? pfopsItem);
    void DeleteItems([MarshalAs(UnmanagedType.IUnknown)] object punkItems);
    void NewItem(IShellItem psiDestinationFolder, uint dwFileAttributes, [MarshalAs(UnmanagedType.LPWStr)] string pszName,
        [MarshalAs(UnmanagedType.LPWStr)] string? pszTemplateName, IFileOperationProgressSink? pfopsItem);
    void PerformOperations();
    void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool pfAnyOperationsAborted);
}

[ComImport]
[Guid("04b0f1a7-9490-44bc-96e1-4296a31252e2")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileOperationProgressSink
{
    void StartOperations();
    void FinishOperations(int hrResult);
    void PreRenameItem(uint dwFlags, IShellItem psiItem, [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName);
    void PostRenameItem(uint dwFlags, IShellItem psiItem, [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName, int hrRename, IShellItem? psiNewlyCreated);
    void PreMoveItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName);
    void PostMoveItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName, int hrMove, IShellItem? psiNewlyCreated);
    void PreCopyItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName);
    void PostCopyItem(uint dwFlags, IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName, int hrCopy, IShellItem? psiNewlyCreated);
    void PreDeleteItem(uint dwFlags, IShellItem psiItem);
    void PostDeleteItem(uint dwFlags, IShellItem psiItem, int hrDelete, IShellItem? psiNewlyCreated);
    void PreNewItem(uint dwFlags, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName);
    void PostNewItem(uint dwFlags, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string? pszNewName,
        [MarshalAs(UnmanagedType.LPWStr)] string? pszTemplateName, uint dwFileAttributes, int hrNew, IShellItem? psiNewItem);
    void UpdateProgress(uint iWorkTotal, uint iWorkSoFar);
    void ResetTimer();
    void PauseTimer();
    void ResumeTimer();
}
