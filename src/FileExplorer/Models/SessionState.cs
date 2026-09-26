namespace FileExplorer.Models;

/// <summary>Everything restored on the next start: window placement, tabs and favorites.</summary>
public sealed class SessionState
{
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public double WindowWidth { get; set; } = 1180;
    public double WindowHeight { get; set; } = 760;
    public bool WindowMaximized { get; set; }

    public bool ShowHiddenItems { get; set; }
    public bool ShowFileExtensions { get; set; } = true;
    public double NavigationPaneWidth { get; set; } = 240;

    public List<TabState> Tabs { get; set; } = [];
    public int SelectedTabIndex { get; set; }
    public List<FavoriteState> Favorites { get; set; } = [];
}

public sealed class TabState
{
    public string Path { get; set; } = string.Empty;
    public ViewMode ViewMode { get; set; } = ViewMode.Details;
    public SortColumn SortColumn { get; set; } = SortColumn.Name;
    public bool SortDescending { get; set; }
    public List<string> History { get; set; } = [];
    public int HistoryIndex { get; set; } = -1;
}

public sealed class FavoriteState
{
    public string Path { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
}
