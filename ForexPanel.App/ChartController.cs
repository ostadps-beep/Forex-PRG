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
        candles = CreateTestCandles(symbol);
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

        var candlePlot = chart.Plot.Add.Candlestick(data);
        candlePlot.Axes.YAxis = chart.Plot.Axes.Right;
        candlePlot.Sequential = false;
    }

    private void ConfigureAxes()
    {
        chart.Plot.Axes.DateTimeTicksBottom();

        chart.Plot.Axes.Left.IsVisible = false;
        chart.Plot.Axes.Right.IsVisible = true;
        chart.Plot.Axes.Right.MinimumSize = 65;
        chart.Plot.Axes.Bottom.MinimumSize = 35;

        // Bottom time axis: keep labels and ticks readable on the dark chart theme.
        chart.Plot.Axes.Bottom.TickLabelStyle.ForeColor = ScottPlot.Colors.White;
        chart.Plot.Axes.Bottom.MajorTickStyle.Color = ScottPlot.Colors.White;
        chart.Plot.Axes.Bottom.MinorTickStyle.Color = ScottPlot.Colors.White;
        chart.Plot.Axes.Bottom.FrameLineStyle.Color = ScottPlot.Colors.White;
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

    private List<Candle> CreateTestCandles(string symbol)
    {
        List<Candle> result = new();
        double price = GetStartingPrice(symbol);
        DateTime time = DateTime.Now.AddMinutes(-(CandleCount * candleMinutes));
        Random random = new(42);

        for (int i = 0; i < CandleCount; i++)
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
            time = time.AddMinutes(candleMinutes);
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
