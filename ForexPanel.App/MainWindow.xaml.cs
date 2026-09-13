using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ForexPanel.App.ChartLayout;
using ForexPanel.App.Theme;
using ForexPanel.Core;

namespace ForexPanel.App;

public partial class MainWindow : Window
{
    private const double InitialBarSpacing = 10.0;

    private readonly Mt4PipeServer mt4PipeServer;
    private readonly ChartController chartController;
    private readonly CandleLayoutModel candleLayoutModel;
    private int candleMinutes = 15;
    private string currentSymbol = "EURUSD";
    private bool crosshairEnabled;
    private bool candleLayoutInitialized;

    private bool zoomAreaMode;
    private bool zoomAreaDragging;
    private ScottPlot.Pixel zoomAreaStartPixel;

    public MainWindow()
    {
        InitializeComponent();
        InitializeTitleBarIcons();
        ApplyMenuInteractionSizing();
        ThemeManager.Apply(ForexPanel.App.Theme.ThemeMode.System);
        MainMenu.AddHandler(MenuItem.SubmenuOpenedEvent, new RoutedEventHandler(MainMenu_SubmenuOpened));
        mt4PipeServer = new Mt4PipeServer();
        mt4PipeServer.MessageReceived += Mt4PipeServer_MessageReceived;
        chartController = new ChartController(Chart, UpdateCandleInfo);
        candleLayoutModel = new CandleLayoutModel(0, InitialBarSpacing);
        Chart.SizeChanged += Chart_SizeChanged;
        Chart.MouseLeftButtonUp += Chart_MouseLeftButtonUp;
        Chart.AddHandler(UIElement.MouseWheelEvent, new MouseWheelEventHandler(Chart_MouseWheel), true);
        ApplyInitialCandleViewport();
        Chart.Menu = new StyledChartMenu(Chart);
        ApplyChartSurfaceTheme();

        MainToolbar.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(MainToolbar_ButtonClick));
        Chart.MouseMove += MainWindow_ChartMouseMove;
        Chart.PreviewMouseLeftButtonDown += Chart_PreviewMouseLeftButtonDown;
        Chart.PreviewMouseMove += Chart_PreviewMouseMove;
        Chart.PreviewMouseLeftButtonUp += Chart_PreviewMouseLeftButtonUp;
        ApplyCrosshairVisibility();

        MainToolbar.SymbolChanged += (_, symbol) =>
        {
            CancelZoomAreaMode();
            currentSymbol = symbol;
            chartController.Rebuild(currentSymbol, candleMinutes);
            ApplyInitialCandleViewport();
            ApplyCrosshairVisibility();
        };
        MainToolbar.TimeframeChanged += (_, timeframe) =>
        {
            CancelZoomAreaMode();
            candleMinutes = timeframe switch
            {
                "M1" => 1, "M5" => 5, "M15" => 15, "M30" => 30,
                "H1" => 60, "H4" => 240, "D1" => 1440, _ => candleMinutes
            };
            chartController.Rebuild(currentSymbol, candleMinutes);
            ApplyInitialCandleViewport();
            ApplyCrosshairVisibility();
        };

        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
        StateChanged += MainWindow_StateChanged;
        UpdateThemeMenuChecks();
    }

    private void ApplyInitialCandleViewport()
    {
        if (Chart == null)
            return;

        var width = Chart.ActualWidth;
        if (width <= 0)
            return;

        candleLayoutModel.SetChartWidth(width);
        candleLayoutModel.SetBarSpacing(InitialBarSpacing);
        candleLayoutModel.SetRightOffset(0);
        candleLayoutInitialized = true;
        ApplyCandleLayoutToChart(DateTime.Now.ToOADate());
    }

    private void Chart_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!candleLayoutInitialized || Chart.ActualWidth <= 0)
            return;

        SyncCandleLayoutFromChart();

        var currentLimits = Chart.Plot.Axes.GetLimits();
        var right = currentLimits.Right;
        if (double.IsNaN(right) || double.IsInfinity(right))
            right = DateTime.Now.ToOADate();

        candleLayoutModel.SetChartWidth(Chart.ActualWidth);
        ApplyCandleLayoutToChart(right);
    }

    private void ApplyCandleLayoutToChart(double right)
    {
        var intervalDays = candleMinutes / (24.0 * 60.0);
        var spanDays = candleLayoutModel.VisibleBars * intervalDays;
        var left = right - spanDays;

        Chart.Plot.Axes.SetLimitsX(left, right);
        Chart.Refresh();
    }

    private void SyncCandleLayoutFromChart()
    {
        if (!candleLayoutInitialized || Chart.ActualWidth <= 0)
            return;

        var limits = Chart.Plot.Axes.GetLimits();
        var spanDays = Math.Abs(limits.Right - limits.Left);
        var intervalDays = candleMinutes / (24.0 * 60.0);

        if (spanDays > 0 && intervalDays > 0)
        {
            var visibleBars = spanDays / intervalDays;
            var barSpacing = Chart.ActualWidth / visibleBars;

            if (!double.IsNaN(barSpacing) && !double.IsInfinity(barSpacing))
                candleLayoutModel.SetBarSpacing(barSpacing);
        }

        if (intervalDays > 0)
        {
            var latest = DateTime.Now.ToOADate();
            var rightOffset = Math.Max(0, (latest - limits.Right) / intervalDays);
            if (!double.IsNaN(rightOffset) && !double.IsInfinity(rightOffset))
                candleLayoutModel.SetRightOffset(rightOffset);
        }
    }

    private void Chart_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (zoomAreaMode)
            return;

        Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(SyncCandleLayoutFromChart));
    }

    private void Chart_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(SyncCandleLayoutFromChart));
    }

    private void MainToolbar_ButtonClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not ButtonBase button)
            return;

        if (button.Tag is not ForexPanel.App.Toolbar.ToolbarTool tool)
            return;

        if (string.Equals(tool.Id, "ZoomIn", StringComparison.Ordinal))
        {
            CancelZoomAreaMode();
            SyncCandleLayoutFromChart();
            var limits = Chart.Plot.Axes.GetLimits();
            candleLayoutModel.Zoom(1.20);
            ApplyCandleLayoutToChart(limits.Right);
            return;
        }

        if (string.Equals(tool.Id, "ZoomOut", StringComparison.Ordinal))
        {
            CancelZoomAreaMode();
            SyncCandleLayoutFromChart();
            var limits = Chart.Plot.Axes.GetLimits();
            candleLayoutModel.Zoom(1.0 / 1.20);
            ApplyCandleLayoutToChart(limits.Right);
            return;
        }

        if (string.Equals(tool.Id, "ZoomArea", StringComparison.Ordinal))
        {
            BeginZoomAreaMode();
            return;
        }

        if (string.Equals(tool.Id, "Crosshair", StringComparison.Ordinal))
        {
            crosshairEnabled = !crosshairEnabled;
            var activeBackground = TryFindResource("Color.Toolbar.ButtonPressed") as Brush;
            var activeBorder = TryFindResource("Color.Toolbar.ButtonPressedBorder") as Brush;
            button.Background = crosshairEnabled && activeBackground != null ? activeBackground : Brushes.Transparent;
            button.BorderBrush = crosshairEnabled && activeBorder != null ? activeBorder : Brushes.Transparent;
            ApplyCrosshairVisibility();
            return;
        }

        if (!string.Equals(tool.Id, "Grid", StringComparison.Ordinal))
            return;

        chartController.SetGridEnabled(!chartController.GridEnabled);

        var gridActiveBackground = TryFindResource("Color.Toolbar.ButtonPressed") as Brush;
        var gridActiveBorder = TryFindResource("Color.Toolbar.ButtonPressedBorder") as Brush;
        button.Background = chartController.GridEnabled && gridActiveBackground != null
            ? gridActiveBackground
            : Brushes.Transparent;
        button.BorderBrush = chartController.GridEnabled && gridActiveBorder != null
            ? gridActiveBorder
            : Brushes.Transparent;
    }

    private void BeginZoomAreaMode()
    {
        CancelZoomAreaMode();
        zoomAreaMode = true;
        Chart.Focus();
        Chart.Plot.ZoomRectangle.IsVisible = false;
    }

    private void CancelZoomAreaMode()
    {
        if (zoomAreaDragging && Chart.IsMouseCaptured)
            Chart.ReleaseMouseCapture();

        zoomAreaDragging = false;
        zoomAreaMode = false;
        Chart.Plot.ZoomRectangle.IsVisible = false;
        Chart.Refresh();
    }

    private void Chart_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!zoomAreaMode)
            return;

        var point = e.GetPosition(Chart);
        zoomAreaStartPixel = ToScottPlotPixel(point);
        zoomAreaDragging = true;
        Chart.CaptureMouse();
        Chart.Plot.ZoomRectangle.MouseDown = zoomAreaStartPixel;
        Chart.Plot.ZoomRectangle.MouseUp = zoomAreaStartPixel;
        Chart.Plot.ZoomRectangle.IsVisible = true;
        Chart.Refresh();
        e.Handled = true;
    }

    private void Chart_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!zoomAreaMode || !zoomAreaDragging)
            return;

        var point = e.GetPosition(Chart);
        var currentPixel = ToScottPlotPixel(point);
        Chart.Plot.ZoomRectangle.MouseDown = zoomAreaStartPixel;
        Chart.Plot.ZoomRectangle.MouseUp = currentPixel;
        Chart.Plot.ZoomRectangle.IsVisible = true;
        Chart.Refresh();
        e.Handled = true;
    }

    private void Chart_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!zoomAreaMode || !zoomAreaDragging)
            return;

        var point = e.GetPosition(Chart);
        var endPixel = ToScottPlotPixel(point);
        var startPixel = zoomAreaStartPixel;

        Chart.ReleaseMouseCapture();
        zoomAreaDragging = false;
        zoomAreaMode = false;
        Chart.Plot.ZoomRectangle.IsVisible = false;

        var width = Math.Abs(endPixel.X - startPixel.X);
        var height = Math.Abs(endPixel.Y - startPixel.Y);

        if (width >= 5 && height >= 5)
        {
            Chart.Plot.Axes.Zoom(startPixel, endPixel);
            SyncCandleLayoutFromChart();
            Chart.Refresh();
        }
        else
        {
            Chart.Refresh();
        }

        e.Handled = true;
    }

    private ScottPlot.Pixel ToScottPlotPixel(Point point)
    {
        var scale = Chart.DisplayScale;
        return new ScottPlot.Pixel((float)(point.X * scale), (float)(point.Y * scale));
    }

    private void MainWindow_ChartMouseMove(object? sender, MouseEventArgs e)
    {
        if (!crosshairEnabled)
            ApplyCrosshairVisibility();
    }

    private void ApplyCrosshairVisibility()
    {
        if (Chart == null)
            return;

        var crosshairs = Chart.Plot.GetPlottables().OfType<ScottPlot.Plottables.Crosshair>();
        foreach (var item in crosshairs)
            item.IsVisible = crosshairEnabled;

        Chart.Refresh();
    }

    private void InitializeTitleBarIcons()
    {
        MinimizeButton.Content = CreateCaptionIcon("M2,8 L14,8");
        SetMaximizeCaptionIcon();
        CloseButton.Content = CreateCaptionIcon("M3,3 L13,13 M13,3 L3,13");
    }

    private static Path CreateCaptionIcon(string geometry)
    {
        var path = new Path
        {
            Data = Geometry.Parse(geometry),
            Width = 12,
            Height = 12,
            StrokeThickness = 1.6,
            Stretch = Stretch.Uniform,
            Fill = Brushes.Transparent
        };

        path.SetBinding(
            Shape.StrokeProperty,
            new Binding(nameof(Foreground))
            {
                RelativeSource = new RelativeSource(
                    RelativeSourceMode.FindAncestor,
                    typeof(Button),
                    1)
            });

        return path;
    }

    private void SetMaximizeCaptionIcon()
    {
        MaximizeButton.Content = CreateCaptionIcon(
            WindowState == WindowState.Maximized
                ? "M5,3 L13,3 L13,11 L5,11 Z M3,5 L3,13 L11,13"
                : "M2,2 L14,2 L14,14 L2,14 Z");
    }

    private void ApplyMenuInteractionSizing()
    {
        ApplyMenuItemSizing(MainMenu, 0);
    }

    private static void ApplyMenuItemSizing(ItemsControl parent, int depth)
    {
        foreach (var item in parent.Items)
        {
            if (item is not MenuItem menuItem)
                continue;

            if (depth == 0)
            {
                menuItem.MinHeight = 28;
                menuItem.Padding = new Thickness(10, 0, 10, 0);
            }
            else
            {
                menuItem.MinHeight = 30;
                menuItem.Padding = new Thickness(10, 5, 10, 5);
            }

            ApplyMenuItemSizing(menuItem, depth + 1);
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateThemeMenuChecks();
        ApplyChartSurfaceTheme();
        ApplyCrosshairVisibility();
        ApplyMaximizeBounds();
        SetMaximizeCaptionIcon();
        ApplyMenuInteractionSizing();
        ApplyInitialCandleViewport();
        try
        {
            await mt4PipeServer.StartAsync();
            Debug.WriteLine("ForexPanel MT4 Pipe Server started.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"MT4 Pipe Server error: {ex.Message}");
        }
    }

    private async void MainWindow_Closed(object? sender, EventArgs e)
    {
        try
        {
            CancelZoomAreaMode();
            MainToolbar.SaveLayout();
            await mt4PipeServer.DisposeAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"MT4 Pipe Server shutdown error: {ex.Message}");
        }
    }

    private void ApplyTheme(ForexPanel.App.Theme.ThemeMode mode)
    {
        ThemeManager.Apply(mode);
        ApplyChartSurfaceTheme();
        RefreshToggleToolbarButtonBackgrounds();
        UpdateThemeMenuChecks();
    }

    /// <summary>
    /// Re-applies the active/highlighted background+border for toggle-style toolbar buttons
    /// (Crosshair, Grid) using the CURRENT theme's resources. These are set imperatively via
    /// TryFindResource rather than {DynamicResource ...} bindings, so unlike most of the UI
    /// they do not update automatically when the theme changes - without this, a button left
    /// in its "selected" state keeps the old theme's highlight color after switching themes.
    /// </summary>
    private void RefreshToggleToolbarButtonBackgrounds()
    {
        var activeBackground = TryFindResource("Color.Toolbar.ButtonPressed") as Brush;
        var activeBorder = TryFindResource("Color.Toolbar.ButtonPressedBorder") as Brush;

        ApplyToggleButtonVisual("Crosshair", crosshairEnabled, activeBackground, activeBorder);
        ApplyToggleButtonVisual("Grid", chartController.GridEnabled, activeBackground, activeBorder);
    }

    private void ApplyToggleButtonVisual(string toolId, bool isActive, Brush? activeBackground, Brush? activeBorder)
    {
        if (MainToolbar == null)
            return;

        foreach (var button in FindButtonsByToolId(MainToolbar, toolId))
        {
            button.Background = isActive && activeBackground != null ? activeBackground : Brushes.Transparent;
            button.BorderBrush = isActive && activeBorder != null ? activeBorder : Brushes.Transparent;
        }
    }

    private static IEnumerable<ButtonBase> FindButtonsByToolId(DependencyObject root, string toolId)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);

            if (child is ButtonBase button &&
                button.Tag is ForexPanel.App.Toolbar.ToolbarTool tool &&
                string.Equals(tool.Id, toolId, StringComparison.Ordinal))
            {
                yield return button;
            }

            foreach (var nested in FindButtonsByToolId(child, toolId))
                yield return nested;
        }
    }

    private void MainMenu_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not MenuItem menuItem)
            return;

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() => ApplyOpenedSubmenuTheme(menuItem)));
    }

    private void ApplyOpenedSubmenuTheme(MenuItem menuItem)
    {
        var background = TryFindResource("Menu.Static.Background") as Brush;
        var borderBrush = TryFindResource("Menu.Static.Border") as Brush;

        if (background == null)
            return;

        if (menuItem.Template.FindName("SubMenuBorder", menuItem) is Border submenuBorder)
        {
            submenuBorder.Background = background;
            submenuBorder.MinWidth = 190;
            submenuBorder.Padding = new Thickness(2);
            if (borderBrush != null)
                submenuBorder.BorderBrush = borderBrush;

            HideDefaultSubmenuDecorations(submenuBorder);
        }

        if (menuItem.Template.FindName("NestedSubMenuBorder", menuItem) is Border nestedSubmenuBorder)
        {
            nestedSubmenuBorder.Background = background;
            nestedSubmenuBorder.MinWidth = 190;
            nestedSubmenuBorder.Padding = new Thickness(2);
            if (borderBrush != null)
                nestedSubmenuBorder.BorderBrush = borderBrush;
            HideDefaultSubmenuDecorations(nestedSubmenuBorder);
        }

        if (menuItem.Template.FindName("OpaqueRect", menuItem) is Rectangle opaqueRect)
            opaqueRect.Fill = background;
    }

    private static void HideDefaultSubmenuDecorations(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);

            if (child is Rectangle rectangle && IsDefaultSubmenuDecoration(rectangle))
            {
                rectangle.Visibility = Visibility.Collapsed;
            }
            else
            {
                HideDefaultSubmenuDecorations(child);
            }
        }
    }

    private static bool IsDefaultSubmenuDecoration(Rectangle rectangle)
    {
        var margin = rectangle.Margin;

        if (Math.Abs(rectangle.Width - 28) < 0.1 &&
            Math.Abs(margin.Left - 1) < 0.1 &&
            Math.Abs(margin.Top - 2) < 0.1)
        {
            return true;
        }

        if (Math.Abs(rectangle.Width - 1) < 0.1 &&
            Math.Abs(margin.Top - 2) < 0.1 &&
            (Math.Abs(margin.Left - 29) < 0.1 || Math.Abs(margin.Left - 30) < 0.1))
        {
            return true;
        }

        return false;
    }

    private void ApplyChartSurfaceTheme()
    {
        if (Chart == null)
            return;

        var backgroundBrush = TryFindResource("Color.App.Background") as System.Windows.Media.SolidColorBrush;
        var gridBrush = TryFindResource("Color.Chart.Grid") as System.Windows.Media.SolidColorBrush;
        var textBrush = TryFindResource("Color.Chart.Text") as System.Windows.Media.SolidColorBrush;
        var candleUpBrush = TryFindResource("Color.Chart.CandleUp") as System.Windows.Media.SolidColorBrush;
        var candleDownBrush = TryFindResource("Color.Chart.CandleDown") as System.Windows.Media.SolidColorBrush;
        var crosshairBrush = TryFindResource("Color.Chart.Crosshair") as System.Windows.Media.SolidColorBrush;

        if (backgroundBrush != null)
        {
            var backgroundColor = ScottPlot.Color.FromARGB(
                backgroundBrush.Color.A << 24 |
                backgroundBrush.Color.R << 16 |
                backgroundBrush.Color.G << 8 |
                backgroundBrush.Color.B);
            Chart.Background = backgroundBrush;
            Chart.Plot.FigureBackground.Color = backgroundColor;
            Chart.Plot.DataBackground.Color = backgroundColor;
        }

        if (gridBrush != null)
        {
            var gridColor = ScottPlot.Color.FromARGB(
                gridBrush.Color.A << 24 |
                gridBrush.Color.R << 16 |
                gridBrush.Color.G << 8 |
                gridBrush.Color.B);
            Chart.Plot.Grid.MajorLineColor = gridColor;
            Chart.Plot.Grid.MajorLineWidth = 1;
        }

        // Axis text, candle up/down, and crosshair colors are owned by ChartController so
        // they get re-applied automatically on every Rebuild (timeframe/symbol change) too -
        // previously these reset to hardcoded/default colors after every Rebuild.
        chartController.ApplyTheme(
            axisText: (textBrush ?? System.Windows.Media.Brushes.White).Color,
            candleUp: (candleUpBrush ?? System.Windows.Media.Brushes.LimeGreen).Color,
            candleDown: (candleDownBrush ?? System.Windows.Media.Brushes.OrangeRed).Color,
            crosshairColor: (crosshairBrush ?? System.Windows.Media.Brushes.Gray).Color);

        Chart.Refresh();
    }

    private void ApplyMaximizeBounds()
    {
        if (WindowState == WindowState.Maximized)
        {
            MaxWidth = SystemParameters.WorkArea.Width;
            MaxHeight = SystemParameters.WorkArea.Height;
        }
        else
        {
            MaxWidth = double.PositiveInfinity;
            MaxHeight = double.PositiveInfinity;
        }
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        ApplyMaximizeBounds();
        SetMaximizeCaptionIcon();
    }

    private void UpdateThemeMenuChecks()
    {
        if (LightThemeMenuItem == null || DarkThemeMenuItem == null || SystemThemeMenuItem == null)
            return;

        LightThemeMenuItem.IsCheckable = true;
        DarkThemeMenuItem.IsCheckable = true;
        SystemThemeMenuItem.IsCheckable = true;
        LightThemeMenuItem.IsChecked = ThemeManager.CurrentMode == ForexPanel.App.Theme.ThemeMode.Light;
        DarkThemeMenuItem.IsChecked = ThemeManager.CurrentMode == ForexPanel.App.Theme.ThemeMode.Dark;
        SystemThemeMenuItem.IsChecked = ThemeManager.CurrentMode == ForexPanel.App.Theme.ThemeMode.System;
    }

    private void LightThemeMenuItem_Click(object sender, RoutedEventArgs e) => ApplyTheme(ForexPanel.App.Theme.ThemeMode.Light);
    private void DarkThemeMenuItem_Click(object sender, RoutedEventArgs e) => ApplyTheme(ForexPanel.App.Theme.ThemeMode.Dark);
    private void SystemThemeMenuItem_Click(object sender, RoutedEventArgs e) => ApplyTheme(ForexPanel.App.Theme.ThemeMode.System);

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MaximizeButton_Click(sender, e);
            return;
        }
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        ApplyMaximizeBounds();
        SetMaximizeCaptionIcon();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void CollapseToolbarButton_Click(object sender, RoutedEventArgs e)
    {
        MainToolbar.ToggleCollapse();
        CollapseToolbarButton.Content = MainToolbar.IsCollapsed ? "▼" : "▲";
        CollapseToolbarButton.ToolTip = MainToolbar.IsCollapsed ? "Expand Toolbar" : "Collapse Toolbar";
        CollapseToolbarButton.Margin = MainToolbar.IsCollapsed ? new Thickness(0, 0, 8, 0) : new Thickness(0, -7, 8, 0);
    }

    private void Mt4PipeServer_MessageReceived(string message)
    {
        Debug.WriteLine($"MT4 -> ForexPanel: {message}");
        Dispatcher.Invoke(() => Mt4LogText.Text += $"MT4 -> ForexPanel: {message}" + Environment.NewLine);
        if (message.StartsWith("HELLO|")) _ = SendPingToMt4();
    }

    private async System.Threading.Tasks.Task SendPingToMt4()
    {
        try
        {
            await mt4PipeServer.SendAsync("PING");
            Debug.WriteLine("ForexPanel -> MT4: PING");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PING error: {ex.Message}");
        }
    }

    private void UpdateCandleInfo(Candle? candle)
    {
        if (candle == null)
        {
            CandleTimeText.Text = "Time:"; CandleOpenText.Text = "Open:"; CandleHighText.Text = "High:";
            CandleLowText.Text = "Low:"; CandleCloseText.Text = "Close:"; CandleVolumeText.Text = "Volume:";
            return;
        }
        CandleTimeText.Text = $"Time: {candle.Time:yyyy-MM-dd HH:mm}";
        CandleOpenText.Text = $"Open: {candle.Open:F5}";
        CandleHighText.Text = $"High: {candle.High:F5}";
        CandleLowText.Text = $"Low: {candle.Low:F5}";
        CandleCloseText.Text = $"Close: {candle.Close:F5}";
        CandleVolumeText.Text = $"Volume: {candle.Volume:F0}";
    }
}
