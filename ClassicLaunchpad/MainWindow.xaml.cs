using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinRT.Interop;
using ClassicLaunchpad.Core;

namespace ClassicLaunchpad
{
    public partial class MainWindow : Window
    {
        #region Win32 P/Invoke & Constants

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string lpClassName, string? lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        // Win32 Constants
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
        private const int DWMWA_TRANSITIONS_FORCEDISABLED = 3;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        private const int DWMWCP_DONOTROUND = 1;
        private const int DWMSBT_TRANSIENTWINDOW = 3;

        private const uint MOD_ALT = 0x0001;
        private const uint MOD_NOREPEAT = 0x4000;
        private const uint VK_SPACE = 0x20;
        private const uint WM_HOTKEY = 0x0312;
        private const uint WM_ACTIVATE = 0x0006;
        private const int WA_INACTIVE = 0;

        private const int HOTKEY_ID = 1;
        private static readonly UIntPtr SUBCLASS_ID = new UIntPtr(101);

        public delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr idSubclass, IntPtr dwRefData);

        [DllImport("comctl32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc callback, UIntPtr idSubclass, IntPtr dwRefData);

        [DllImport("comctl32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RemoveWindowSubclass(IntPtr hWnd, SubclassProc callback, UIntPtr idSubclass);

        [DllImport("comctl32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        internal enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct AccentPolicy
        {
            public AccentState AccentState;
            public uint AccentFlags;
            public uint GradientColor;
            public uint AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public IntPtr SizeOfData;
        }

        internal enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19
        }

        #endregion

        private IntPtr _hwnd = IntPtr.Zero;
        private AppWindow? _appWindow;
        private SubclassProc? _subclassCallback;
        private bool _persistentMode = true;
        private static bool _isTaskbarHidden = false;

        private LaunchpadViewModel? _viewModel;
        private bool _isInitialized = false;
        private AppItem? _draggedItem;
        private AppItem? _draggedFolderItem;
        private bool _isFolderOverlayOpen = false;

        public MainWindow()
        {
            this.InitializeComponent();

            // 1. Register exit and crash safety handlers first to ensure taskbar recovery in case of initialization failure
            this.Closed += OnWindowClosed;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            if (Application.Current != null)
            {
                Application.Current.UnhandledException += OnAppUnhandledException;
            }

            if (this.Resources.TryGetValue("CloseFolderStoryboard", out var closeStoryboardObj) && 
                closeStoryboardObj is Microsoft.UI.Xaml.Media.Animation.Storyboard closeStoryboard)
            {
                closeStoryboard.Completed += (s, e) =>
                {
                    FolderOverlay.Visibility = Visibility.Collapsed;
                };
            }

            _hwnd = WindowNative.GetWindowHandle(this);
            if (_hwnd != IntPtr.Zero)
            {
                var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
                _appWindow = AppWindow.GetFromWindowId(windowId);

                ConfigureWindowStyling();
                PositionOnMonitor();
                HideTaskbar();

                // Prevent GC of delegate
                _subclassCallback = new SubclassProc(WindowSubclassCallback);
                SetWindowSubclass(_hwnd, _subclassCallback, SUBCLASS_ID, IntPtr.Zero);

                // Register global Alt + Space hotkey and check for failure
                bool registered = RegisterHotKey(_hwnd, HOTKEY_ID, MOD_ALT | MOD_NOREPEAT, VK_SPACE);
                if (!registered)
                {
                    System.Diagnostics.Debug.WriteLine("Warning: Failed to register global hotkey (Alt + Space).");
                }
            }

            InitializeLaunchpad();
        }

        private void InitializeLaunchpad()
        {
            var scanner = new AppScanner();
            var settingsStore = new SettingsStore();
            var searchEngine = new SearchEngine();

            _viewModel = new LaunchpadViewModel(scanner, settingsStore, searchEngine);

            this.Activated += async (s, e) =>
            {
                if (!_isInitialized && _viewModel != null)
                {
                    _isInitialized = true;
                    await _viewModel.InitializeAsync();
                    
                    if (this.Content is FrameworkElement contentElement)
                    {
                        contentElement.KeyDown += OnContentKeyDown;
                    }
                    this.SizeChanged += MainWindow_SizeChanged;

                    RefreshUI();
                }
            };
        }

        private void MainWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            RefreshUI();
        }

        private void RefreshUI()
        {
            if (_viewModel == null) return;

            // 1. Math visible
            if (_viewModel.IsMathVisible)
            {
                MathResultText.Text = _viewModel.MathResult;
                MathResultCard.Visibility = Visibility.Visible;
                AppsFlipView.Visibility = Visibility.Collapsed;
                PageDotsContainer.Visibility = Visibility.Collapsed;
                return;
            }
            else
            {
                MathResultCard.Visibility = Visibility.Collapsed;
                AppsFlipView.Visibility = Visibility.Visible;
                PageDotsContainer.Visibility = Visibility.Visible;
            }

            // 2. Folder overlay
            if (_viewModel.OpenFolder != null)
            {
                FolderNameTextBox.Text = _viewModel.OpenFolder.Name;
                BuildFolderAppsGrid();

                if (!_isFolderOverlayOpen)
                {
                    _isFolderOverlayOpen = true;
                    FolderOverlay.Visibility = Visibility.Visible;
                    if (this.Resources.TryGetValue("OpenFolderStoryboard", out var openStoryboardObj) && 
                        openStoryboardObj is Microsoft.UI.Xaml.Media.Animation.Storyboard openStoryboard)
                    {
                        openStoryboard.Begin();
                    }
                }
            }
            else
            {
                if (_isFolderOverlayOpen)
                {
                    _isFolderOverlayOpen = false;
                    if (this.Resources.TryGetValue("CloseFolderStoryboard", out var closeStoryboardObj) && 
                        closeStoryboardObj is Microsoft.UI.Xaml.Media.Animation.Storyboard closeStoryboard)
                    {
                        closeStoryboard.Begin();
                    }
                    else
                    {
                        FolderOverlay.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    FolderOverlay.Visibility = Visibility.Collapsed;
                }
            }

            // 3. Grid & Icon sizing based on Swift formulas
            double screenHeight = RootGrid.ActualHeight > 0 ? RootGrid.ActualHeight : 1080;
            bool isSmallScreen = screenHeight < 950;
            double rawIconSize = _viewModel.IconSize;
            double rawRowSpacing = isSmallScreen ? 22 : 32;
            double rawColumnSpacing = isSmallScreen ? 44 : 52;

            double maxGridHeight = screenHeight - 240;
            double numerator = Math.Max(100.0, maxGridHeight - _viewModel.Rows * 40);
            double denominator = Math.Max(1.0, _viewModel.Rows * rawIconSize + (_viewModel.Rows - 1) * rawRowSpacing);
            double scaleFactor = Math.Min(1.0, Math.Max(0.35, numerator / denominator));

            double iconSize = rawIconSize * scaleFactor;
            double rowSpacing = rawRowSpacing * scaleFactor;
            double columnSpacing = rawColumnSpacing * scaleFactor;

            // 4. Populate FlipView
            AppsFlipView.Items.Clear();

            for (int pageIndex = 0; pageIndex < _viewModel.TotalPages; pageIndex++)
            {
                var pageGrid = new Grid
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    RowSpacing = rowSpacing,
                    ColumnSpacing = columnSpacing
                };

                for (int c = 0; c < _viewModel.Columns; c++)
                {
                    pageGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(iconSize + 20) });
                }
                for (int r = 0; r < _viewModel.Rows; r++)
                {
                    pageGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(iconSize + 40) });
                }

                var itemsOnPage = _viewModel.GetItemsOnPage(pageIndex);
                for (int i = 0; i < itemsOnPage.Count; i++)
                {
                    var item = itemsOnPage[i];
                    int row = i / _viewModel.Columns;
                    int col = i % _viewModel.Columns;

                    var cell = CreateAppCell(item, pageIndex, i, iconSize);
                    Grid.SetRow(cell, row);
                    Grid.SetColumn(cell, col);
                    pageGrid.Children.Add(cell);
                }

                AppsFlipView.Items.Add(pageGrid);
            }

            if (AppsFlipView.SelectedIndex != _viewModel.CurrentPageIndex && _viewModel.CurrentPageIndex < AppsFlipView.Items.Count)
            {
                AppsFlipView.SelectedIndex = _viewModel.CurrentPageIndex;
            }

            // 5. Populate page dots
            PageDotsContainer.Children.Clear();
            if (_viewModel.TotalPages > 1)
            {
                for (int p = 0; p < _viewModel.TotalPages; p++)
                {
                    int pageIndex = p;
                    var dot = new Microsoft.UI.Xaml.Shapes.Ellipse
                    {
                        Width = 8,
                        Height = 8,
                        Fill = new SolidColorBrush(Microsoft.UI.Colors.White),
                        Opacity = pageIndex == _viewModel.CurrentPageIndex ? 1.0 : 0.4,
                        Margin = new Thickness(6, 0, 6, 0),
                        RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
                        RenderTransform = new ScaleTransform
                        {
                            ScaleX = pageIndex == _viewModel.CurrentPageIndex ? 1.2 : 1.0,
                            ScaleY = pageIndex == _viewModel.CurrentPageIndex ? 1.2 : 1.0
                        }
                    };
                    dot.PointerPressed += (s, e) =>
                    {
                        _viewModel.CurrentPageIndex = pageIndex;
                        _viewModel.SelectedItemIndex = 0;
                        RefreshUI();
                    };
                    PageDotsContainer.Children.Add(dot);
                }
            }
        }

        private void BuildFolderAppsGrid()
        {
            if (_viewModel == null || _viewModel.OpenFolder == null) return;

            FolderAppsGrid.Children.Clear();
            FolderAppsGrid.ColumnDefinitions.Clear();
            FolderAppsGrid.RowDefinitions.Clear();

            double screenHeight = RootGrid.ActualHeight > 0 ? RootGrid.ActualHeight : 1080;
            bool isSmallScreen = screenHeight < 950;
            double rawIconSize = _viewModel.IconSize;
            double rawRowSpacing = isSmallScreen ? 22 : 32;
            double rawColumnSpacing = isSmallScreen ? 44 : 52;

            double maxGridHeight = screenHeight - 240;
            double numerator = Math.Max(100.0, maxGridHeight - _viewModel.Rows * 40);
            double denominator = Math.Max(1.0, _viewModel.Rows * rawIconSize + (_viewModel.Rows - 1) * rawRowSpacing);
            double scaleFactor = Math.Min(1.0, Math.Max(0.35, numerator / denominator));

            double iconSize = rawIconSize * scaleFactor;
            double rowSpacing = rawRowSpacing * scaleFactor;
            double columnSpacing = rawColumnSpacing * scaleFactor;

            FolderAppsGrid.RowSpacing = rowSpacing;
            FolderAppsGrid.ColumnSpacing = columnSpacing;

            int folderCols = 4;
            for (int col = 0; col < folderCols; col++)
            {
                FolderAppsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(iconSize + 20) });
            }

            int numFolderRows = (_viewModel.OpenFolder.FolderItems.Count + folderCols - 1) / folderCols;
            for (int row = 0; row < numFolderRows; row++)
            {
                FolderAppsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(iconSize + 40) });
            }

            for (int i = 0; i < _viewModel.OpenFolder.FolderItems.Count; i++)
            {
                var app = _viewModel.OpenFolder.FolderItems[i];
                int row = i / folderCols;
                int col = i % folderCols;

                var cell = CreateFolderAppCell(app, i, iconSize);
                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, col);
                FolderAppsGrid.Children.Add(cell);
            }
        }

        private UIElement CreateAppCell(AppItem item, int pageIndex, int selectedIndex, double iconSize)
        {
            var button = new Button
            {
                Style = (Style)this.Resources["LaunchpadButtonStyle"],
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                DataContext = item
            };

            button.Click += OnAppCellClicked;

            button.CanDrag = true;
            button.AllowDrop = true;
            button.DragStarting += AppButton_DragStarting;
            button.DragOver += AppButton_DragOver;
            button.Drop += AppButton_Drop;

            var iconBorder = new Border
            {
                Width = iconSize,
                Height = iconSize,
                CornerRadius = new CornerRadius(16),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            bool isSelected = (_viewModel != null && pageIndex == _viewModel.CurrentPageIndex && selectedIndex == _viewModel.SelectedItemIndex);
            if (isSelected)
            {
                iconBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 10, 132, 255));
                iconBorder.BorderThickness = new Thickness(3);
                iconBorder.Padding = new Thickness(2);
            }
            else
            {
                iconBorder.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                iconBorder.BorderThickness = new Thickness(0);
            }

            if (item.IsFolder)
            {
                var folderGrid = new Grid
                {
                    Width = iconSize,
                    Height = iconSize,
                    Background = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                    CornerRadius = new CornerRadius(16),
                    Padding = new Thickness(6)
                };
                folderGrid.RowDefinitions.Add(new RowDefinition());
                folderGrid.RowDefinitions.Add(new RowDefinition());
                folderGrid.ColumnDefinitions.Add(new ColumnDefinition());
                folderGrid.ColumnDefinitions.Add(new ColumnDefinition());

                for (int subIdx = 0; subIdx < Math.Min(4, item.FolderItems.Count); subIdx++)
                {
                    var subItem = item.FolderItems[subIdx];
                    var subImage = new Image
                    {
                        Width = iconSize / 2.5,
                        Height = iconSize / 2.5,
                        Stretch = Stretch.Uniform
                    };
                    try
                    {
                        if (!string.IsNullOrEmpty(subItem.IconPath) && File.Exists(subItem.IconPath))
                        {
                            subImage.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(subItem.IconPath));
                        }
                    }
                    catch { }

                    int subRow = subIdx / 2;
                    int subCol = subIdx % 2;
                    Grid.SetRow(subImage, subRow);
                    Grid.SetColumn(subImage, subCol);
                    folderGrid.Children.Add(subImage);
                }
                iconBorder.Child = folderGrid;
            }
            else
            {
                var image = new Image
                {
                    Width = iconSize,
                    Height = iconSize,
                    Stretch = Stretch.UniformToFill
                };

                try
                {
                    if (!string.IsNullOrEmpty(item.IconPath) && File.Exists(item.IconPath))
                    {
                        image.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(item.IconPath));
                    }
                    else
                    {
                        iconBorder.Background = new SolidColorBrush(Color.FromArgb(255, 60, 60, 60));
                    }
                }
                catch
                {
                    iconBorder.Background = new SolidColorBrush(Color.FromArgb(255, 60, 60, 60));
                }

                iconBorder.Child = image;
            }

            var textBlock = new TextBlock
            {
                Text = item.Name,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.Medium,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 2,
                Height = 32,
                Margin = new Thickness(0, 8, 0, 0),
                VerticalAlignment = VerticalAlignment.Top
            };

            var stack = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(iconBorder);
            stack.Children.Add(textBlock);

            button.Content = stack;
            return button;
        }

        private UIElement CreateFolderAppCell(AppItem app, int index, double iconSize)
        {
            var button = new Button
            {
                Style = (Style)this.Resources["LaunchpadButtonStyle"],
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                DataContext = app
            };

            button.Click += OnAppCellClicked;

            button.CanDrag = true;
            button.DragStarting += FolderAppButton_DragStarting;

            var menuFlyout = new MenuFlyout();

            var launchItem = new MenuFlyoutItem { Text = "Launch App" };
            launchItem.Click += (s, e) =>
            {
                LaunchAppDirect(app);
            };
            menuFlyout.Items.Add(launchItem);

            var removeItem = new MenuFlyoutItem { Text = "Remove from Folder" };
            removeItem.Click += (s, e) =>
            {
                if (_viewModel != null)
                {
                    _viewModel.DragAppOutOfFolder(app);
                    RefreshUI();
                }
            };
            menuFlyout.Items.Add(removeItem);

            button.ContextFlyout = menuFlyout;

            var iconBorder = new Border
            {
                Width = iconSize,
                Height = iconSize,
                CornerRadius = new CornerRadius(16),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var image = new Image
            {
                Width = iconSize,
                Height = iconSize,
                Stretch = Stretch.UniformToFill
            };

            try
            {
                if (!string.IsNullOrEmpty(app.IconPath) && File.Exists(app.IconPath))
                {
                    image.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(app.IconPath));
                }
                else
                {
                    iconBorder.Background = new SolidColorBrush(Color.FromArgb(255, 60, 60, 60));
                }
            }
            catch
            {
                iconBorder.Background = new SolidColorBrush(Color.FromArgb(255, 60, 60, 60));
            }

            iconBorder.Child = image;

            var textBlock = new TextBlock
            {
                Text = app.Name,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.Medium,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 2,
                Height = 32,
                Margin = new Thickness(0, 8, 0, 0),
                VerticalAlignment = VerticalAlignment.Top
            };

            var stack = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(iconBorder);
            stack.Children.Add(textBlock);

            button.Content = stack;
            return button;
        }

        private void OnAppCellClicked(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;

            if (sender is FrameworkElement fe && fe.DataContext is AppItem app)
            {
                if (app.IsFolder)
                {
                    _viewModel.OpenFolderOverlay(app);
                    RefreshUI();
                }
                else
                {
                    LaunchAppDirect(app);
                }
            }
        }

        private void LaunchAppDirect(AppItem app)
        {
            if (_viewModel == null) return;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = app.TargetPath,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to launch app: {ex.Message}");
            }
            HideLaunchpad();
        }

        private void HandleViewModelAction()
        {
            if (_viewModel == null) return;

            if (_viewModel.LaunchedApp != null)
            {
                LaunchAppDirect(_viewModel.LaunchedApp);
            }

            if (_viewModel.ExecutedSystemAction != SystemActionType.None)
            {
                var executor = new SystemCommandExecutor();
                executor.Execute(_viewModel.ExecutedSystemAction);
                HideLaunchpad();
            }

            if (!_viewModel.IsVisible)
            {
                HideLaunchpad();
            }
        }

        private void OnContentKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (_viewModel == null) return;
            if (_viewModel.OpenFolder != null)
            {
                if (e.Key != Windows.System.VirtualKey.Escape && e.Key != Windows.System.VirtualKey.Enter)
                {
                    return;
                }
            }

            var key = e.Key;
            var focusedElement = FocusManager.GetFocusedElement(this.Content.XamlRoot);
            
            if (focusedElement == FolderNameTextBox)
            {
                if (key == Windows.System.VirtualKey.Enter || key == Windows.System.VirtualKey.Escape)
                {
                    // Let it fall through
                }
                else
                {
                    return;
                }
            }

            bool isAlphaNumeric = (key >= Windows.System.VirtualKey.A && key <= Windows.System.VirtualKey.Z) ||
                                 (key >= Windows.System.VirtualKey.Number0 && key <= Windows.System.VirtualKey.Number9) ||
                                 (key >= Windows.System.VirtualKey.NumberPad0 && key <= Windows.System.VirtualKey.NumberPad9) ||
                                 key == Windows.System.VirtualKey.Space;

            if (isAlphaNumeric && focusedElement != SearchBox)
            {
                SearchBox.Focus(FocusState.Programmatic);
                char? typedChar = null;
                if (key >= Windows.System.VirtualKey.A && key <= Windows.System.VirtualKey.Z)
                {
                    typedChar = (char)('A' + (key - Windows.System.VirtualKey.A));
                }
                else if (key >= Windows.System.VirtualKey.Number0 && key <= Windows.System.VirtualKey.Number9)
                {
                    typedChar = (char)('0' + (key - Windows.System.VirtualKey.Number0));
                }
                else if (key >= Windows.System.VirtualKey.NumberPad0 && key <= Windows.System.VirtualKey.NumberPad9)
                {
                    typedChar = (char)('0' + (key - Windows.System.VirtualKey.NumberPad0));
                }
                else if (key == Windows.System.VirtualKey.Space)
                {
                    typedChar = ' ';
                }

                if (typedChar != null)
                {
                    SearchBox.Text += typedChar.Value.ToString().ToLowerInvariant();
                    SearchBox.SelectionStart = SearchBox.Text.Length;
                    e.Handled = true;
                    return;
                }
            }

            switch (key)
            {
                case Windows.System.VirtualKey.Left:
                    _viewModel.MoveFocusLeft();
                    e.Handled = true;
                    RefreshUI();
                    break;
                case Windows.System.VirtualKey.Right:
                    _viewModel.MoveFocusRight();
                    e.Handled = true;
                    RefreshUI();
                    break;
                case Windows.System.VirtualKey.Up:
                    _viewModel.MoveFocusUp();
                    e.Handled = true;
                    RefreshUI();
                    break;
                case Windows.System.VirtualKey.Down:
                    _viewModel.MoveFocusDown();
                    e.Handled = true;
                    RefreshUI();
                    break;
                case Windows.System.VirtualKey.PageUp:
                    _viewModel.PrevPage();
                    e.Handled = true;
                    RefreshUI();
                    break;
                case Windows.System.VirtualKey.PageDown:
                    _viewModel.NextPage();
                    e.Handled = true;
                    RefreshUI();
                    break;
                case Windows.System.VirtualKey.Enter:
                    _viewModel.PressEnter();
                    e.Handled = true;
                    HandleViewModelAction();
                    RefreshUI();
                    break;
                case Windows.System.VirtualKey.Escape:
                    _viewModel.PressEscape();
                    e.Handled = true;
                    HandleViewModelAction();
                    RefreshUI();
                    break;
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_viewModel == null) return;
            string query = SearchBox.Text;
            _viewModel.UpdateSearch(query);
            ClearSearchButton.Visibility = string.IsNullOrEmpty(query) ? Visibility.Collapsed : Visibility.Visible;
            RefreshUI();
        }

        private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = string.Empty;
        }

        private void SearchBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (_viewModel == null) return;

            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (_viewModel.IsMathVisible)
                {
                    CopyMathResultToClipboard();
                    e.Handled = true;
                    return;
                }

                if (_viewModel.PendingSystemAction != SystemActionType.None)
                {
                    var executor = new SystemCommandExecutor();
                    executor.Execute(_viewModel.PendingSystemAction);
                    HideLaunchpad();
                    e.Handled = true;
                    return;
                }

                if (_viewModel.DisplayedItems.Count > 0)
                {
                    var first = _viewModel.DisplayedItems[0];
                    if (first.IsFolder)
                    {
                        _viewModel.OpenFolderOverlay(first);
                        RefreshUI();
                    }
                    else
                    {
                        LaunchAppDirect(first);
                    }
                    e.Handled = true;
                }
            }
        }

        private void CopyMathResultToClipboard()
        {
            if (_viewModel == null) return;
            try
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText(_viewModel.MathResult);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
            }
            catch { }
        }

        private void MathResultCard_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            CopyMathResultToClipboard();
            e.Handled = true;
        }

        private void FolderOverlay_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (e.OriginalSource == FolderOverlay && _viewModel != null)
            {
                _viewModel.CloseFolderOverlay();
                RefreshUI();
                e.Handled = true;
            }
        }

        private void FolderNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitFolderRename();
        }

        private void FolderNameTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                CommitFolderRename();
                e.Handled = true;
                FolderNameTextBox.Focus(FocusState.Unfocused);
            }
        }

        private void CommitFolderRename()
        {
            if (_viewModel != null && _viewModel.OpenFolder != null)
            {
                if (string.IsNullOrWhiteSpace(FolderNameTextBox.Text))
                {
                    FolderNameTextBox.Text = _viewModel.OpenFolder.Name;
                }
                else
                {
                    _viewModel.RenameFolder(FolderNameTextBox.Text);
                    RefreshUI();
                }
            }
        }

        private void AppsFlipView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_viewModel != null && _viewModel.CurrentPageIndex != AppsFlipView.SelectedIndex && AppsFlipView.SelectedIndex >= 0)
            {
                _viewModel.CurrentPageIndex = AppsFlipView.SelectedIndex;
                _viewModel.SelectedItemIndex = 0;
                RefreshUI();
            }
        }

        private void ConfigureWindowStyling()
        {
            if (_appWindow != null && _appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
                presenter.IsResizable = false;
                presenter.IsMinimizable = false;
                presenter.IsMaximizable = false;
                presenter.IsAlwaysOnTop = true;
            }

            // Disable OS transitions
            int disableTransitions = 1;
            DwmSetWindowAttribute(_hwnd, DWMWA_TRANSITIONS_FORCEDISABLED, ref disableTransitions, sizeof(int));

            // Prevent corner rounding
            int cornerPreference = DWMWCP_DONOTROUND;
            DwmSetWindowAttribute(_hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));

            // Backdrop
            ApplyBackdrop();
        }

        private void ApplyBackdrop()
        {
            if (Environment.OSVersion.Version.Major >= 10)
            {
                // Check if Windows 11 Build 22621+ (22H2)
                if (Environment.OSVersion.Version.Build >= 22621)
                {
                    int backdropType = DWMSBT_TRANSIENTWINDOW;
                    DwmSetWindowAttribute(_hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
                }
                else
                {
                    var accent = new AccentPolicy
                    {
                        AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                        GradientColor = 0x99000000
                    };
                    int accentStructSize = Marshal.SizeOf(accent);
                    IntPtr accentPtr = Marshal.AllocHGlobal(accentStructSize);
                    try
                    {
                        Marshal.StructureToPtr(accent, accentPtr, false);
                        var data = new WindowCompositionAttributeData
                        {
                            Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                            SizeOfData = new IntPtr(accentStructSize),
                            Data = accentPtr
                        };
                        SetWindowCompositionAttribute(_hwnd, ref data);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(accentPtr);
                    }
                }
            }
        }

        private void PositionOnMonitor()
        {
            if (_appWindow != null && GetCursorPos(out POINT cursorPoint))
            {
                IntPtr hMonitor = MonitorFromPoint(cursorPoint, MONITOR_DEFAULTTONEAREST);
                var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(hMonitor, ref monitorInfo))
                {
                    var rect = monitorInfo.rcMonitor;
                    _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(
                        rect.left,
                        rect.top,
                        rect.right - rect.left,
                        rect.bottom - rect.top
                    ));
                }
            }
        }

        private void ShowLaunchpad()
        {
            PositionOnMonitor();
            HideTaskbar();
            if (_appWindow != null)
            {
                _appWindow.Show();
            }
            this.Activate();
            SetForegroundWindow(_hwnd);
        }

        private void HideLaunchpad()
        {
            if (_appWindow != null)
            {
                _appWindow.Hide();
            }
            ShowTaskbar();
        }

        private void ToggleLaunchpad()
        {
            if (_appWindow != null && _appWindow.IsVisible)
            {
                HideLaunchpad();
            }
            else
            {
                ShowLaunchpad();
            }
        }

        private void OnFocusLost()
        {
            if (_persistentMode)
            {
                HideLaunchpad();
            }
            else
            {
                DismissAndExit();
            }
        }

        private void DismissAndExit()
        {
            CleanupNativeHooks();
            ShowTaskbar();
            Application.Current.Exit();
        }

        private void CleanupNativeHooks()
        {
            if (_hwnd != IntPtr.Zero)
            {
                UnregisterHotKey(_hwnd, HOTKEY_ID);
                if (_subclassCallback != null)
                {
                    RemoveWindowSubclass(_hwnd, _subclassCallback, SUBCLASS_ID);
                }
            }
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            CleanupNativeHooks();
            ShowTaskbar();
            CleanupAppDomainEvents();
        }

        private void OnProcessExit(object? sender, EventArgs e) => ShowTaskbar();
        private void OnUnhandledException(object sender, System.UnhandledExceptionEventArgs e) => ShowTaskbar();
        private void OnAppUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e) => ShowTaskbar();

        private void CleanupAppDomainEvents()
        {
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
            if (Application.Current != null)
            {
                Application.Current.UnhandledException -= OnAppUnhandledException;
            }
        }

        private IntPtr WindowSubclassCallback(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr idSubclass, IntPtr dwRefData)
        {
            if (uMsg == WM_HOTKEY)
            {
                if (wParam.ToInt32() == HOTKEY_ID)
                {
                    ToggleLaunchpad();
                    return IntPtr.Zero;
                }
            }
            else if (uMsg == WM_ACTIVATE)
            {
                int activationState = wParam.ToInt32() & 0xFFFF;
                if (activationState == WA_INACTIVE)
                {
                    OnFocusLost();
                }
            }
            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        #region Taskbar Management API

        private static List<IntPtr> GetTaskbarHandles()
        {
            var handles = new List<IntPtr>();
            IntPtr primary = FindWindow("Shell_TrayWnd", null);
            if (primary != IntPtr.Zero)
            {
                handles.Add(primary);
            }
            IntPtr secondary = IntPtr.Zero;
            while ((secondary = FindWindowEx(IntPtr.Zero, secondary, "SecondaryTrayWnd", null)) != IntPtr.Zero)
            {
                handles.Add(secondary);
            }
            return handles;
        }

        public static void HideTaskbar()
        {
            if (_isTaskbarHidden) return;
            foreach (var hwnd in GetTaskbarHandles())
            {
                ShowWindow(hwnd, SW_HIDE);
            }
            _isTaskbarHidden = true;
        }

        public static void ShowTaskbar()
        {
            foreach (var hwnd in GetTaskbarHandles())
            {
                ShowWindow(hwnd, SW_SHOW);
            }
            _isTaskbarHidden = false;
        }

        #endregion

        #region Drag and Drop Helpers

        private void AppButton_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            if (sender is FrameworkElement fe && fe.DataContext is AppItem app)
            {
                _draggedItem = app;
                args.Data.SetText(app.Id);
            }
        }

        private void AppButton_DragOver(object sender, DragEventArgs args)
        {
            if (sender is FrameworkElement fe && fe.DataContext is AppItem targetApp)
            {
                if (_draggedItem != null && 
                    _draggedItem != targetApp && 
                    !_draggedItem.IsFolder && 
                    !targetApp.IsFolder)
                {
                    args.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
                }
                else
                {
                    args.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
                }
            }
        }

        private void AppButton_Drop(object sender, DragEventArgs args)
        {
            if (_viewModel != null && sender is FrameworkElement fe && fe.DataContext is AppItem targetApp)
            {
                if (_draggedItem != null)
                {
                    _viewModel.CreateFolder(_draggedItem, targetApp);
                    _draggedItem = null;
                    RefreshUI();
                }
            }
        }

        private void FolderAppButton_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            if (sender is FrameworkElement fe && fe.DataContext is AppItem app)
            {
                _draggedFolderItem = app;
                args.Data.SetText(app.Id);
            }
        }

        private void FolderOverlay_DragOver(object sender, DragEventArgs e)
        {
            if (_draggedFolderItem != null)
            {
                e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
            }
            else
            {
                e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
            }
        }

        private void FolderOverlay_Drop(object sender, DragEventArgs e)
        {
            if (_viewModel != null && _draggedFolderItem != null)
            {
                _viewModel.DragAppOutOfFolder(_draggedFolderItem);
                _draggedFolderItem = null;
                RefreshUI();
            }
        }

        private void FolderCardBorder_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
            e.Handled = true;
        }

        private void FolderCardBorder_Drop(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
            e.Handled = true;
        }

        #endregion
    }
}
