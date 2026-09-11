namespace ForexPanel.App.ChartLayout;

/// <summary>
/// Central time-scale state and calculations for candle layout.
/// Data count and visible count are intentionally independent.
/// </summary>
public sealed class CandleLayoutModel
{
    public const double DefaultMinBarSpacing = 6.0;
    public const double DefaultMaxBarSpacing = 80.0;
    public const double DefaultCandleBodyRatio = 0.72;

    public CandleLayoutModel(
        double chartWidth,
        double barSpacing,
        double rightOffset = 0.0,
        double minBarSpacing = DefaultMinBarSpacing,
        double maxBarSpacing = DefaultMaxBarSpacing,
        double candleBodyRatio = DefaultCandleBodyRatio)
    {
        if (chartWidth < 0)
            throw new ArgumentOutOfRangeException(nameof(chartWidth));

        if (minBarSpacing <= 0)
            throw new ArgumentOutOfRangeException(nameof(minBarSpacing));

        if (maxBarSpacing < minBarSpacing)
            throw new ArgumentOutOfRangeException(nameof(maxBarSpacing));

        if (candleBodyRatio <= 0 || candleBodyRatio > 1)
            throw new ArgumentOutOfRangeException(nameof(candleBodyRatio));

        ChartWidth = chartWidth;
        MinBarSpacing = minBarSpacing;
        MaxBarSpacing = maxBarSpacing;
        CandleBodyRatio = candleBodyRatio;
        BarSpacing = Math.Clamp(barSpacing, MinBarSpacing, MaxBarSpacing);
        RightOffset = Math.Max(0, rightOffset);
    }

    public double ChartWidth { get; private set; }

    public double BarSpacing { get; private set; }

    public double RightOffset { get; private set; }

    public double MinBarSpacing { get; }

    public double MaxBarSpacing { get; }

    public double CandleBodyRatio { get; }

    public double VisibleBars =>
        BarSpacing <= 0 ? 0 : ChartWidth / BarSpacing;

    public double CandleWidth =>
        BarSpacing * CandleBodyRatio;

    public double GetVisibleBars(double spanDays, double intervalDays)
    {
        if (spanDays < 0)
            throw new ArgumentOutOfRangeException(nameof(spanDays));

        if (intervalDays <= 0)
            throw new ArgumentOutOfRangeException(nameof(intervalDays));

        return spanDays / intervalDays;
    }

    public double GetBarSpacing(double spanDays, double intervalDays)
    {
        var visibleBars = GetVisibleBars(spanDays, intervalDays);

        if (visibleBars <= 0 || ChartWidth <= 0)
            return BarSpacing;

        return ChartWidth / visibleBars;
    }

    public double GetSpanDays(double intervalDays)
    {
        if (intervalDays <= 0)
            throw new ArgumentOutOfRangeException(nameof(intervalDays));

        return VisibleBars * intervalDays;
    }

    public double GetCandleWidth(double barSpacing)
    {
        if (barSpacing < 0)
            throw new ArgumentOutOfRangeException(nameof(barSpacing));

        return barSpacing * CandleBodyRatio;
    }

    public void SetChartWidth(double chartWidth)
    {
        if (chartWidth < 0)
            throw new ArgumentOutOfRangeException(nameof(chartWidth));

        ChartWidth = chartWidth;
    }

    public void SetBarSpacing(double barSpacing)
    {
        BarSpacing = Math.Clamp(barSpacing, MinBarSpacing, MaxBarSpacing);
    }

    public void SetRightOffset(double rightOffset)
    {
        RightOffset = Math.Max(0, rightOffset);
    }

    public void Zoom(double factor)
    {
        if (factor <= 0)
            throw new ArgumentOutOfRangeException(nameof(factor));

        SetBarSpacing(BarSpacing * factor);
    }
}
