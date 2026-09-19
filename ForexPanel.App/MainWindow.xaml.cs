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

internal enum DrawingToolMode { None, HorizontalLine, VerticalLine, TrendLine, Ray, Rectangle }

public partial class MainWindow : Window
{
    private const double InitialBarSpacing = 10.0;

    private readonly Mt4PipeServer mt4PipeServer;
    private readonly ChartController chartController;
    private readonly CandleLayoutModel candleLayoutModel;
    private int candleMinutes = 15;
    private string currentSymbol = "EURUSD";
    private bool crosshairEnabled;
    private const double ChartShiftFraction = 0.15; // MT4-style blank space after the last candle, as a fraction of the visible span
    private readonly ForexPanel.App.Settings.ChartSettings chartSettings = ForexPanel.App.Settings.ChartSettings.CreateDefault();
    private bool candleLayoutInitialized;

    private bool zoomAreaMode;
    private DrawingToolMode activeDrawingTool = DrawingToolMode.None;
    private ScottPlot.Coordinates? pendingTrendStart;
    private readonly List<ScottPlot.IPlottable> placedDrawings = new();
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
        FixChartContextMenuAutoscale();
        ApplyChartSurfaceTheme();
        RefreshChartTypeToolbarHighlight();

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
            placedDrawings.Clear();
            activeDrawingTool = DrawingToolMode.None;
            pendingTrendStart = null;
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
            placedDrawings.Clear();
            activeDrawingTool = DrawingToolMode.None;
            pendingTrendStart = null;
            ApplyInitialCandleViewport();
            ApplyCrosshairVisibility();
        };

        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
        StateChanged += MainWindow_StateChanged;
        UpdateThemeMenuChecks();
    }

    /// <summary>
    /// Reset View: restore default bar spacing/right-offset on the time axis and auto-scale
    /// the price axis to whatever candles end up visible. Shared by the Reset toolbar tool
    /// and the chart's right-click context menu (see FixChartContextMenuAutoscale).
    /// </summary>
    private void PerformResetView()
    {
        CancelZoomAreaMode();
        ApplyInitialCandleViewport();
        chartController.ResetPriceScaleToVisibleRange();
    }

    /// <summary>
    /// ScottPlot's default right-click context menu includes an "Autoscale" item that calls
    /// the raw Plot.Axes.AutoScale() - this fits ALL plotted candles into view at once,
    /// completely ignoring our CandleLayoutModel/viewport system, which is what made the
    /// chart look "compressed"/bunched when PS used it (this is what PS referred to as the
    /// primitive early "Auto Scroll" behavior in the right-click menu). Repoints that same
    /// menu entry at our own proper Reset View logic instead of removing/duplicating menu items.
    /// </summary>
    private void FixChartContextMenuAutoscale()
    {
        if (Chart.Menu is not StyledChartMenu styledMenu)
            return;

        var items = styledMenu.ContextMenuItems;
        for (int i = 0; i < items.Count; i++)
        {
            // ContextMenuItem is a struct - it must be replaced by index, not mutated in place.
            if (items[i].Label == "Autoscale")
            {
                items[i] = new ScottPlot.ContextMenuItem
                {
                    Label = "Reset View",
                    OnInvoke = _ => PerformResetView()
                };
                break;
            }
        }
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

        // Chart Shift (MT4-style): when enabled, leaves blank space after the last candle
        // instead of pinning it to the very edge of the chart. `right` here is always the
        // "true" data-anchored right edge (latest candle or current scroll position) - the
        // shift is applied only for display, not fed back into SyncCandleLayoutFromChart's
        // own math (a known minor approximation: bar-spacing recalculated right after toggling
        // Chart Shift while zoomed may be very slightly off, since the visible span then
        // includes the blank margin - acceptable for now, not a functional break).
        var displayRight = chartSettings.General.ChartShiftEnabled
            ? right + spanDays * ChartShiftFraction
            : right;

        var left = displayRight - spanDays;

        Chart.Plot.Axes.SetLimitsX(left, displayRight);
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

        if (tool.Id.StartsWith("ChartType.", StringComparison.Ordinal))
        {
            var type = tool.Id switch
            {
                "ChartType.Candlestick" => ForexPanel.App.Settings.ChartTypeOption.Candlestick,
                "ChartType.HollowCandlestick" => ForexPanel.App.Settings.ChartTypeOption.HollowCandlestick,
                "ChartType.Bar" => ForexPanel.App.Settings.ChartTypeOption.Bar,
                "ChartType.Line" => ForexPanel.App.Settings.ChartTypeOption.Line,
                "ChartType.Area" => ForexPanel.App.Settings.ChartTypeOption.Area,
                _ => ForexPanel.App.Settings.ChartTypeOption.Candlestick
            };

            chartSettings.General.ChartType = type;
            chartController.ApplyChartSettings(chartSettings);
            RefreshChartTypeToolbarHighlight();
            return;
        }

        if (tool.Id is "HorizontalLine" or "VerticalLine" or "TrendLine" or "Ray" or "Rectangle")
        {
            var requestedMode = tool.Id switch
            {
                "HorizontalLine" => DrawingToolMode.HorizontalLine,
                "VerticalLine" => DrawingToolMode.VerticalLine,
                "TrendLine" => DrawingToolMode.TrendLine,
                "Ray" => DrawingToolMode.Ray,
                _ => DrawingToolMode.Rectangle
            };

            CancelZoomAreaMode();
            pendingTrendStart = null;

            // Clicking the already-active tool cancels it (toggle), matching MT4 convention.
            activeDrawingTool = activeDrawingTool == requestedMode ? DrawingToolMode.None : requestedMode;
            RefreshDrawingToolHighlight();
            return;
        }

        if (string.Equals(tool.Id, "Delete", StringComparison.Ordinal))
        {
            if (placedDrawings.Count > 0)
            {
                var last = placedDrawings[^1];
                placedDrawings.RemoveAt(placedDrawings.Count - 1);
                Chart.Plot.PlottableList.Remove(last);
                Chart.Refresh();
            }
            return;
        }

        if (string.Equals(tool.Id, "Cursor", StringComparison.Ordinal))
        {
            // Cursor = standard/neutral pointer mode. If another exclusive mode (Crosshair,
            // an active drawing tool) is currently active, selecting Cursor turns it back off.
            if (crosshairEnabled)
            {
                crosshairEnabled = false;
                RefreshToggleToolbarButtonBackgrounds();
                ApplyCrosshairVisibility();
            }

            if (activeDrawingTool != DrawingToolMode.None)
            {
                activeDrawingTool = DrawingToolMode.None;
                pendingTrendStart = null;
                RefreshDrawingToolHighlight();
            }
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

        if (string.Equals(tool.Id, "Reset", StringComparison.Ordinal))
        {
            PerformResetView();
            return;
        }

        if (string.Equals(tool.Id, "AutoScroll", StringComparison.Ordinal))
        {
            chartSettings.General.AutoScrollEnabled = !chartSettings.General.AutoScrollEnabled;
            var autoScrollBackground = TryFindResource("Color.Toolbar.ButtonPressed") as Brush;
            var autoScrollBorder = TryFindResource("Color.Toolbar.ButtonPressedBorder") as Brush;
            button.Background = chartSettings.General.AutoScrollEnabled && autoScrollBackground != null ? autoScrollBackground : Brushes.Transparent;
            button.BorderBrush = chartSettings.General.AutoScrollEnabled && autoScrollBorder != null ? autoScrollBorder : Brushes.Transparent;

            if (chartSettings.General.AutoScrollEnabled)
            {
                // Snap immediately to the latest bar. There's no live feed yet to keep
                // re-anchoring to as new bars arrive (that's a separate, later step), but this
                // toggle at least does the meaningful, testable part right now: jump to and
                // pin the view on the most recent candle.
                candleLayoutModel.SetRightOffset(0);
                ApplyCandleLayoutToChart(DateTime.Now.ToOADate());
            }
            return;
        }

        if (string.Equals(tool.Id, "ChartShift", StringComparison.Ordinal))
        {
            SyncCandleLayoutFromChart();
            var limits = Chart.Plot.Axes.GetLimits();
            var trueRight = chartSettings.General.ChartShiftEnabled
                ? limits.Right - (candleLayoutModel.VisibleBars * (candleMinutes / (24.0 * 60.0)) * ChartShiftFraction)
                : limits.Right;

            chartSettings.General.ChartShiftEnabled = !chartSettings.General.ChartShiftEnabled;

            var chartShiftBackground = TryFindResource("Color.Toolbar.ButtonPressed") as Brush;
            var chartShiftBorder = TryFindResource("Color.Toolbar.ButtonPressedBorder") as Brush;
            button.Background = chartSettings.General.ChartShiftEnabled && chartShiftBackground != null ? chartShiftBackground : Brushes.Transparent;
            button.BorderBrush = chartSettings.General.ChartShiftEnabled && chartShiftBorder != null ? chartShiftBorder : Brushes.Transparent;

            ApplyCandleLayoutToChart(trueRight);
            return;
        }

        if (string.Equals(tool.Id, "Settings", StringComparison.Ordinal))
        {
            var window = new ForexPanel.App.Settings.ChartSettingsWindow(
                chartSettings,
                () =>
                {
                    chartController.ApplyChartSettings(chartSettings);
                    RefreshChartTypeToolbarHighlight();
                })
            {
                Owner = this
            };
            window.ShowDialog();
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
        if (activeDrawingTool != DrawingToolMode.None)
        {
            PlaceDrawingAtCursor(e.GetPosition(Chart));
            e.Handled = true;
            return;
        }

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

    /// <summary>
    /// Places the currently-active drawing tool at the clicked chart position. Horizontal and
    /// Vertical lines place immediately on one click; Trend Line needs two clicks (start, then
    /// end) - matching MT4's own "phase 1, simple draw only" convention (no dragging/editing of
    /// already-placed objects yet, per the same phased approach used for the sibling project).
    /// </summary>
    private void PlaceDrawingAtCursor(Point wpfPoint)
    {
        var pixel = ToScottPlotPixel(wpfPoint);
        var coords = Chart.Plot.GetCoordinates(pixel, Chart.Plot.Axes.Bottom, Chart.Plot.Axes.Right);
        var style = chartSettings.DrawingTools;
        var color = ToScottPlotColorForDrawing(style.DefaultColor.Effective);
        float width = (float)style.Thickness;
        var pattern = style.LineStyle switch
        {
            ForexPanel.App.Settings.LineStyleOption.Dash => ScottPlot.LinePattern.Dashed,
            ForexPanel.App.Settings.LineStyleOption.Dot => ScottPlot.LinePattern.Dotted,
            _ => ScottPlot.LinePattern.Solid
        };

        switch (activeDrawingTool)
        {
            case DrawingToolMode.HorizontalLine:
            {
                var line = Chart.Plot.Add.HorizontalLine(coords.Y, width, color, pattern);
                line.Axes.YAxis = Chart.Plot.Axes.Right;
                placedDrawings.Add(line);
                activeDrawingTool = DrawingToolMode.None;
                RefreshDrawingToolHighlight();
                break;
            }
            case DrawingToolMode.VerticalLine:
            {
                var line = Chart.Plot.Add.VerticalLine(coords.X, width, color, pattern);
                line.Axes.YAxis = Chart.Plot.Axes.Right;
                placedDrawings.Add(line);
                activeDrawingTool = DrawingToolMode.None;
                RefreshDrawingToolHighlight();
                break;
            }
            case DrawingToolMode.TrendLine:
            {
                if (pendingTrendStart == null)
                {
                    pendingTrendStart = coords;
                    return; // wait for the second click - stay in TrendLine mode
                }

                var trend = Chart.Plot.Add.Line(pendingTrendStart.Value.X, pendingTrendStart.Value.Y, coords.X, coords.Y);
                trend.Axes.YAxis = Chart.Plot.Axes.Right;
                trend.LineWidth = width;
                trend.Color = color;
                trend.LinePattern = pattern;
                placedDrawings.Add(trend);
                pendingTrendStart = null;
                activeDrawingTool = DrawingToolMode.None;
                RefreshDrawingToolHighlight();
                break;
            }
            case DrawingToolMode.Ray:
            {
                if (pendingTrendStart == null)
                {
                    pendingTrendStart = coords;
                    return; // wait for the second click to set the ray's direction
                }

                var start = pendingTrendStart.Value;
                double dx = coords.X - start.X;
                double dy = coords.Y - start.Y;

                // ScottPlot has no dedicated "ray" (one-sided infinite line) plottable, so this
                // approximates it: extend far past the second click in the same direction, well
                // beyond any realistic zoom level, using a two-point line.
                const double ExtendFactor = 1000;
                double farX = coords.X + dx * ExtendFactor;
                double farY = coords.Y + dy * ExtendFactor;

                var ray = Chart.Plot.Add.Line(start.X, start.Y, farX, farY);
                ray.Axes.YAxis = Chart.Plot.Axes.Right;
                ray.LineWidth = width;
                ray.Color = color;
                ray.LinePattern = pattern;
                placedDrawings.Add(ray);
                pendingTrendStart = null;
                activeDrawingTool = DrawingToolMode.None;
                RefreshDrawingToolHighlight();
                break;
            }
            case DrawingToolMode.Rectangle:
            {
                if (pendingTrendStart == null)
                {
                    pendingTrendStart = coords;
                    return; // wait for the second click for the opposite corner
                }

                var corner1 = pendingTrendStart.Value;
                var rect = Chart.Plot.Add.Rectangle(
                    Math.Min(corner1.X, coords.X), Math.Max(corner1.X, coords.X),
                    Math.Min(corner1.Y, coords.Y), Math.Max(corner1.Y, coords.Y));
                rect.Axes.YAxis = Chart.Plot.Axes.Right;
                rect.LineWidth = width;
                rect.LineColor = color;
                rect.LinePattern = pattern;
                rect.FillColor = ScottPlot.Colors.Transparent; // outline-only, matching the "simple draw" phase
                placedDrawings.Add(rect);
                pendingTrendStart = null;
                activeDrawingTool = DrawingToolMode.None;
                RefreshDrawingToolHighlight();
                break;
            }
        }

        Chart.Refresh();
    }

    private static ScottPlot.Color ToScottPlotColorForDrawing(System.Windows.Media.Color c) =>
        ScottPlot.Color.FromARGB((uint)((c.A << 24) | (c.R << 16) | (c.G << 8) | c.B));

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
        ApplyToggleButtonVisual("AutoScroll", chartSettings.General.AutoScrollEnabled, activeBackground, activeBorder);
        ApplyToggleButtonVisual("ChartShift", chartSettings.General.ChartShiftEnabled, activeBackground, activeBorder);
        RefreshChartTypeToolbarHighlight(activeBackground, activeBorder);
        RefreshDrawingToolHighlight(activeBackground, activeBorder);
    }

    /// <summary>
    /// Highlights whichever drawing-tool button (if any) is currently active, matching the
    /// same pattern used for chart-type buttons - a manually-managed radio group since the
    /// toolbar model itself has no built-in mutual exclusivity.
    /// </summary>
    private void RefreshDrawingToolHighlight(Brush? activeBackground = null, Brush? activeBorder = null)
    {
        activeBackground ??= TryFindResource("Color.Toolbar.ButtonPressed") as Brush;
        activeBorder ??= TryFindResource("Color.Toolbar.ButtonPressedBorder") as Brush;

        ApplyToggleButtonVisual("HorizontalLine", activeDrawingTool == DrawingToolMode.HorizontalLine, activeBackground, activeBorder);
        ApplyToggleButtonVisual("VerticalLine", activeDrawingTool == DrawingToolMode.VerticalLine, activeBackground, activeBorder);
        ApplyToggleButtonVisual("TrendLine", activeDrawingTool == DrawingToolMode.TrendLine, activeBackground, activeBorder);
        ApplyToggleButtonVisual("Ray", activeDrawingTool == DrawingToolMode.Ray, activeBackground, activeBorder);
        ApplyToggleButtonVisual("Rectangle", activeDrawingTool == DrawingToolMode.Rectangle, activeBackground, activeBorder);
    }

    /// <summary>
    /// Highlights whichever ChartType.* toolbar button matches the current chart type and
    /// un-highlights the other four - these five act like a radio group even though the
    /// toolbar model itself has no built-in mutual-exclusivity mechanism.
    /// </summary>
    private void RefreshChartTypeToolbarHighlight(Brush? activeBackground = null, Brush? activeBorder = null)
    {
        activeBackground ??= TryFindResource("Color.Toolbar.ButtonPressed") as Brush;
        activeBorder ??= TryFindResource("Color.Toolbar.ButtonPressedBorder") as Brush;

        string[] allIds = { "ChartType.Candlestick", "ChartType.HollowCandlestick", "ChartType.Bar", "ChartType.Line", "ChartType.Area" };
        string activeId = chartController.CurrentChartType switch
        {
            ForexPanel.App.Settings.ChartTypeOption.HollowCandlestick => "ChartType.HollowCandlestick",
            ForexPanel.App.Settings.ChartTypeOption.Bar => "ChartType.Bar",
            ForexPanel.App.Settings.ChartTypeOption.Line => "ChartType.Line",
            ForexPanel.App.Settings.ChartTypeOption.Area => "ChartType.Area",
            _ => "ChartType.Candlestick"
        };

        foreach (var id in allIds)
            ApplyToggleButtonVisual(id, id == activeId, activeBackground, activeBorder);
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
            Chart.Background = backgroundBrush;

        // Sync any color the user has NOT explicitly customized to the newly-active theme.
        // Without this, ChartSettings' stored values (which default to dark-theme colors) would
        // keep overwriting the theme on every settings push - which is exactly why the chart
        // background stayed dark after switching to the light theme. Colors the user did pick
        // themselves keep their choice and are deliberately left alone here.
        SyncUncustomizedColor(chartSettings.GridAndBackground.BackgroundColor, backgroundBrush);
        SyncUncustomizedColor(chartSettings.GridAndBackground.GridColor, gridBrush);
        SyncUncustomizedColor(chartSettings.Axes.AxisColor, textBrush);
        SyncUncustomizedColor(chartSettings.Candles.BullishColor, candleUpBrush);
        SyncUncustomizedColor(chartSettings.Candles.BearishColor, candleDownBrush);
        SyncUncustomizedColor(chartSettings.Candles.HollowUpColor, candleUpBrush);
        SyncUncustomizedColor(chartSettings.Candles.HollowDownColor, candleDownBrush);

        // Crosshair has no ChartSettings entry of its own yet, so it stays purely theme-driven.
        chartController.ApplyTheme(
            axisText: (textBrush ?? System.Windows.Media.Brushes.White).Color,
            candleUp: (candleUpBrush ?? System.Windows.Media.Brushes.LimeGreen).Color,
            candleDown: (candleDownBrush ?? System.Windows.Media.Brushes.OrangeRed).Color,
            crosshairColor: (crosshairBrush ?? System.Windows.Media.Brushes.Gray).Color);

        // Settings take precedence over the raw theme, so push them last.
        chartController.ApplyChartSettings(chartSettings);

        Chart.Refresh();
    }

    private static void SyncUncustomizedColor(ForexPanel.App.Settings.ColorSetting setting, System.Windows.Media.SolidColorBrush? themeBrush)
    {
        if (setting.IsCustomized || themeBrush == null)
            return;

        setting.BaseColor = themeBrush.Color;
        setting.ShadePercent = 100;
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
