using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using FileExplorer.Interop;

namespace FileExplorer.Models;

public enum EntryKind
{
    Directory,
    File,
    Drive,
}

/// <summary>
/// One row in the file list. Icons and thumbnails are resolved lazily on first access so
/// listing a folder with thousands of entries stays fast.
/// </summary>
public sealed class FileSystemEntry : INotifyPropertyChanged
{
    private ImageSource? _icon;
    private bool _iconRequested;
    private string? _typeName;
    private bool _isRenaming;

    public FileSystemEntry(string fullPath, string name, EntryKind kind, long size, DateTime modified, FileAttributes attributes)
    {
        FullPath = fullPath;
        Name = name;
        Kind = kind;
        Size = size;
        Modified = modified;
        Attributes = attributes;
    }

    public string FullPath { get; }

    public string Name { get; }

    public EntryKind Kind { get; }

    public long Size { get; }

    public DateTime Modified { get; }

    public FileAttributes Attributes { get; }

    public bool IsDirectory => Kind != EntryKind.File;

    public bool IsHidden => (Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0;

    public string Extension => Kind == EntryKind.File ? Path.GetExtension(Name) : string.Empty;

    /// <summary>Human readable size; folders and drives show nothing.</summary>
    public string SizeText => Kind == EntryKind.File ? FormatSize(Size) : string.Empty;

    public string ModifiedText => Modified == default ? string.Empty : Modified.ToString("g");

    /// <summary>Localized type description from the shell, resolved on demand.</summary>
    public string TypeName => _typeName ??= ShellHelper.GetTypeName(FullPath, IsDirectory);

    /// <summary>Drives the inline edit box in the list, the way F2 works in Explorer.</summary>
    public bool IsRenaming
    {
        get => _isRenaming;
        set
        {
            if (_isRenaming == value)
                return;

            _isRenaming = value;
            OnPropertyChanged();
        }
    }

    public ImageSource? Icon
    {
        get
        {
            if (!_iconRequested)
            {
                _iconRequested = true;
                _icon = ShellIconProvider.GetSmallIcon(FullPath, IsDirectory);
            }

            return _icon;
        }
    }

    /// <summary>Replaces the icon with a real thumbnail; used by the larger view modes.</summary>
    public void ApplyThumbnail(ImageSource thumbnail)
    {
        _iconRequested = true;
        _icon = thumbnail;
        OnPropertyChanged(nameof(Icon));
    }

    public static string FormatSize(long bytes)
    {
        if (bytes < 0)
            return string.Empty;

        string[] units = ["B", "KB", "MB", "GB", "TB", "PB"];
        double value = bytes;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{bytes} {units[0]}"
            : $"{value:0.#} {units[unit]}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
