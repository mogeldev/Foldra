using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using FileExplorer.Interop;
using FileExplorer.Models;
using FileExplorer.Services;
using FileExplorer.ViewModels;

namespace FileExplorer.Views;

public partial class ExplorerView : UserControl
{
    private Point _dragStart;
    private bool _mayDrag;

    public ExplorerView()
    {
        InitializeComponent();

        // Column header clicks drive sorting; the header is inside the GridView template,
        // so the event has to be caught at the list level.
        FileList.AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(OnColumnHeaderClick));

        // Start with the list focused so the keyboard works without clicking first.
        Loaded += (_, _) => FileList.Focus();
    }

    private ExplorerTabViewModel? Tab => DataContext as ExplorerTabViewModel;

    private MainViewModel? Main => (Window.GetWindow(this) as MainWindow)?.ViewModel;

    private HwndSource? Source => PresentationSource.FromVisual(this) as HwndSource;

    // --- Selection & activation ------------------------------------------

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        => Tab?.SetSelection(FileList.SelectedItems.Cast<FileSystemEntry>().ToList());

    private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // A double-click inside the rename box selects a word; it must not open the item.
        if (FindEntry(e.OriginalSource) is { IsRenaming: false } entry)
            Activate(entry);
    }

    private void Activate(FileSystemEntry entry)
    {
        if (entry.IsDirectory)
            _ = Tab?.NavigateAsync(entry.FullPath);
        else
            LaunchService.Open(entry.FullPath);
    }

    private void OnListMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Middle click on a folder opens it in a new tab, as in every browser.
        if (e.ChangedButton != MouseButton.Middle)
            return;

        if (FindEntry(e.OriginalSource) is { IsDirectory: true } entry)
        {
            _ = Main?.NewTabAsync(entry.FullPath);
            e.Handled = true;
        }
    }

    private static FileSystemEntry? FindEntry(object? source)
    {
        var current = source as DependencyObject;

        while (current is not null and not ListViewItem)
            current = VisualTreeHelper.GetParent(current);

        return (current as ListViewItem)?.DataContext as FileSystemEntry;
    }

    // --- Keyboard ---------------------------------------------------------

    private void OnListKeyDown(object sender, KeyEventArgs e)
    {
        if (Tab is null || Main is null)
            return;

        // The rename TextBox handles its own keys (Enter/Escape); this handler runs
        // first via PreviewKeyDown, so it must not steal Enter from it.
        if (e.OriginalSource is TextBox)
            return;

        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

        switch (e.Key)
        {
            case Key.Enter when FileList.SelectedItem is FileSystemEntry entry:
                Activate(entry);
                e.Handled = true;
                break;

            case Key.Back:
                _ = Tab.GoBackAsync();
                e.Handled = true;
                break;

            case Key.F2 when FileList.SelectedItem is FileSystemEntry renameTarget:
                renameTarget.IsRenaming = true;
                e.Handled = true;
                break;

            case Key.F5:
                _ = Tab.RefreshAsync();
                e.Handled = true;
                break;

            case Key.Delete:
                Main.DeleteSelection(permanent: shift);
                e.Handled = true;
                break;

            case Key.C when ctrl:
                Main.CopySelection();
                e.Handled = true;
                break;

            case Key.X when ctrl:
                Main.CutSelection();
                e.Handled = true;
                break;

            case Key.V when ctrl:
                Main.Paste();
                e.Handled = true;
                break;

            case Key.A when ctrl:
                FileList.SelectAll();
                e.Handled = true;
                break;
        }
    }

    // --- Sorting ----------------------------------------------------------

    private void OnColumnHeaderClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader { Content: string header } || Tab is null)
            return;

        Tab.Sort(header switch
        {
            "Date modified" => SortColumn.Modified,
            "Type" => SortColumn.Type,
            "Size" => SortColumn.Size,
            _ => SortColumn.Name,
        });
    }

    // --- Shell context menu ----------------------------------------------

    private void OnListRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Right-clicking an unselected item selects it first, like Explorer does.
        if (FindEntry(e.OriginalSource) is not { } entry)
            return;

        if (!FileList.SelectedItems.Contains(entry))
        {
            FileList.SelectedItems.Clear();
            FileList.SelectedItems.Add(entry);
        }
    }

    private void OnListRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (Tab is null || Source is null)
            return;

        e.Handled = true;

        var screen = FileList.PointToScreen(e.GetPosition(FileList));
        var x = (int)screen.X;
        var y = (int)screen.Y;
        var extended = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

        string? verb;

        if (FindEntry(e.OriginalSource) is not null && FileList.SelectedItems.Count > 0)
        {
            var entries = FileList.SelectedItems.Cast<FileSystemEntry>().ToList();
            var paths = entries.Select(i => i.FullPath).ToList();

            // Opening a single folder and renaming happen in this window; left to the shell,
            // "open" would pop up a separate Explorer window next to the navigated tab.
            IReadOnlyCollection<string> appVerbs = entries is [{ IsDirectory: true }]
                ? ["open", "explore", "rename"]
                : ["rename"];

            verb = ShellContextMenu.ShowForItems(Source, paths, x, y, extended, PinStateFor(paths, entries), appVerbs);
        }
        else if (!KnownLocations.IsVirtual(Tab.CurrentPath))
        {
            FileList.SelectedItems.Clear();
            verb = ShellContextMenu.ShowForFolderBackground(
                Source, Tab.CurrentPath, x, y, extended, ClipboardService.HasFiles(),
                PinStateFor([Tab.CurrentPath], null));
        }
        else
        {
            return;
        }

        HandleInvokedVerb(verb);
    }

    /// <summary>
    /// The folder the pin entry refers to: the selected one, or the open folder when the
    /// menu came from the empty space below the list.
    /// </summary>
    private string PinTarget()
        => FileList.SelectedItem is FileSystemEntry { IsDirectory: true } entry
            ? entry.FullPath
            : Tab?.CurrentPath ?? string.Empty;

    /// <summary>
    /// Whether the menu should offer to pin (<c>true</c>), to unpin (<c>false</c>) or neither.
    /// Only folders can be pinned, and only one entry at a time, which matches Explorer.
    /// </summary>
    private bool? PinStateFor(IReadOnlyList<string> paths, IReadOnlyList<FileSystemEntry>? entries)
    {
        if (Main is null || paths.Count != 1 || KnownLocations.IsVirtual(paths[0]))
            return null;

        if (entries is not null && entries[0] is not { IsDirectory: true })
            return null;

        return !Main.IsFavorite(paths[0]);
    }

    /// <summary>
    /// The shell already performed the action; we only mirror the effects it has on our view.
    /// </summary>
    private void HandleInvokedVerb(string? verb)
    {
        if (verb is null)
            return;

        // The app's own entries are handled here; the shell never saw them.
        switch (verb)
        {
            case ShellContextMenu.PasteVerb:
                Main?.Paste();
                return;

            case ShellContextMenu.RefreshVerb:
                _ = Tab?.RefreshAsync();
                return;

            case ShellContextMenu.PinVerb:
                Main?.AddFavorite(PinTarget());
                return;

            case ShellContextMenu.UnpinVerb:
                Main?.RemoveFavorite(PinTarget());
                return;
        }

        switch (verb.ToLowerInvariant())
        {
            case "rename":
                if (FileList.SelectedItem is FileSystemEntry entry)
                    entry.IsRenaming = true;
                break;

            case "open" or "explore" when FileList.SelectedItem is FileSystemEntry { IsDirectory: true } folder:
                _ = Tab?.NavigateAsync(folder.FullPath);
                break;

            case "delete":
            case "paste":
            case "cut":
            case "link":
            case "newfolder":
                _ = Tab?.RefreshAsync();
                break;

            default:
                // Third-party verbs may or may not change the folder; a refresh is cheap.
                _ = Tab?.RefreshAsync();
                break;
        }
    }

    // --- Inline rename ----------------------------------------------------

    private void OnRenameBoxVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not TextBox box || !box.IsVisible || box.Tag is not FileSystemEntry entry)
            return;

        box.Text = entry.Name;
        box.Focus();

        // Preselect the base name, leaving the extension alone.
        var stem = entry.IsDirectory ? entry.Name.Length : Path.GetFileNameWithoutExtension(entry.Name).Length;
        box.Select(0, stem > 0 ? stem : entry.Name.Length);
    }

    private void OnRenameBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box || box.Tag is not FileSystemEntry entry)
            return;

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            CommitRename(entry, box.Text);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            entry.IsRenaming = false;
            FileList.Focus();
        }
    }

    private void OnRenameBoxLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { Tag: FileSystemEntry entry } box && entry.IsRenaming)
            CommitRename(entry, box.Text);
    }

    private void CommitRename(FileSystemEntry entry, string newName)
    {
        entry.IsRenaming = false;
        Main?.Rename(entry, newName.Trim());
        FileList.Focus();
    }

    // --- Drag & drop ------------------------------------------------------

    private void OnListLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _mayDrag = FindEntry(e.OriginalSource) is not null;
    }

    private void OnListMouseMove(object sender, MouseEventArgs e)
    {
        if (!_mayDrag || e.LeftButton != MouseButtonState.Pressed || FileList.SelectedItems.Count == 0)
            return;

        var position = e.GetPosition(this);
        if (Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _mayDrag = false;

        var files = new StringCollection();
        foreach (FileSystemEntry entry in FileList.SelectedItems)
            files.Add(entry.FullPath);

        var data = new DataObject();
        data.SetFileDropList(files);

        try
        {
            DragDrop.DoDragDrop(FileList, data, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // A drop target that rejects the data must not take the app down.
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = ResolveDropTarget(e) is not { } target
            ? DragDropEffects.None
            : ShouldMove(e, target) ? DragDropEffects.Move : DragDropEffects.Copy;

        e.Handled = true;
    }

    /// <summary>
    /// Explorer's rule: dragging inside one volume moves, dragging across volumes copies.
    /// Shift forces a move, Ctrl forces a copy.
    /// </summary>
    private static bool ShouldMove(DragEventArgs e, string target)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            return true;

        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            return false;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } paths)
            return false;

        var sourceRoot = Path.GetPathRoot(paths[0]);
        var targetRoot = Path.GetPathRoot(target);

        return !string.IsNullOrEmpty(sourceRoot)
            && string.Equals(sourceRoot, targetRoot, StringComparison.OrdinalIgnoreCase);
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (ResolveDropTarget(e) is not { } target || Main is null)
            return;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
            return;

        var move = ShouldMove(e, target);

        // Dropping a folder onto itself would be a no-op at best, and moving an item into
        // the folder it already lives in is one too.
        var sources = paths
            .Where(p => !string.Equals(p, target, StringComparison.OrdinalIgnoreCase))
            .Where(p => !move || !string.Equals(
                Path.GetDirectoryName(p), target, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Main.DropFiles(sources, target, move);
        e.Handled = true;
    }

    /// <summary>The folder a drop would land in: the hovered directory, else the current folder.</summary>
    private string? ResolveDropTarget(DragEventArgs e)
    {
        if (Tab is null || !e.Data.GetDataPresent(DataFormats.FileDrop))
            return null;

        var position = e.GetPosition(FileList);
        var hit = FileList.InputHitTest(position) as DependencyObject;
        var entry = FindEntry(hit);

        if (entry is { IsDirectory: true })
            return entry.FullPath;

        return KnownLocations.IsVirtual(Tab.CurrentPath) ? null : Tab.CurrentPath;
    }
}
