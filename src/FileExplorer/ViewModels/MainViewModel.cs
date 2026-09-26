using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using FileExplorer.Interop;
using FileExplorer.Models;
using FileExplorer.Services;

namespace FileExplorer.ViewModels;

/// <summary>
/// Owns the tab strip, the navigation pane and every command that acts on the active tab.
/// File operations are delegated to the shell (see <see cref="ShellFileOperations"/>).
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private ExplorerTabViewModel? _selectedTab;

    public MainViewModel()
    {
        Settings.Changed += (_, _) => _ = SelectedTab?.RefreshAsync();
    }

    public ExplorerSettings Settings { get; } = new();

    public ObservableCollection<ExplorerTabViewModel> Tabs { get; } = [];

    public ObservableCollection<NavigationNodeViewModel> Favorites { get; } = [];

    public ObservableCollection<NavigationNodeViewModel> TreeRoots { get; } = [];

    /// <summary>Owner window handle, needed so shell dialogs are modal to this app.</summary>
    public IntPtr OwnerHandle { get; set; }

    public ExplorerTabViewModel? SelectedTab
    {
        get => _selectedTab;
        set => SetProperty(ref _selectedTab, value);
    }

    public IReadOnlyList<ViewMode> ViewModes { get; } = Enum.GetValues<ViewMode>();

    // --- Session ----------------------------------------------------------

    public SessionState LoadSession()
    {
        var state = SessionService.Load();

        foreach (var favorite in state.Favorites)
        {
            if (Directory.Exists(favorite.Path))
                Favorites.Add(CreateNode(favorite.Path, favorite.DisplayName));
        }

        TreeRoots.Add(new NavigationNodeViewModel(KnownLocations.ThisPc, "This PC", Settings, canExpand: true));

        Settings.ShowHiddenItems = state.ShowHiddenItems;
        Settings.ShowFileExtensions = state.ShowFileExtensions;

        return state;
    }

    public async Task RestoreTabsAsync(SessionState state)
    {
        foreach (var tabState in state.Tabs)
        {
            var path = Directory.Exists(tabState.Path) || tabState.Path == KnownLocations.ThisPc
                ? tabState.Path
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            var tab = new ExplorerTabViewModel(Settings);
            tab.RestoreHistory(tabState);
            Tabs.Add(tab);
            await tab.NavigateAsync(path, addToHistory: false);
        }

        if (Tabs.Count == 0)
            await NewTabAsync(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        SelectedTab = Tabs[Math.Clamp(state.SelectedTabIndex, 0, Tabs.Count - 1)];
    }

    public void SaveSession(SessionState state)
    {
        state.Tabs = Tabs.Select(t => t.ToState()).ToList();
        state.SelectedTabIndex = SelectedTab is null ? 0 : Tabs.IndexOf(SelectedTab);
        state.ShowHiddenItems = Settings.ShowHiddenItems;
        state.ShowFileExtensions = Settings.ShowFileExtensions;
        state.Favorites = Favorites.Select(f => new FavoriteState { Path = f.Path, DisplayName = f.Name }).ToList();

        SessionService.Save(state);
    }

    private NavigationNodeViewModel CreateNode(string path, string? displayName = null)
        => new(path, displayName ?? KnownLocations.DisplayName(path), Settings,
            DirectoryService.HasSubFolders(path, Settings.ShowHiddenItems));

    // --- Tabs -------------------------------------------------------------

    public async Task NewTabAsync(string? path = null)
    {
        path ??= SelectedTab?.CurrentPath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var tab = new ExplorerTabViewModel(Settings);
        Tabs.Add(tab);
        SelectedTab = tab;
        await tab.NavigateAsync(path);
    }

    public void CloseTab(ExplorerTabViewModel tab)
    {
        if (Tabs.Count <= 1)
            return;

        var index = Tabs.IndexOf(tab);
        if (index < 0)
            return;

        // Closing a background tab leaves the active one where it is.
        var active = SelectedTab == tab ? null : SelectedTab;

        tab.Dispose();
        Tabs.Remove(tab);

        SelectedTab = active ?? Tabs[Math.Min(index, Tabs.Count - 1)];
    }

    public void DuplicateTab(ExplorerTabViewModel tab) => _ = NewTabAsync(tab.CurrentPath);

    // --- File commands ----------------------------------------------------

    private IReadOnlyList<string> SelectedPaths
        => SelectedTab?.SelectedEntries.Select(e => e.FullPath).ToList() ?? [];

    public void CopySelection() => ClipboardService.SetFiles(SelectedPaths, cut: false);

    public void CutSelection() => ClipboardService.SetFiles(SelectedPaths, cut: true);

    public void Paste()
    {
        if (SelectedTab is not { } tab || KnownLocations.IsVirtual(tab.CurrentPath))
            return;

        var (paths, isCut) = ClipboardService.GetFiles();
        if (paths.Count == 0)
            return;

        // Refreshed even when the user cancelled: part of the items may already have moved.
        _ = isCut
            ? ShellFileOperations.Move(OwnerHandle, paths, tab.CurrentPath)
            : ShellFileOperations.Copy(OwnerHandle, paths, tab.CurrentPath);

        _ = tab.RefreshAsync();
    }

    public void DeleteSelection(bool permanent)
    {
        var paths = SelectedPaths;
        if (paths.Count == 0)
            return;

        ShellFileOperations.Delete(OwnerHandle, paths, permanent);
        _ = SelectedTab?.RefreshAsync();
    }

    public bool Rename(FileSystemEntry entry, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || newName == entry.Name)
            return false;

        var ok = ShellFileOperations.Rename(OwnerHandle, entry.FullPath, newName);
        _ = SelectedTab?.RefreshAsync();
        return ok;
    }

    public void CreateFolder()
    {
        if (SelectedTab is not { } tab || KnownLocations.IsVirtual(tab.CurrentPath))
            return;

        var created = ShellFileOperations.CreateNewItem(OwnerHandle, tab.CurrentPath, "New folder", isFolder: true);
        if (created is not null)
            _ = tab.RefreshAsync();
    }

    public void DropFiles(IReadOnlyList<string> paths, string destination, bool move)
    {
        if (paths.Count == 0 || KnownLocations.IsVirtual(destination))
            return;

        _ = move
            ? ShellFileOperations.Move(OwnerHandle, paths, destination)
            : ShellFileOperations.Copy(OwnerHandle, paths, destination);

        _ = SelectedTab?.RefreshAsync();
    }

    // --- Favorites --------------------------------------------------------

    public void AddFavorite(string path)
    {
        if (KnownLocations.IsVirtual(path) || Favorites.Any(f =>
                string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        Favorites.Add(CreateNode(path));
    }

    public void RemoveFavorite(NavigationNodeViewModel node) => Favorites.Remove(node);

    public void RemoveFavorite(string path)
    {
        if (Favorites.FirstOrDefault(f =>
                string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase)) is { } node)
        {
            Favorites.Remove(node);
        }
    }

    public bool IsFavorite(string path)
        => Favorites.Any(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase));

    // --- Misc -------------------------------------------------------------

    public void OpenInWindowsExplorer()
    {
        if (SelectedTab is { } tab && !KnownLocations.IsVirtual(tab.CurrentPath))
            ShellHelper.RevealInExplorer(tab.CurrentPath);
    }

    public void OpenTerminalHere()
    {
        if (SelectedTab is { } tab && !KnownLocations.IsVirtual(tab.CurrentPath))
            LaunchService.OpenTerminal(tab.CurrentPath);
    }

    public void CopyPathOfSelection()
    {
        var paths = SelectedPaths;
        if (paths.Count == 0)
            return;

        try
        {
            Clipboard.SetText(string.Join(Environment.NewLine, paths.Select(p => $"\"{p}\"")));
        }
        catch (Exception)
        {
            // Clipboard contention; nothing worth interrupting the user for.
        }
    }
}
