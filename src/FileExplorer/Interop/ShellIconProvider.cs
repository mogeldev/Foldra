using System.IO;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FileExplorer.Interop;

/// <summary>
/// Supplies the same icons and thumbnails Explorer shows, straight from the shell.
/// Small icons are cached per file extension, thumbnails per full path.
/// </summary>
internal static class ShellIconProvider
{
    /// <summary>
    /// Cache ceiling. Extension keys are few, but per-file keys (executables, shortcuts,
    /// thumbnails) grow with every folder visited, so a full cache is dropped rather than
    /// letting a long session accumulate bitmaps without bound.
    /// </summary>
    private const int MaxCacheEntries = 2000;

    private static readonly ConcurrentDictionary<string, ImageSource?> SmallIconCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, ImageSource?> ThumbnailCache = new(StringComparer.OrdinalIgnoreCase);

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr hgdiobj, int cbBuffer, ref BITMAP lpvObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    /// <summary>
    /// 16px list icon. Directories share one cache entry, files are keyed by extension,
    /// which keeps the shell calls to roughly one per distinct file type.
    /// </summary>
    internal static ImageSource? GetSmallIcon(string path, bool isDirectory)
    {
        var key = isDirectory ? "\0dir" : Path.GetExtension(path).ToLowerInvariant();

        // Executables, shortcuts and drives carry their own icon, so they must not share a
        // cache slot - a drive would otherwise get the generic folder icon.
        var perFile = key is ".exe" or ".lnk" or ".ico" or ".url" or ".cur" or ".msc"
            || (isDirectory && ShellHelper.IsVolumeRoot(path));
        if (perFile)
            key = path;

        if (perFile)
            Trim(SmallIconCache);

        return SmallIconCache.GetOrAdd(key, _ => LoadSmallIcon(path, isDirectory, useFileAttributes: !perFile));
    }

    private static ImageSource? LoadSmallIcon(string path, bool isDirectory, bool useFileAttributes)
    {
        var info = new SHFILEINFO();
        var flags = SHGFI.Icon | SHGFI.SmallIcon;
        uint attributes = 0;

        if (useFileAttributes)
        {
            flags |= SHGFI.UseFileAttributes;
            attributes = isDirectory ? 0x10u : 0x80u;
        }

        var result = NativeMethods.SHGetFileInfo(path, attributes, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);
        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
            return null;

        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(
                info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            NativeMethods.DestroyIcon(info.hIcon);
        }
    }

    /// <summary>
    /// Thumbnail (real preview for images/documents, large icon otherwise) at the requested edge length.
    /// Returns <c>null</c> when the shell has nothing to offer; callers fall back to the small icon.
    /// </summary>
    internal static ImageSource? GetThumbnail(string path, int size)
    {
        var key = size + "|" + path;
        if (ThumbnailCache.TryGetValue(key, out var cached))
            return cached;

        var image = LoadThumbnail(path, size);
        Trim(ThumbnailCache);
        ThumbnailCache[key] = image;
        return image;
    }

    private static ImageSource? LoadThumbnail(string path, int size)
    {
        var item = ShellHelper.CreateItem(path);
        if (item is not IShellItemImageFactory factory)
        {
            if (item is not null)
                Marshal.ReleaseComObject(item);
            return null;
        }

        var hBitmap = IntPtr.Zero;
        try
        {
            var hr = factory.GetImage(new NativeSize(size, size), SIIGBF.ResizeToFit | SIIGBF.BiggerSizeOk, out hBitmap);
            if (hr != 0 || hBitmap == IntPtr.Zero)
                return null;

            return ConvertHBitmap(hBitmap);
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            if (hBitmap != IntPtr.Zero)
                DeleteObject(hBitmap);
            Marshal.ReleaseComObject(factory);
        }
    }

    /// <summary>
    /// Copies a 32bpp shell HBITMAP into a WPF bitmap. Going through the raw DIB bits
    /// preserves the alpha channel, which <c>CreateBitmapSourceFromHBitmap</c> throws away.
    /// </summary>
    private static ImageSource? ConvertHBitmap(IntPtr hBitmap)
    {
        var bmp = new BITMAP();
        if (GetObject(hBitmap, Marshal.SizeOf<BITMAP>(), ref bmp) == 0)
            return null;

        if (bmp.bmBitsPixel != 32 || bmp.bmBits == IntPtr.Zero)
        {
            var plain = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            plain.Freeze();
            return plain;
        }

        var stride = bmp.bmWidthBytes;
        var length = stride * bmp.bmHeight;
        var buffer = new byte[length];
        Marshal.Copy(bmp.bmBits, buffer, 0, length);

        // Shell DIBs are bottom-up only when bmHeight is positive in a BITMAPINFOHEADER;
        // the sections returned here are top-down, so the rows are already in order.
        var source = BitmapSource.Create(
            bmp.bmWidth, bmp.bmHeight, 96, 96, PixelFormats.Bgra32, null, buffer, stride);
        source.Freeze();
        return source;
    }

    private static void Trim(ConcurrentDictionary<string, ImageSource?> cache)
    {
        if (cache.Count > MaxCacheEntries)
            cache.Clear();
    }
}
