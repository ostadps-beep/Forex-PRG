using System;
using System.Collections.Generic;
using System.Windows;

namespace ForexPanel.ChartBodyLab;

public partial class MainWindow : Window
{
    private const int CandleCount = 250;
    private const int CandleMinutes = 15;

    public MainWindow()
    {
        InitializeComponent();
        BuildChart();
    }

    private void BuildChart()
    {
        List<ScottPlot.OHLC> data = CreateTestCandles();

        var candles = Chart.Plot.Add.Candlestick(data);
        candles.Axes.YAxis = Chart.Plot.Axes.Right;
        candles.Sequential = false;

        Chart.Plot.Axes.DateTimeTicksBottom();
        Chart.Plot.Axes.Left.IsVisible = false;
        Chart.Plot.Axes.Right.IsVisible = true;
        Chart.Plot.Axes.Right.MinimumSize = 65;
        Chart.Plot.Axes.Bottom.MinimumSize = 35;

        Chart.Plot.Grid.IsVisible = true;
        Chart.Plot.Axes.AutoScale();

        // This lab intentionally uses ScottPlot 5's standard interaction system.
        // No custom mouse event handlers are installed here.
        // Standard ScottPlot responses provide pan/zoom behavior for comparison.
        Chart.Refresh();
    }

    private static List<ScottPlot.OHLC> CreateTestCandles()
    {
        List<ScottPlot.OHLC> result = new();
        double price = 1.16800;
        DateTime time = DateTime.Now.AddMinutes(-(CandleCount * CandleMinutes));
        Random random = new(42);

        for (int i = 0; i < CandleCount; i++)
        {
            double open = price;
            double change = (random.NextDouble() - 0.5) * 0.0010;
            double close = open + change;
            double spread = 0.0005;
            double high = Math.Max(open, close) + random.NextDouble() * spread;
            double low = Math.Min(open, close) - random.NextDouble() * spread;

            result.Add(new ScottPlot.OHLC(
                open,
                high,
                low,
                close,
                time,
                TimeSpan.FromMinutes(CandleMinutes)));

            price = close;
            time = time.AddMinutes(CandleMinutes);
        }

        return result;
    }
}
