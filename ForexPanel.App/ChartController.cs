using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ForexPanel.Core;

namespace ForexPanel.App;

public sealed class ChartController
{
    private readonly ScottPlot.WPF.WpfPlot chart;
    private readonly Action<Candle?>? candleChanged;

    private ScottPlot.Plottables.Crosshair? crosshair;

    private List<Candle> candles = new();

    private int candleMinutes = 15;
    private bool gridEnabled;

    private const int CandleCount = 250;
    private const double PriceScaleWidth = 100.0;

    private const double MinimumScaleFactor = 0.50;
    private const double MaximumScaleFactor = 4.00;

    private double priceScaleBaseHalf;

    private bool priceScaleDrag;
    private bool timeScaleDrag;

    private Point dragStartPixel;
    private double priceScaleStartY;

    private ScottPlot.Plottables.CandlestickPlot? candlePlot;
    private ScottPlot.Color? themeAxisText;
    private ScottPlot.Color? themeCandleUp;
    private ScottPlot.Color? themeCandleDown;
    private ScottPlot.Color? themeCrosshair;

    // Real candle calculation engine state: a persistent M1 baseline per symbol, resampled
    // on demand into whatever timeframe is requested (see CandleResampler). The baseline is
    // generated once per symbol and reused across timeframe switches, so the same underlying
    // price history is consistent no matter which timeframe you're viewing it at - this
    // replaces the old approach of regenerating a brand-new random dataset per timeframe.
    private readonly Dictionary<string, List<Candle>> baselineCache = new();
    private const int BaselineM1Minutes = 260 * 1440; // ~260 days of M1 bars - enough for CandleCount daily candles plus buffer

    public ChartController(
        ScottPlot.WPF.WpfPlot chart,
        Action<Candle?>? candleChanged = null)
    {
        this.chart = chart;
        this.candleChanged = candleChanged;

        // Use ScottPlot's standard body pan exactly as in ChartBodyLab.
        // Keep custom handling only for the price-scale zoom and existing wheel behavior.
        chart.UserInputProcessor.LeftClickDragPan(true);
        chart.UserInputProcessor.RightClickDragZoom(false);

        chart.UserInputProcessor.RemoveAll<
            ScottPlot.Interactivity.UserActionResponses.MouseWheelZoom>();

        chart.PreviewMouseWheel += Chart_PreviewMouseWheel;
        chart.PreviewMouseLeftButtonDown += Chart_PreviewMouseLeftButtonDown;
        chart.PreviewMouseLeftButtonUp += Chart_PreviewMouseLeftButtonUp;
        chart.PreviewMouseMove += Chart_PreviewMouseMove;
        chart.MouseMove += Chart_MouseMove;
        chart.MouseLeave += Chart_MouseLeave;

        Rebuild("EURUSD", 15);
    }

    /// <summary>
    /// Applies theme colors (from the current WPF ResourceDictionary) to the chart itself:
    /// axis text/ticks, candle up/down colors, and the crosshair. Safe to call any time,
    /// including after Rebuild, since the colors are cached and re-applied on every Rebuild
    /// automatically (see ConfigureAxes/AddCandlesticks/CreateCrosshair).
    /// </summary>
    public void ApplyTheme(
        System.Windows.Media.Color axisText,
        System.Windows.Media.Color candleUp,
        System.Windows.Media.Color candleDown,
        System.Windows.Media.Color crosshairColor)
    {
        themeAxisText = ToScottPlotColor(axisText);
        themeCandleUp = ToScottPlotColor(candleUp);
        themeCandleDown = ToScottPlotColor(candleDown);
        themeCrosshair = ToScottPlotColor(crosshairColor);

        ApplyAxisTheme();
        ApplyCandleTheme();
        ApplyCrosshairTheme();
        chart.Refresh();
    }

    private static ScottPlot.Color ToScottPlotColor(System.Windows.Media.Color c) =>
        ScottPlot.Color.FromARGB((uint)((c.A << 24) | (c.R << 16) | (c.G << 8) | c.B));

    private void ApplyAxisTheme()
    {
        var color = themeAxisText ?? ScottPlot.Colors.White;
        chart.Plot.Axes.Bottom.TickLabelStyle.ForeColor = color;
        chart.Plot.Axes.Bottom.MajorTickStyle.Color = color;
        chart.Plot.Axes.Bottom.MinorTickStyle.Color = color;
        chart.Plot.Axes.Bottom.FrameLineStyle.Color = color;
        chart.Plot.Axes.Right.TickLabelStyle.ForeColor = color;
    }

    private void ApplyCandleTheme()
    {
        if (candlePlot == null)
            return;

        candlePlot.RisingColor = themeCandleUp ?? ScottPlot.Color.FromHex("#4CAF50");
        candlePlot.FallingColor = themeCandleDown ?? ScottPlot.Color.FromHex("#EF5350");
    }

    private void ApplyCrosshairTheme()
    {
        if (crosshair == null)
            return;

        crosshair.LineColor = themeCrosshair ?? ScottPlot.Color.FromHex("#B0B0B0");
    }

    public IReadOnlyList<Candle> Candles => candles;

    public bool GridEnabled => gridEnabled;

    public void SetGridEnabled(bool enabled)
    {
        gridEnabled = enabled;
        ApplyGridVisibility();
    }

    private void ApplyGridVisibility()
    {
        chart.Plot.Grid.IsVisible = gridEnabled;
    }

    public void Rebuild(string symbol, int timeframeMinutes)
    {
        candleMinutes = Math.Max(1, timeframeMinutes);

        priceScaleDrag = false;
        timeScaleDrag = false;

        if (chart.IsMouseCaptured)
            chart.ReleaseMouseCapture();

        chart.Plot.Clear();

        List<Candle> baseline = GetOrCreateBaseline(symbol);
        List<Candle> resampled = CandleResampler.Resample(baseline, candleMinutes);
        candles = resampled.Count > CandleCount
            ? resampled.Skip(resampled.Count - CandleCount).ToList()
            : resampled;

        AddCandlesticks();
        ConfigureAxes();
        ConfigureGrid();
        CreateCrosshair();
        chart.Plot.Axes.AutoScale();

        var initialLimits = chart.Plot.Axes.GetLimits(
            chart.Plot.Axes.Bottom,
            chart.Plot.Axes.Right);

        priceScaleBaseHalf =
            (initialLimits.Top - initialLimits.Bottom) / 2.0;

        chart.Refresh();
    }

    /// <summary>
    /// Returns the cached M1 baseline for a symbol, generating it once if it doesn't exist yet.
    /// The same baseline is reused across every timeframe switch for that symbol.
    /// </summary>
    private List<Candle> GetOrCreateBaseline(string symbol)
    {
        if (baselineCache.TryGetValue(symbol, out var existing))
            return existing;

        List<Candle> generated = GenerateM1Baseline(symbol);
        baselineCache[symbol] = generated;
        return generated;
    }

    private void AddCandlesticks()
    {
        List<ScottPlot.OHLC> data = candles
            .Select(c => new ScottPlot.OHLC(
                c.Open,
                c.High,
                c.Low,
                c.Close,
                c.Time,
                TimeSpan.FromMinutes(candleMinutes)))
            .ToList();

        candlePlot = chart.Plot.Add.Candlestick(data);
        candlePlot.Axes.YAxis = chart.Plot.Axes.Right;
        candlePlot.Sequential = false;
        ApplyCandleTheme();
    }

    private void ConfigureAxes()
    {
        chart.Plot.Axes.DateTimeTicksBottom();

        chart.Plot.Axes.Left.IsVisible = false;
        chart.Plot.Axes.Right.IsVisible = true;
        chart.Plot.Axes.Right.MinimumSize = 65;
        chart.Plot.Axes.Bottom.MinimumSize = 35;

        // Axis colors come from the current theme (see ApplyTheme); falls back to white
        // until MainWindow applies the real theme for the first time.
        ApplyAxisTheme();
    }

    private void ConfigureGrid()
    {
        ApplyGridVisibility();
    }

    private void CreateCrosshair()
    {
        crosshair = chart.Plot.Add.Crosshair(0, 0);
        crosshair.HorizontalLine.LineWidth = 1;
        crosshair.VerticalLine.LineWidth = 1;
        crosshair.HorizontalLine.LinePattern = ScottPlot.LinePattern.Dotted;
        crosshair.VerticalLine.LinePattern = ScottPlot.LinePattern.Dotted;
        crosshair.IsVisible = false;
        ApplyCrosshairTheme();
    }

    private void Chart_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(chart);

        if (position.Y >= chart.ActualHeight - 35)
        {
            timeScaleDrag = true;
            priceScaleDrag = false;
            dragStartPixel = position;
            chart.CaptureMouse();
            e.Handled = true;
            return;
        }

        if (position.X >= chart.ActualWidth - PriceScaleWidth)
        {
            priceScaleDrag = true;
            timeScaleDrag = false;
            priceScaleStartY = position.Y;
            chart.CaptureMouse();
            e.Handled = true;
            return;
        }

        // Body drag is intentionally NOT handled here.
        // ScottPlot's standard MouseDragPan response receives it.
    }

    private void Chart_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!priceScaleDrag && !timeScaleDrag)
            return;

        timeScaleDrag = false;
        priceScaleDrag = false;

        if (chart.IsMouseCaptured)
            chart.ReleaseMouseCapture();

        e.Handled = true;
    }

    private void Chart_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!priceScaleDrag)
            return;

        var position = e.GetPosition(chart);

        double dy = position.Y - priceScaleStartY;
        if (Math.Abs(dy) < 0.5)
            return;

        var limits = chart.Plot.Axes.GetLimits(
            chart.Plot.Axes.Bottom,
            chart.Plot.Axes.Right);

        double center = (limits.Top + limits.Bottom) / 2.0;
        double half = (limits.Top - limits.Bottom) / 2.0;
        double factor = Math.Exp(dy / 180.0);
        double newHalf = half * factor;

        double minimumHalf = priceScaleBaseHalf * MinimumScaleFactor;
        double maximumHalf = priceScaleBaseHalf * MaximumScaleFactor;

        half = Math.Clamp(newHalf, minimumHalf, maximumHalf);

        chart.Plot.Axes.SetLimitsY(
            center - half,
            center + half,
            chart.Plot.Axes.Right);

        priceScaleStartY = position.Y;
        chart.Refresh();
        e.Handled = true;
    }

    private void Chart_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (candles.Count == 0)
            return;

        var limits = chart.Plot.Axes.GetLimits();
        double visibleWidth = limits.Right - limits.Left;
        double shift = visibleWidth * 0.10;

        if (e.Delta > 0)
        {
            chart.Plot.Axes.SetLimitsX(
                limits.Left + shift,
                limits.Right + shift);
        }
        else if (e.Delta < 0)
        {
            chart.Plot.Axes.SetLimitsX(
                limits.Left - shift,
                limits.Right - shift);
        }

        chart.Refresh();
        e.Handled = true;
    }

    private void Chart_MouseMove(object? sender, MouseEventArgs e)
    {
        if (crosshair == null || candles.Count == 0)
            return;

        if (priceScaleDrag || timeScaleDrag)
            return;

        var position = e.GetPosition(chart);
        ScottPlot.Pixel pixel = new(
            position.X * chart.DisplayScale,
            position.Y * chart.DisplayScale);

        ScottPlot.Coordinates coordinates = chart.Plot.GetCoordinates(pixel);
        crosshair.Position = coordinates;
        crosshair.IsVisible = true;
        UpdateCandleInfo(coordinates.X);
        chart.Refresh();
    }

    private void Chart_MouseLeave(object? sender, MouseEventArgs e)
    {
        if (crosshair == null)
            return;

        if (priceScaleDrag || timeScaleDrag)
            return;

        crosshair.IsVisible = false;
        candleChanged?.Invoke(null);
        chart.Refresh();
    }

    private void UpdateCandleInfo(double x)
    {
        Candle? nearest = GetNearestCandle(x);
        candleChanged?.Invoke(nearest);
    }

    public Candle? GetNearestCandle(double x)
    {
        if (candles.Count == 0)
            return null;

        return candles
            .OrderBy(c => Math.Abs(c.Time.ToOADate() - x))
            .First();
    }

    /// <summary>
    /// Generates a deterministic M1 price series for a symbol. This is still placeholder data
    /// (no live source is wired up yet - that is a separate, later step), but it is now generated
    /// ONLY at M1 granularity and cached per-symbol; every displayed timeframe (M5/M15/.../D1)
    /// is derived from this same series via CandleResampler, so switching timeframe no longer
    /// changes the underlying price history - only how it's aggregated for display.
    /// </summary>
    private List<Candle> GenerateM1Baseline(string symbol)
    {
        List<Candle> result = new(BaselineM1Minutes);
        double price = GetStartingPrice(symbol);
        DateTime time = DateTime.Now.AddMinutes(-BaselineM1Minutes);
        Random random = new(symbol.GetHashCode());

        for (int i = 0; i < BaselineM1Minutes; i++)
        {
            double open = price;
            double change = GetPriceChange(symbol, random);
            double close = open + change;
            double spread = GetSpread(symbol);
            double high = Math.Max(open, close) + random.NextDouble() * spread;
            double low = Math.Min(open, close) - random.NextDouble() * spread;

            result.Add(new Candle
            {
                Time = time,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = random.Next(100, 1000)
            });

            price = close;
            time = time.AddMinutes(1);
        }

        return result;
    }

    private static double GetStartingPrice(string symbol)
    {
        return symbol switch
        {
            "EURUSD" => 1.16800,
            "GBPUSD" => 1.35000,
            "USDJPY" => 147.500,
            _ => 1.16800
        };
    }

    private static double GetPriceChange(string symbol, Random random)
    {
        if (symbol == "USDJPY")
            return (random.NextDouble() - 0.5) * 0.50;

        return (random.NextDouble() - 0.5) * 0.0010;
    }

    private static double GetSpread(string symbol)
    {
        return symbol switch
        {
            "USDJPY" => 0.25,
            _ => 0.0005
        };
    }
}
