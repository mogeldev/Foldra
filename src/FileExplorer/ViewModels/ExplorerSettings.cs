namespace FileExplorer.ViewModels;

/// <summary>Options shared by every tab, mirroring Explorer's "Show" menu.</summary>
public sealed class ExplorerSettings : ObservableObject
{
    private bool _showHiddenItems;
    private bool _showFileExtensions = true;

    public bool ShowHiddenItems
    {
        get => _showHiddenItems;
        set
        {
            if (SetProperty(ref _showHiddenItems, value))
                Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool ShowFileExtensions
    {
        get => _showFileExtensions;
        set
        {
            if (SetProperty(ref _showFileExtensions, value))
                Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Raised when a setting changes so open tabs can refresh.</summary>
    public event EventHandler? Changed;
}
