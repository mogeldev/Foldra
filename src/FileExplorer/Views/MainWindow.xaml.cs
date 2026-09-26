using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Runtime.InteropServices;
using FileExplorer.Interop;
using FileExplorer.Models;
using FileExplorer.Services;
using FileExplorer.ViewModels;
using MenuItem = System.Windows.Controls.MenuItem;

namespace FileExplorer.Views;

public partial class MainWindow : Window
{
    /// <summary>In-process drag format for reordering tabs; never leaves this app.</summary>
    private const string TabDragFormat = "FileExplorer.Tab";

    private SessionState _session = new();
    private Point _tabDragStart;
    private ExplorerTabViewModel? _tabDragCandidate;

    public MainWindow()
    {
        InitializeComponent();

        ViewModel = new MainViewModel();
        DataContext = ViewModel;

        Loaded += OnLoaded;
        Closing += OnClosing;

        RegisterShortcuts();
    }

    public MainViewModel ViewModel { get; }

    private ExplorerTabViewModel? Tab => ViewModel.SelectedTab;

    // --- Lifecycle --------------------------------------------------------

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Diagnostics.Log("loaded begin");
        ViewModel.OwnerHandle = new WindowInteropHelper(this).Handle;
        ApplyWindowCornerPreference(ViewModel.OwnerHandle);
        ApplyTitleBarTheme(ViewModel.OwnerHandle, ((App)Application.Current).IsDarkTheme);
        HwndSource.FromHwnd(ViewModel.OwnerHandle)?.AddHook(OnThemeRelatedMessage);

        _session = ViewModel.LoadSession();
        ApplyWindowPlacement(_session);

        await ViewModel.RestoreTabsAsync(_session);
        Diagnostics.Log("tabs restored");
    }

    private static void ApplyWindowCornerPreference(IntPtr hwnd)
    {
        // Ask DWM for its own default corner look instead of leaving it unset: rounded on
        // Windows 11, square (the only option anyway) on Windows 10.
        var preference = NativeMethods.DWMWCP_DEFAULT;
        _ = NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
    }

    /// <summary>Paints the native, non-client title bar dark or light. WPF resources only cover
    /// the window's own content, not this DWM-drawn area, so it needs its own call.</summary>
    private static void ApplyTitleBarTheme(IntPtr hwnd, bool dark)
    {
        var useDark = dark ? 1 : 0;
        var result = NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
        if (result != 0)
            _ = NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref useDark, sizeof(int));
    }

    /// <summary>Reapplies the OS theme/accent when Windows announces either changed, so the app
    /// follows a live light/dark or accent color switch without a restart.</summary>
    private static IntPtr OnThemeRelatedMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_DWMCOLORIZATIONCOLORCHANGED ||
            (msg == NativeMethods.WM_SETTINGCHANGE && Marshal.PtrToStringUni(lParam) == "ImmersiveColorSet"))
        {
            var app = (App)Application.Current;
            app.RefreshTheme();
            ApplyTitleBarTheme(hwnd, app.IsDarkTheme);
        }

        return IntPtr.Zero;
    }

    private void ApplyWindowPlacement(SessionState state)
    {
        // The work area is in DIPs, like Left/Top/Width/Height. Clamping against it keeps the
        // window on a small or high-DPI screen instead of pushing the tab strip above y = 0.
        // It is the work area of the monitor the window was last on, so a window saved on a
        // secondary screen stays there instead of being pulled onto the primary one.
        var work = WorkAreaFor(state) ?? SystemParameters.WorkArea;

        Width = Math.Clamp(state.WindowWidth, MinWidth, Math.Max(MinWidth, work.Width));
        Height = Math.Clamp(state.WindowHeight, MinHeight, Math.Max(MinHeight, work.Height));

        if (!double.IsNaN(state.WindowLeft) && !double.IsNaN(state.WindowTop))
        {
            Left = state.WindowLeft;
            Top = state.WindowTop;
        }

        Left = Math.Clamp(Left, work.Left, Math.Max(work.Left, work.Right - Width));
        Top = Math.Clamp(Top, work.Top, Math.Max(work.Top, work.Bottom - Height));

        NavigationColumn.Width = new GridLength(Math.Clamp(state.NavigationPaneWidth, 160, 480));

        if (state.WindowMaximized)
            WindowState = WindowState.Maximized;
    }

    /// <summary>
    /// Work area of the monitor the saved window rectangle sits on, in DIPs, or <c>null</c>
    /// when there is no usable saved rectangle. The conversion uses this window's scale
    /// factor, which is what WPF applies to Left/Top/Width/Height as well.
    /// </summary>
    private Rect? WorkAreaFor(SessionState state)
    {
        if (double.IsNaN(state.WindowLeft) || double.IsNaN(state.WindowTop) ||
            double.IsNaN(state.WindowWidth) || double.IsNaN(state.WindowHeight))
        {
            return null;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        if (dpi.DpiScaleX <= 0 || dpi.DpiScaleY <= 0)
            return null;

        var rect = new NativeRect
        {
            Left = (int)(state.WindowLeft * dpi.DpiScaleX),
            Top = (int)(state.WindowTop * dpi.DpiScaleY),
            Right = (int)((state.WindowLeft + Math.Max(1, state.WindowWidth)) * dpi.DpiScaleX),
            Bottom = (int)((state.WindowTop + Math.Max(1, state.WindowHeight)) * dpi.DpiScaleY),
        };

        var monitor = NativeMethods.MonitorFromRect(rect, NativeMethods.MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero)
            return null;

        var info = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
            return null;

        return new Rect(
            info.rcWork.Left / dpi.DpiScaleX,
            info.rcWork.Top / dpi.DpiScaleY,
            (info.rcWork.Right - info.rcWork.Left) / dpi.DpiScaleX,
            (info.rcWork.Bottom - info.rcWork.Top) / dpi.DpiScaleY);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, Width, Height)
            : RestoreBounds;

        _session.WindowLeft = bounds.Left;
        _session.WindowTop = bounds.Top;
        _session.WindowWidth = bounds.Width;
        _session.WindowHeight = bounds.Height;
        _session.WindowMaximized = WindowState == WindowState.Maximized;
        _session.NavigationPaneWidth = NavigationColumn.ActualWidth;

        ViewModel.SaveSession(_session);
    }

    /// <summary>The blue "File" tab drops its menu down instead of switching ribbon pages.</summary>
    private void OnFileTabClick(object sender, RoutedEventArgs e)
    {
        if (FileTabButton.ContextMenu is not { } menu)
            return;

        menu.PlacementTarget = FileTabButton;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void OnCloseWindowClick(object sender, RoutedEventArgs e) => Close();

    // --- Shortcuts --------------------------------------------------------

    private void RegisterShortcuts()
    {
        Add(Key.T, ModifierKeys.Control, () => _ = ViewModel.NewTabAsync());
        Add(Key.W, ModifierKeys.Control, () => { if (Tab is not null) ViewModel.CloseTab(Tab); });
        Add(Key.N, ModifierKeys.Control | ModifierKeys.Shift, ViewModel.CreateFolder);
        Add(Key.Left, ModifierKeys.Alt, () => _ = Tab?.GoBackAsync());
        Add(Key.Right, ModifierKeys.Alt, () => _ = Tab?.GoForwardAsync());
        Add(Key.Up, ModifierKeys.Alt, () => _ = Tab?.GoUpAsync());
        Add(Key.F5, ModifierKeys.None, () => _ = Tab?.RefreshAsync());
        Add(Key.F, ModifierKeys.Control, () => SearchBox.Focus());
        Add(Key.L, ModifierKeys.Control, ShowAddressEditor);
        Add(Key.D, ModifierKeys.Control, () => OnFileShortcut(PinSelection));

        // The file commands also live on the list itself, but the list is not always the
        // focused element - after startup, or with the tree or a favourite focused, nothing
        // would react to Ctrl+V at all. When the list does have focus it marks the key as
        // handled first, so these never fire twice.
        Add(Key.C, ModifierKeys.Control, () => OnFileShortcut(ViewModel.CopySelection));
        Add(Key.X, ModifierKeys.Control, () => OnFileShortcut(ViewModel.CutSelection));
        Add(Key.V, ModifierKeys.Control, () => OnFileShortcut(ViewModel.Paste));
        Add(Key.Delete, ModifierKeys.None, () => OnFileShortcut(() => ViewModel.DeleteSelection(permanent: false)));
        Add(Key.Delete, ModifierKeys.Shift, () => OnFileShortcut(() => ViewModel.DeleteSelection(permanent: true)));
        Add(Key.F2, ModifierKeys.None, () => OnFileShortcut(RenameSelection));

        for (var i = 0; i < 9; i++)
        {
            var index = i;
            Add(Key.D1 + i, ModifierKeys.Control, () =>
            {
                if (index < ViewModel.Tabs.Count)
                    ViewModel.SelectedTab = ViewModel.Tabs[index];
            });
        }
    }

    private void Add(Key key, ModifierKeys modifiers, Action action)
        => InputBindings.Add(new KeyBinding(new RelayCommand(action), key, modifiers));

    /// <summary>
    /// Runs a file command unless a text box owns the keystroke - the address bar, the search
    /// box and the inline rename editor need their own Ctrl+V, Entf and F2.
    /// </summary>
    private void OnFileShortcut(Action action)
    {
        if (Keyboard.FocusedElement is TextBoxBase)
            return;

        action();
    }

    /// <summary>
    /// Pins the selected folder, or the open folder when nothing (or only files) is selected.
    /// </summary>
    private void PinSelection()
    {
        if (Tab is null)
            return;

        var folder = Tab.SelectedEntries.FirstOrDefault(entry => entry.IsDirectory)?.FullPath
            ?? Tab.CurrentPath;

        ViewModel.AddFavorite(folder);
    }

    private void RenameSelection()
    {
        if (Tab?.SelectedEntries.FirstOrDefault() is { } entry)
            entry.IsRenaming = true;
    }

    // --- Tabs -------------------------------------------------------------

    private void OnNewTabClick(object sender, RoutedEventArgs e) => _ = ViewModel.NewTabAsync();

    private void OnCloseTabClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ExplorerTabViewModel tab })
            ViewModel.CloseTab(tab);

        e.Handled = true;
    }

    private void OnTabStripMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Middle click closes a tab, as in every browser.
        if (e.ChangedButton == MouseButton.Middle)
        {
            if (ItemUnderMouse(e.OriginalSource) is { } tab)
            {
                ViewModel.CloseTab(tab);
                e.Handled = true;
            }

            return;
        }

        if (e.ChangedButton != MouseButton.Left)
            return;

        // Remember where a possible drag started; the drag itself only begins once the
        // pointer has moved past the system threshold, so plain clicks still select.
        _tabDragStart = e.GetPosition(TabStrip);
        _tabDragCandidate = ItemUnderMouse(e.OriginalSource);
    }

    private void OnTabStripMouseMove(object sender, MouseEventArgs e)
    {
        if (_tabDragCandidate is not { } tab || e.LeftButton != MouseButtonState.Pressed)
            return;

        var position = e.GetPosition(TabStrip);
        if (Math.Abs(position.X - _tabDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _tabDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _tabDragCandidate = null;

        var data = new DataObject(TabDragFormat, tab);
        DragDrop.DoDragDrop(TabStrip, data, DragDropEffects.Move);
    }

    private void OnTabStripDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(TabDragFormat) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnTabStripDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(TabDragFormat) is not ExplorerTabViewModel dragged)
            return;

        e.Handled = true;

        var from = ViewModel.Tabs.IndexOf(dragged);
        if (from < 0)
            return;

        // Dropping past the last tab appends; dropping on a tab takes its place.
        var target = ItemUnderMouse(e.OriginalSource);
        var to = target is null ? ViewModel.Tabs.Count - 1 : ViewModel.Tabs.IndexOf(target);

        if (to < 0 || to == from)
            return;

        ViewModel.Tabs.Move(from, to);
        ViewModel.SelectedTab = dragged;
    }

    private static ExplorerTabViewModel? ItemUnderMouse(object? source)
    {
        var element = source as DependencyObject;

        while (element is not null and not ListBoxItem)
            element = VisualTreeHelper.GetParent(element);

        return (element as ListBoxItem)?.DataContext as ExplorerTabViewModel;
    }

    // --- Navigation -------------------------------------------------------

    private void OnBackClick(object sender, RoutedEventArgs e) => _ = Tab?.GoBackAsync();

    private void OnForwardClick(object sender, RoutedEventArgs e) => _ = Tab?.GoForwardAsync();

    private void OnUpClick(object sender, RoutedEventArgs e) => _ = Tab?.GoUpAsync();

    private void OnRefreshClick(object sender, RoutedEventArgs e) => _ = Tab?.RefreshAsync();

    private void OnBreadcrumbClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string path })
            _ = Tab?.NavigateAsync(path);
    }

    /// <summary>Clicking the empty part of the breadcrumb turns it into an editable path box.</summary>
    private void OnBreadcrumbHostClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is ScrollViewer or Grid)
            ShowAddressEditor();
    }

    private void ShowAddressEditor()
    {
        BreadcrumbHost.Visibility = Visibility.Collapsed;
        AddressBox.Visibility = Visibility.Visible;
        AddressBox.Focus();
        AddressBox.SelectAll();
    }

    private void HideAddressEditor()
    {
        AddressBox.Visibility = Visibility.Collapsed;
        BreadcrumbHost.Visibility = Visibility.Visible;
    }

    private async void OnAddressBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (Tab is null)
            return;

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            var text = AddressBox.Text;
            HideAddressEditor();

            // The current folder stays visible; like Explorer, a typo gets a message instead.
            if (!await Tab.NavigateToAddressAsync(text) && !string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show(this, $"\"{text}\" was not found. Check the spelling and try again.",
                    "Foldra", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            AddressBox.Text = Tab.CurrentPath;
            HideAddressEditor();
        }
    }

    private void OnAddressBoxLostFocus(object sender, RoutedEventArgs e) => HideAddressEditor();

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _ = Tab?.RunSearchAsync();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (Tab is not null)
            {
                Tab.SearchQuery = string.Empty;
                _ = Tab.RefreshAsync();
            }
        }
    }

    // --- Sidebar ----------------------------------------------------------

    private void OnFavoriteClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string path })
            _ = Tab?.NavigateAsync(path);
    }

    private void OnFavoriteMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Middle click opens a favorite in a new tab, as in the file list.
        if (e.ChangedButton != MouseButton.Middle)
            return;

        if (sender is FrameworkElement { Tag: string path })
        {
            _ = ViewModel.NewTabAsync(path);
            e.Handled = true;
        }
    }

    private void OnFavoriteRightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: NavigationNodeViewModel node } element)
            return;

        // Without a placement target the menu inherits neither the theme resources nor the
        // position of the item it belongs to.
        var menu = new ContextMenu { PlacementTarget = element };

        var openInTab = new MenuItem { Header = "Open in new tab" };
        openInTab.Click += (_, _) => _ = ViewModel.NewTabAsync(node.Path);
        menu.Items.Add(openInTab);

        var remove = new MenuItem { Header = "Unpin from Quick access" };
        remove.Click += (_, _) => ViewModel.RemoveFavorite(node);
        menu.Items.Add(remove);

        menu.IsOpen = true;
        e.Handled = true;
    }

    private void OnTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is NavigationNodeViewModel node &&
            !string.Equals(node.Path, Tab?.CurrentPath, StringComparison.OrdinalIgnoreCase))
        {
            _ = Tab?.NavigateAsync(node.Path);
        }
    }

    /// <summary>
    /// Clicking the node that is already selected raises no SelectedItemChanged, so without
    /// this the tree could not take the tab back to a folder it has since navigated away from.
    /// </summary>
    private void OnTreeMouseUp(object sender, MouseButtonEventArgs e)
    {
        var element = e.OriginalSource as DependencyObject;
        while (element is not null and not TreeViewItem)
        {
            // The expander arrow only folds the node open or shut.
            if (element is ToggleButton)
                return;

            element = VisualTreeHelper.GetParent(element);
        }

        if ((element as TreeViewItem)?.DataContext is NavigationNodeViewModel { IsSelected: true } node &&
            !string.Equals(node.Path, Tab?.CurrentPath, StringComparison.OrdinalIgnoreCase))
        {
            _ = Tab?.NavigateAsync(node.Path);
        }
    }

    private void OnTreeMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Middle click opens a folder in a new tab, as in the file list.
        if (e.ChangedButton != MouseButton.Middle)
            return;

        var element = e.OriginalSource as DependencyObject;
        while (element is not null and not TreeViewItem)
            element = VisualTreeHelper.GetParent(element);

        if ((element as TreeViewItem)?.DataContext is NavigationNodeViewModel node)
        {
            _ = ViewModel.NewTabAsync(node.Path);
            e.Handled = true;
        }
    }

    // --- Commands ---------------------------------------------------------

    private void OnNewFolderClick(object sender, RoutedEventArgs e) => ViewModel.CreateFolder();

    private void OnCutClick(object sender, RoutedEventArgs e) => ViewModel.CutSelection();

    private void OnCopyClick(object sender, RoutedEventArgs e) => ViewModel.CopySelection();

    private void OnPasteClick(object sender, RoutedEventArgs e) => ViewModel.Paste();

    private void OnDeleteClick(object sender, RoutedEventArgs e)
        => ViewModel.DeleteSelection(permanent: (Keyboard.Modifiers & ModifierKeys.Shift) != 0);

    private void OnRenameClick(object sender, RoutedEventArgs e) => RenameSelection();

    private void OnSortClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag } && Enum.TryParse<SortColumn>(tag, out var column))
            Tab?.Sort(column);
    }

    private void OnViewModeClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag } && Enum.TryParse<ViewMode>(tag, out var mode) && Tab is not null)
            Tab.ViewMode = mode;
    }

    private void OnAddFavoriteClick(object sender, RoutedEventArgs e) => PinSelection();

    private void OnCopyPathClick(object sender, RoutedEventArgs e) => ViewModel.CopyPathOfSelection();

    private void OnOpenInExplorerClick(object sender, RoutedEventArgs e) => ViewModel.OpenInWindowsExplorer();

    private void OnOpenTerminalClick(object sender, RoutedEventArgs e) => ViewModel.OpenTerminalHere();
}




