using System.Collections.ObjectModel;
using System.Windows.Media;
using FileExplorer.Interop;
using FileExplorer.Services;

namespace FileExplorer.ViewModels;

/// <summary>
/// A node in the left navigation tree. Children are loaded the first time a node is expanded,
/// so the tree never walks the whole disk.
/// </summary>
public sealed class NavigationNodeViewModel : ObservableObject
{
    private static readonly NavigationNodeViewModel Placeholder = new("", "", null, false);

    private readonly ExplorerSettings? _settings;
    private bool _isExpanded;
    private bool _isSelected;
    private bool _childrenLoaded;

    public NavigationNodeViewModel(string path, string name, ExplorerSettings? settings, bool canExpand)
    {
        Path = path;
        Name = name;
        _settings = settings;

        if (canExpand)
            Children.Add(Placeholder);
    }

    public string Path { get; }

    public string Name { get; }

    public ObservableCollection<NavigationNodeViewModel> Children { get; } = [];

    public ImageSource? Icon => ShellIconProvider.GetSmallIcon(Path, isDirectory: true);

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (!SetProperty(ref _isExpanded, value))
                return;

            if (value)
                LoadChildren();
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private void LoadChildren()
    {
        if (_childrenLoaded)
            return;

        _childrenLoaded = true;
        Children.Clear();

        var showHidden = _settings?.ShowHiddenItems ?? false;

        foreach (var (path, name) in DirectoryService.GetSubFolders(Path, showHidden))
        {
            Children.Add(new NavigationNodeViewModel(
                path, name, _settings, DirectoryService.HasSubFolders(path, showHidden)));
        }
    }

    public void Reset()
    {
        _childrenLoaded = false;
        Children.Clear();
        Children.Add(Placeholder);
        _isExpanded = false;
        OnPropertyChanged(nameof(IsExpanded));
    }
}
