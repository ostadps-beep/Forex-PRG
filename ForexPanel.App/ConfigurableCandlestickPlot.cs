using System;
using ScottPlot;
using ScottPlot.Plottables;
using SkiaSharp;

namespace ForexPanel.App;

/// <summary>
/// A CandlestickPlot that supports independently toggling wick and body visibility.
/// ScottPlot's own CandlestickPlot has no such option, but its Render() method is virtual
/// and every member it uses is public, so this replicates that logic with two extra flags
/// rather than forking the whole rendering pipeline.
/// </summary>
public sealed class ConfigurableCandlestickPlot : CandlestickPlot
{
    public bool ShowWicks { get; set; } = true;
    public bool ShowBody { get; set; } = true;

    public ConfigurableCandlestickPlot(IOHLCSource data) : base(data)
    {
    }

    public override void Render(RenderPack rp)
    {
        var ohlcs = Data.GetOHLCs();

        for (int i = 0; i < ohlcs.Count; i++)
        {
            OHLC ohlc = ohlcs[i];
            bool isRising = ohlc.Close >= ohlc.Open;
            LineStyle lineStyle = isRising ? RisingLineStyle : FallingLineStyle;
            FillStyle fillStyle = isRising ? RisingFillStyle : FallingFillStyle;

            float top = Axes.GetPixelY(ohlc.High);
            float bottom = Axes.GetPixelY(ohlc.Low);

            float center, xPxLeft, xPxRight;
            if (!Sequential)
            {
                double centerNumber = NumericConversion.ToNumber(ohlc.DateTime);
                center = Axes.GetPixelX(centerNumber);
                double halfWidthNumber = ohlc.TimeSpan.TotalDays / 2 * SymbolWidth;
                xPxLeft = Axes.GetPixelX(centerNumber - halfWidthNumber);
                xPxRight = Axes.GetPixelX(centerNumber + halfWidthNumber);
            }
            else
            {
                center = Axes.GetPixelX(i);
                xPxLeft = Axes.GetPixelX(i - (float)SymbolWidth / 2);
                xPxRight = Axes.GetPixelX(i + (float)SymbolWidth / 2);
            }

            if (xPxRight < rp.DataRect.Left || xPxLeft > rp.DataRect.Right)
                continue;

            float yPxOpen = Axes.GetPixelY(ohlc.Open);
            float yPxClose = Axes.GetPixelY(ohlc.Close);

            if (ShowWicks)
            {
                PixelLine verticalLine = new(center, top, center, bottom);
                Drawing.DrawLine(rp.Canvas, rp.Paint, verticalLine, lineStyle);
            }

            if (!ShowBody)
                continue;

            bool barIsAtLeastOnePixelWide = xPxRight - xPxLeft > 1;
            if (!barIsAtLeastOnePixelWide)
                continue;

            PixelRangeX xPxRange = new(xPxLeft, xPxRight);
            PixelRangeY yPxRange = new(Math.Min(yPxOpen, yPxClose), Math.Max(yPxOpen, yPxClose));
            PixelRect rect = new(xPxRange, yPxRange);

            if (yPxOpen != yPxClose)
                fillStyle.Render(rp.Canvas, rect, rp.Paint);
            else
                lineStyle.Render(rp.Canvas, rect.BottomLine, rp.Paint);
        }
    }
}
