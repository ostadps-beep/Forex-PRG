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

    /// <summary>
    /// When true, candle bodies are drawn as an outline only (not filled) - the classic
    /// "Hollow Candles" chart style. Uses its own dedicated colors (HollowRisingStyle/
    /// HollowFallingStyle) rather than reusing RisingLineStyle/FallingLineStyle (the wick
    /// color), per PS's explicit request for a separate color setting for this style.
    /// </summary>
    public bool HollowBody { get; set; }
    public LineStyle HollowRisingStyle { get; } = new() { Color = Color.FromHex("#4CAF50"), Width = 1.5f };
    public LineStyle HollowFallingStyle { get; } = new() { Color = Color.FromHex("#EF5350"), Width = 1.5f };

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
            float bodyTop = Math.Min(yPxOpen, yPxClose);
            float bodyBottom = Math.Max(yPxOpen, yPxClose);

            if (ShowWicks)
            {
                if (HollowBody && ShowBody)
                {
                    // Hollow candles have no fill, so a single top-to-bottom wick line would
                    // visibly cut straight through the empty interior of the body outline.
                    // Draw only the two segments outside the body instead (above and below it).
                    if (bodyTop > top)
                        Drawing.DrawLine(rp.Canvas, rp.Paint, new PixelLine(center, top, center, bodyTop), isRising ? HollowRisingStyle : HollowFallingStyle);
                    if (bottom > bodyBottom)
                        Drawing.DrawLine(rp.Canvas, rp.Paint, new PixelLine(center, bodyBottom, center, bottom), isRising ? HollowRisingStyle : HollowFallingStyle);
                }
                else
                {
                    PixelLine verticalLine = new(center, top, center, bottom);
                    Drawing.DrawLine(rp.Canvas, rp.Paint, verticalLine, lineStyle);
                }
            }

            if (!ShowBody)
                continue;

            bool barIsAtLeastOnePixelWide = xPxRight - xPxLeft > 1;
            if (!barIsAtLeastOnePixelWide)
                continue;

            PixelRangeX xPxRange = new(xPxLeft, xPxRight);
            PixelRangeY yPxRange = new(bodyTop, bodyBottom);
            PixelRect rect = new(xPxRange, yPxRange);

            if (HollowBody)
            {
                LineStyle hollowStyle = isRising ? HollowRisingStyle : HollowFallingStyle;
                Drawing.DrawRectangle(rp.Canvas, rect, rp.Paint, hollowStyle);
            }
            else if (yPxOpen != yPxClose)
            {
                fillStyle.Render(rp.Canvas, rect, rp.Paint);
            }
            else
            {
                lineStyle.Render(rp.Canvas, rect.BottomLine, rp.Paint);
            }
        }
    }
}
