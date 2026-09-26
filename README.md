# Foldra

A tabbed file explorer for Windows 10 and 11, built on WPF (.NET 9) with no third-party UI libraries.

As much of the logic as possible is deliberately left to the Windows shell: context menus,
file operations, icons, thumbnails and search come from the system, not from custom code.

> **Alpha:** features and behavior may still change.

> Not a Microsoft product and not affiliated with Microsoft. The interface is modeled on
> Windows File Explorer but contains no Microsoft artwork.

## Download

A self-contained single-file build is attached to each
[release](https://github.com/mogeldev/Foldra/releases). It runs without an installed .NET runtime.

## Build and run

```bash
dotnet build FileExplorer.sln
```

The executable ends up in `src/FileExplorer/bin/Debug/net9.0-windows/Foldra.exe`.

Self-contained single file (runs without an installed .NET runtime, ~60 MB):

```bash
dotnet publish src/FileExplorer/FileExplorer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -o publish
```

## Features

**Tabs** — Ctrl+T opens, Ctrl+W closes, middle click closes, Ctrl+1…9 switches.
Tabs can be reordered by drag and drop. Open tabs, including their history, are saved on
exit and restored on the next start. Middle-clicking a folder in the list, in Quick Access
or in the folder tree opens it in a new tab.

**Navigation** — back/forward/up/refresh, a clickable breadcrumb bar, and an editable
address bar (click the empty area or press Ctrl+L). Quick Access on the left, the folder
tree below it.

**Quick Access** — right-click a folder (or the empty area of the list) to pin or unpin it;
Ctrl+D and the ribbon button pin the selected folder, or the open one if nothing is selected.

**Views** — details, list, tiles, medium / large / extra large icons.
Sort by column header or ribbon, folders always before files, natural sort order
("File 10" after "File 2") via `StrCmpLogicalW`.

**File operations** — copy, cut, paste, rename (inline, F2), delete
(Del = recycle bin, Shift+Del = permanently), drag and drop, new folder.
Everything goes through `IFileOperation`, so the real Windows progress dialog, conflict
prompts, recycle bin and UAC elevation apply. Drag and drop follows Explorer: move within a
drive, copy across drives, Shift forces a move, Ctrl a copy.

**Context menu** — the real shell context menu via `IContextMenu` / `IContextMenu3`,
including third-party entries (7-Zip, Git, "Open with", Properties). Shift+right-click shows
the extended verbs. Works for items and for the folder background ("New >"). The app's own
entries (paste, refresh, Quick Access) are prepended, because in Explorer they come from the
folder view, which this app does not host.

**Search** — Ctrl+F. Uses the Windows Search index (`Search.CollatorDSO`) when the folder is
indexed, otherwise a recursive file system search. Results stream in as they are found.

**Clipboard** — CF_HDROP plus `Preferred DropEffect`, so copy and paste work in both
directions with the real Explorer.

**Theme** — light/dark mode and the accent color follow the Windows settings, including when
they change while the app is running.

## Project structure

| Folder | Contents |
| --- | --- |
| `Interop/` | Shell COM interfaces, context menu, `IFileOperation`, icon/thumbnail provider |
| `Models/` | `FileSystemEntry`, view and session models |
| `Services/` | Directory enumeration, search, clipboard, session, launching files |
| `ViewModels/` | `MainViewModel` (tabs, commands), `ExplorerTabViewModel` (one tab) |
| `Views/` | `MainWindow` (frame, ribbon), `ExplorerView` (file list), styles, themes, icons |

## Platform notes

* The window keeps the **system title bar** and frame, so it gets the native look of the
  running OS: rounded corners on Windows 11, square ones on Windows 10. In dark mode the title
  bar is drawn dark as well.
* **Colors**: the base palettes live in `Views/Theme.Light.xaml` and `Theme.Dark.xaml`;
  selection, hover and the "Datei" ribbon tab are derived from the Windows accent color.
* **Icons**: the ribbon icons are custom vectors (`Views/Icons.xaml`), the remaining glyphs come
  from *Segoe MDL2 Assets*, and the window and executable icon from `Assets/Foldra.ico`.
* The **new** Windows 11 context menu only exists on Windows 11. Elsewhere the classic shell
  menu appears, which offers the same functionality.
* The **Windows Search** query fails or returns nothing when the folder is not indexed
  (the default on Server SKUs); the app then falls back to the file system search.

## Diagnostics

With `FOLDRA_TRACE=1` the app writes a startup trace to `%APPDATA%\Foldra\trace.log`.
Unhandled errors always go to `%APPDATA%\Foldra\error.log`, the session to `session.json`
next to it.

## Icon credits

`src/FileExplorer/Assets/Foldra.ico` was made from the clipart
["yellow folder icon"](https://openclipart.org/detail/354572/yellow-folder-icon) from Openclipart,
licensed **CC0 1.0 (public domain)** — no attribution required, commercial use allowed.
The original SVG is in `src/FileExplorer/Assets/folder.svg`; the ICO file contains the sizes
16/24/32/48/64/128/256 px.

## License

Copyright (C) 2026 mogeldev

Foldra is free software: you can redistribute it and/or modify it under the terms of the
[GNU General Public License, version 3](LICENSE), as published by the Free Software Foundation.

Foldra is distributed in the hope that it will be useful, but **without any warranty**; without
even the implied warranty of merchantability or fitness for a particular purpose. See the
[LICENSE](LICENSE) file for details.
