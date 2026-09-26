using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using FileExplorer.Interop;
using FileExplorer.Models;
using FileExplorer.Services;

namespace FileExplorer.ViewModels;

public sealed record Breadcrumb(string Path, string Name);

/// <summary>
/// One tab: current folder, listing, selection, navigation history, view mode and search.
/// </summary>
public sealed class ExplorerTabViewModel : ObservableObject
{
    private readonly ExplorerSettings _settings;
    private readonly Dispatcher _dispatcher;
    private readonly List<string> _history = [];

    private CancellationTokenSource? _listingCts;
    private CancellationTokenSource? _thumbnailCts;
    private CancellationTokenSource? _searchCts;

    private int _historyIndex = -1;
    private string _currentPath = string.Empty;
    private string _header = string.Empty;
    private string _addressText = string.Empty;
    private string _statusText = string.Empty;
    private string? _error;
    private bool _isLoading;
    private string _searchQuery = string.Empty;
    private bool _isSearchResult;
    private ViewMode _viewMode = ViewMode.Details;
    private SortColumn _sortColumn = SortColumn.Name;
    private bool _sortDescending;

    public ExplorerTabViewModel(ExplorerSettings settings)
    {
        _settings = settings;
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    public ObservableCollection<FileSystemEntry> Entries { get; } = [];

    public ObservableCollection<Breadcrumb> Breadcrumbs { get; } = [];

    /// <summary>Set by the view whenever the list selection changes.</summary>
    public IReadOnlyList<FileSystemEntry> SelectedEntries { get; private set; } = [];

    public string CurrentPath
    {
        get => _currentPath;
        private set => SetProperty(ref _currentPath, value);
    }

    public string Header
    {
        get => _header;
        private set => SetProperty(ref _header, value);
    }

    public string AddressText
    {
        get => _addressText;
        set => SetProperty(ref _addressText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string? Error
    {
        get => _error;
        private set
        {
            if (SetProperty(ref _error, value))
                OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrEmpty(Error);

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }

    public bool IsSearchResult
    {
        get => _isSearchResult;
        private set => SetProperty(ref _isSearchResult, value);
    }

    public ViewMode ViewMode
    {
        get => _viewMode;
        set
        {
            if (!SetProperty(ref _viewMode, value))
                return;

            OnPropertyChanged(nameof(IsDetailsView));
            OnPropertyChanged(nameof(IsListView));
            OnPropertyChanged(nameof(IsIconView));
            OnPropertyChanged(nameof(ThumbnailSize));
            StartThumbnailLoad();
        }
    }

    public bool IsDetailsView => ViewMode == ViewMode.Details;

    public bool IsListView => ViewMode == ViewMode.List;

    public bool IsIconView => ViewMode.IsIconView();

    public int ThumbnailSize => ViewMode.ThumbnailSize() is var size && size > 0 ? size : 16;

    public SortColumn SortColumn
    {
        get => _sortColumn;
        private set => SetProperty(ref _sortColumn, value);
    }

    public bool SortDescending
    {
        get => _sortDescending;
        private set => SetProperty(ref _sortDescending, value);
    }

    public bool CanGoBack => _historyIndex > 0;

    public bool CanGoForward => _historyIndex >= 0 && _historyIndex < _history.Count - 1;

    public bool CanGoUp => DirectoryService.GetParent(CurrentPath) is not null;

    public event EventHandler? NavigationChanged;

    // --- Navigation -------------------------------------------------------

    public async Task NavigateAsync(string path, bool addToHistory = true)
    {
        path = NormalizePath(path);

        if (addToHistory && !string.Equals(path, CurrentPath, StringComparison.OrdinalIgnoreCase))
        {
            if (_historyIndex < _history.Count - 1)
                _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);

            _history.Add(path);
            _historyIndex = _history.Count - 1;
        }
        else if (_history.Count == 0)
        {
            _history.Add(path);
            _historyIndex = 0;
        }

        await LoadAsync(path);
    }

    public Task GoBackAsync()
    {
        if (!CanGoBack)
            return Task.CompletedTask;

        _historyIndex--;
        return LoadAsync(_history[_historyIndex]);
    }

    public Task GoForwardAsync()
    {
        if (!CanGoForward)
            return Task.CompletedTask;

        _historyIndex++;
        return LoadAsync(_history[_historyIndex]);
    }

    public Task GoUpAsync()
    {
        var parent = DirectoryService.GetParent(CurrentPath);
        return parent is null ? Task.CompletedTask : NavigateAsync(parent);
    }

    public Task RefreshAsync() => LoadAsync(CurrentPath);

    private async Task LoadAsync(string path)
    {
        _searchCts?.Cancel();
        _listingCts?.Cancel();
        _listingCts = new CancellationTokenSource();
        var token = _listingCts.Token;

        CurrentPath = path;
        AddressText = path == KnownLocations.ThisPc ? KnownLocations.DisplayName(path) : path;
        Header = KnownLocations.DisplayName(path);
        IsSearchResult = false;
        SearchQuery = string.Empty;
        IsLoading = true;
        Error = null;
        UpdateBreadcrumbs(path);

        try
        {
            var listing = await DirectoryService.ListAsync(path, _settings.ShowHiddenItems, token);
            if (token.IsCancellationRequested)
                return;

            Error = listing.Error;
            ReplaceEntries(listing.Entries);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (!token.IsCancellationRequested)
                IsLoading = false;
        }

        RaiseNavigationChanged();
    }

    private void RaiseNavigationChanged()
    {
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
        OnPropertyChanged(nameof(CanGoUp));
        NavigationChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string NormalizePath(string path)
    {
        if (path == KnownLocations.ThisPc || string.IsNullOrWhiteSpace(path))
            return path;

        path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));

        // "C:" alone means "current directory on C:" to Win32 but "C:\" to a user.
        if (path.Length == 2 && path[1] == ':')
            path += Path.DirectorySeparatorChar;

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return path;
        }
    }

    /// <summary>
    /// Resolves what the user typed in the address bar: a folder, a file (which gets opened),
    /// or something the shell can handle such as <c>shell:Downloads</c>.
    /// </summary>
    public async Task<bool> NavigateToAddressAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var path = NormalizePath(input);

        if (Directory.Exists(path) || path == KnownLocations.ThisPc)
        {
            await NavigateAsync(path);
            return true;
        }

        if (File.Exists(path))
        {
            LaunchService.Open(path);
            return true;
        }

        // Fall back to the shell so shell: and ::{GUID} locations still work.
        var resolved = ShellHelper.GetDisplayName(input, SIGDN.FileSysPath);
        if (!string.IsNullOrEmpty(resolved) && Directory.Exists(resolved))
        {
            await NavigateAsync(resolved);
            return true;
        }

        return false;
    }

    private void UpdateBreadcrumbs(string path)
    {
        Breadcrumbs.Clear();
        Breadcrumbs.Add(new Breadcrumb(KnownLocations.ThisPc, "This PC"));

        if (path == KnownLocations.ThisPc)
            return;

        var segments = new List<Breadcrumb>();
        var current = path;

        while (!string.IsNullOrEmpty(current))
        {
            segments.Insert(0, new Breadcrumb(current, KnownLocations.DisplayName(current)));

            // Stop at the volume root. Path.GetDirectoryName is used rather than
            // Directory.GetParent because the latter resolves "C:" against the process
            // working directory, which walks back down and loops forever.
            if (string.Equals(current, Path.GetPathRoot(current), StringComparison.OrdinalIgnoreCase))
                break;

            var parent = Path.GetDirectoryName(current.TrimEnd(Path.DirectorySeparatorChar));
            if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
                break;

            current = parent;
        }

        foreach (var segment in segments)
            Breadcrumbs.Add(segment);
    }

    // --- Listing ----------------------------------------------------------

    private void ReplaceEntries(IReadOnlyList<FileSystemEntry> entries)
    {
        Entries.Clear();
        foreach (var entry in Sort(entries))
            Entries.Add(entry);

        UpdateStatus();
        StartThumbnailLoad();
    }

    public void Sort(SortColumn column)
    {
        if (column == SortColumn)
        {
            SortDescending = !SortDescending;
        }
        else
        {
            SortColumn = column;
            SortDescending = false;
        }

        var sorted = Sort(Entries.ToList());
        Entries.Clear();
        foreach (var entry in sorted)
            Entries.Add(entry);
    }

    private IEnumerable<FileSystemEntry> Sort(IEnumerable<FileSystemEntry> entries)
    {
        // Folders always come before files, exactly like Explorer.
        var ordered = entries.OrderBy(e => e.IsDirectory ? 0 : 1);

        ordered = SortColumn switch
        {
            SortColumn.Modified => SortDescending
                ? ordered.ThenByDescending(e => e.Modified)
                : ordered.ThenBy(e => e.Modified),
            SortColumn.Size => SortDescending
                ? ordered.ThenByDescending(e => e.Size)
                : ordered.ThenBy(e => e.Size),
            SortColumn.Type => SortDescending
                ? ordered.ThenByDescending(e => e.Extension, StringComparer.CurrentCultureIgnoreCase)
                : ordered.ThenBy(e => e.Extension, StringComparer.CurrentCultureIgnoreCase),
            _ => SortDescending
                ? ordered.ThenByDescending(e => e.Name, NaturalComparer.Instance)
                : ordered.ThenBy(e => e.Name, NaturalComparer.Instance),
        };

        return ordered;
    }

    public void SetSelection(IReadOnlyList<FileSystemEntry> selection)
    {
        SelectedEntries = selection;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var total = Entries.Count;
        var selected = SelectedEntries.Count;

        if (selected == 0)
        {
            StatusText = total == 1 ? "1 item" : $"{total} items";
            return;
        }

        var bytes = SelectedEntries.Where(e => !e.IsDirectory).Sum(e => e.Size);
        var sizeText = bytes > 0 ? $" ({FileSystemEntry.FormatSize(bytes)})" : string.Empty;
        StatusText = $"{total} items | {selected} selected{sizeText}";
    }

    // --- Thumbnails -------------------------------------------------------

    private void StartThumbnailLoad()
    {
        _thumbnailCts?.Cancel();

        var size = ViewMode.ThumbnailSize();
        if (size == 0)
            return;

        _thumbnailCts = new CancellationTokenSource();
        var token = _thumbnailCts.Token;
        var snapshot = Entries.ToList();

        _ = Task.Run(() =>
        {
            foreach (var entry in snapshot)
            {
                if (token.IsCancellationRequested)
                    return;

                var thumbnail = ShellIconProvider.GetThumbnail(entry.FullPath, size);
                if (thumbnail is null)
                    continue;

                _dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
                {
                    if (!token.IsCancellationRequested)
                        entry.ApplyThumbnail(thumbnail);
                });
            }
        }, token);
    }

    // --- Search -----------------------------------------------------------

    public async Task RunSearchAsync()
    {
        _searchCts?.Cancel();

        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            await RefreshAsync();
            return;
        }

        // A listing still in flight would otherwise land on top of the search results.
        _listingCts?.Cancel();

        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        var root = CurrentPath;
        RaiseNavigationChanged();

        IsSearchResult = true;
        IsLoading = true;
        Error = null;
        Entries.Clear();

        try
        {
            await SearchService.SearchAsync(root, SearchQuery, _settings.ShowHiddenItems, batch =>
            {
                _dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
                {
                    if (token.IsCancellationRequested)
                        return;

                    foreach (var entry in batch)
                        Entries.Add(entry);

                    UpdateStatus();
                });
            }, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                IsLoading = false;
                UpdateStatus();
            }
        }
    }

    // --- Session ----------------------------------------------------------

    public TabState ToState() => new()
    {
        Path = CurrentPath,
        ViewMode = ViewMode,
        SortColumn = SortColumn,
        SortDescending = SortDescending,
        History = [.. _history],
        HistoryIndex = _historyIndex,
    };

    public void RestoreHistory(TabState state)
    {
        _history.Clear();
        _history.AddRange(state.History.Count > 0 ? state.History : [state.Path]);
        _historyIndex = Math.Clamp(state.HistoryIndex, 0, _history.Count - 1);

        _viewMode = state.ViewMode;
        _sortColumn = state.SortColumn;
        _sortDescending = state.SortDescending;

        OnPropertyChanged(nameof(ViewMode));
        OnPropertyChanged(nameof(IsDetailsView));
        OnPropertyChanged(nameof(IsListView));
        OnPropertyChanged(nameof(IsIconView));
        OnPropertyChanged(nameof(ThumbnailSize));
    }

    public void Dispose()
    {
        _listingCts?.Cancel();
        _thumbnailCts?.Cancel();
        _searchCts?.Cancel();
    }
}

/// <summary>Sorts "File 10" after "File 2", the way Explorer does.</summary>
internal sealed class NaturalComparer : IComparer<string>
{
    internal static readonly NaturalComparer Instance = new();

    public int Compare(string? x, string? y) => StrCmpLogicalW(x ?? string.Empty, y ?? string.Empty);

    [System.Runtime.InteropServices.DllImport("shlwapi.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int StrCmpLogicalW(string x, string y);
}

