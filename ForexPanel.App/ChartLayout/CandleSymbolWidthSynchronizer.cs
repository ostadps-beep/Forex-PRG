using System;
using System.Linq;
using System.Windows;

namespace ForexPanel.App.ChartLayout;

public static class CandleSymbolWidthSynchronizer
{
    public static void Attach(Window window)
    {
        if (window.FindName("Chart") is not ScottPlot.WPF.WpfPlot chart)
            return;

        chart.Plot.RenderManager.RenderStarting += (_, _) =>
        {
            var candlestick = chart.Plot.GetPlottables()
                .OfType<ScottPlot.Plottables.CandlestickPlot>()
                .FirstOrDefault();

            if (candlestick == null)
                return;

            candlestick.SymbolWidth = CandleLayoutModel.DefaultCandleBodyRatio;
        };
    }
}
