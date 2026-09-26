namespace FileExplorer.Models;

public enum ViewMode
{
    Details,
    List,
    Tiles,
    MediumIcons,
    LargeIcons,
    ExtraLargeIcons,
}

public enum SortColumn
{
    Name,
    Modified,
    Type,
    Size,
}

public static class ViewModeExtensions
{
    /// <summary>Thumbnail edge length in device-independent pixels, or 0 for the 16px list icon.</summary>
    public static int ThumbnailSize(this ViewMode mode) => mode switch
    {
        ViewMode.Tiles => 48,
        ViewMode.MediumIcons => 64,
        ViewMode.LargeIcons => 96,
        ViewMode.ExtraLargeIcons => 160,
        _ => 0,
    };

    public static bool IsIconView(this ViewMode mode) => mode.ThumbnailSize() > 0;

    public static string DisplayName(this ViewMode mode) => mode switch
    {
        ViewMode.Details => "Details",
        ViewMode.List => "List",
        ViewMode.Tiles => "Tiles",
        ViewMode.MediumIcons => "Medium icons",
        ViewMode.LargeIcons => "Large icons",
        ViewMode.ExtraLargeIcons => "Extra large icons",
        _ => mode.ToString(),
    };
}
