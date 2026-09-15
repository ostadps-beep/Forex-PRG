using System.Windows.Media;

namespace ForexPanel.App.Settings;

/// <summary>
/// Full category list for the Chart Settings sidebar, per PS's spec
/// (docs/CHART_SETTINGS_FULL_SPEC.txt). Only the first five have real model/behavior
/// wired up in this pass - the rest are reserved: either for this project's own future
/// roadmap (indicators, HUD, layout persistence) or for the separate C++/Python engine
/// that will eventually be merged in (see docs/CHART_SETTINGS_PLAN.md).
/// </summary>
public enum SettingsCategory
{
    Chart,
    Axes,
    Candles,
    GridAndBackground,
    DrawingTools,
    AnalyticalModules,   // reserved - no modules exist in this repo yet
    HudAndOverlay,       // reserved - no HUD system exists yet
    Performance,         // reserved - GPU/multi-threading concepts belong to the future engine
    Workspace,           // reserved - layout save/load not built yet
    Advanced             // reserved - Python/C++ bridge settings for the future engine merge
}

public enum ChartTypeOption { Candlestick, HollowCandlestick, Bar, Line, Area, Histogram, Combined }
public enum ZoomAxisOption { TimeAxis, PriceAxis, Both }
public enum MouseWheelOption { Pan, Zoom }
public enum AxisPositionOption { Left, Right }
public enum TimeAxisPositionOption { Bottom, Top }
public enum TimeFormatOption { HhMm, Date, Combined }
public enum LineStyleOption { Solid, Dash, Dot }

public sealed class ChartGeneralSettings
{
    public ChartTypeOption ChartType { get; set; } = ChartTypeOption.Candlestick;
    public int VisibleCandles { get; set; } = 250;
    public bool AutoFit { get; set; } = true;
    public ZoomAxisOption ZoomBehavior { get; set; } = ZoomAxisOption.Both;
    public MouseWheelOption MouseWheelBehavior { get; set; } = MouseWheelOption.Pan; // MT4-standard default - do not silently change
    public ColorSetting LineColor { get; set; } = ColorSetting.From(Color.FromRgb(0x21, 0x96, 0xF3));
    public ColorSetting AreaColor { get; set; } = ColorSetting.From(Color.FromArgb(60, 0x21, 0x96, 0xF3)); // common pale/translucent blue
    public bool ChartShiftEnabled { get; set; }
    public bool AutoScrollEnabled { get; set; }

    public ChartGeneralSettings Clone()
    {
        var clone = (ChartGeneralSettings)MemberwiseClone();
        clone.LineColor = LineColor.Clone();
        clone.AreaColor = AreaColor.Clone();
        return clone;
    }
}

public sealed class AxesSettings
{
    // Price axis
    public AxisPositionOption PricePosition { get; set; } = AxisPositionOption.Right; // matches current fixed layout
    public bool ShowLastPrice { get; set; } = true;
    public bool ShowHorizontalGrid { get; set; } = true;
    public int DecimalPlaces { get; set; } = 5;
    public ColorSetting AxisColor { get; set; } = ColorSetting.From(Colors.White);
    public double AxisThickness { get; set; } = 1.0;

    // Time axis
    public TimeAxisPositionOption TimePosition { get; set; } = TimeAxisPositionOption.Bottom; // fixed in current renderer
    public bool ShowVerticalGrid { get; set; } = true;
    public TimeFormatOption TimeFormat { get; set; } = TimeFormatOption.HhMm;

    public AxesSettings Clone()
    {
        var clone = (AxesSettings)MemberwiseClone();
        clone.AxisColor = AxisColor.Clone();
        return clone;
    }
}

public sealed class CandleSettings
{
    public ColorSetting BullishColor { get; set; } = ColorSetting.From(Color.FromRgb(0x4C, 0xAF, 0x50));
    public ColorSetting BearishColor { get; set; } = ColorSetting.From(Color.FromRgb(0xEF, 0x53, 0x50));
    public ColorSetting HollowUpColor { get; set; } = ColorSetting.From(Color.FromRgb(0x4C, 0xAF, 0x50));
    public ColorSetting HollowDownColor { get; set; } = ColorSetting.From(Color.FromRgb(0xEF, 0x53, 0x50));
    public ColorSetting BarUpColor { get; set; } = ColorSetting.From(Color.FromRgb(0x00, 0x80, 0x80)); // teal, matching PS's MT4 reference default
    public ColorSetting BarDownColor { get; set; } = ColorSetting.From(Color.FromRgb(0x80, 0x00, 0x00)); // maroon, matching PS's MT4 reference default
    public double BodyThickness { get; set; } = 0.8; // fraction of the candle's time slot (ScottPlot convention)
    public double WickThickness { get; set; } = 1.0; // pixel width of the regular candle's wick line
    public double HollowThickness { get; set; } = 1.5; // pixel width of the hollow candle's outline
    public bool ShowWicks { get; set; } = true;
    public bool ShowBody { get; set; } = true;

    public CandleSettings Clone()
    {
        var clone = (CandleSettings)MemberwiseClone();
        clone.BullishColor = BullishColor.Clone();
        clone.BearishColor = BearishColor.Clone();
        clone.HollowUpColor = HollowUpColor.Clone();
        clone.HollowDownColor = HollowDownColor.Clone();
        clone.BarUpColor = BarUpColor.Clone();
        clone.BarDownColor = BarDownColor.Clone();
        return clone;
    }
}

public sealed class GridBackgroundSettings
{
    public ColorSetting BackgroundColor { get; set; } = ColorSetting.From(Color.FromRgb(0x18, 0x1A, 0x1F));
    public bool ShowGrid { get; set; } = true;
    public ColorSetting GridColor { get; set; } = ColorSetting.From(Color.FromRgb(0x3A, 0x3F, 0x48));
    public LineStyleOption LineStyle { get; set; } = LineStyleOption.Solid;

    public GridBackgroundSettings Clone()
    {
        var clone = (GridBackgroundSettings)MemberwiseClone();
        clone.BackgroundColor = BackgroundColor.Clone();
        clone.GridColor = GridColor.Clone();
        return clone;
    }
}

public sealed class DrawingToolsSettings
{
    // A single shared default style for all drawing tools (H/V/Trendline today). Per-tool-type
    // individual styling is listed in the full spec but is deferred to "Drawing Tools phase 2"
    // (the unified per-object editing pass already planned for this project) - see
    // docs/CHART_SETTINGS_PLAN.md.
    public ColorSetting DefaultColor { get; set; } = ColorSetting.From(Colors.DodgerBlue);
    public double Thickness { get; set; } = 1.5;
    public LineStyleOption LineStyle { get; set; } = LineStyleOption.Solid;

    public DrawingToolsSettings Clone()
    {
        var clone = (DrawingToolsSettings)MemberwiseClone();
        clone.DefaultColor = DefaultColor.Clone();
        return clone;
    }
}

/// <summary>
/// Root settings object. Chrome/Toolbar colors intentionally live outside this model -
/// they stay theme-driven (Light/Dark/System) per PS's decision.
/// </summary>
public sealed class ChartSettings
{
    public ChartGeneralSettings General { get; set; } = new();
    public AxesSettings Axes { get; set; } = new();
    public CandleSettings Candles { get; set; } = new();
    public GridBackgroundSettings GridAndBackground { get; set; } = new();
    public DrawingToolsSettings DrawingTools { get; set; } = new();

    public static ChartSettings CreateDefault() => new();

    public ChartSettings Clone() => new()
    {
        General = General.Clone(),
        Axes = Axes.Clone(),
        Candles = Candles.Clone(),
        GridAndBackground = GridAndBackground.Clone(),
        DrawingTools = DrawingTools.Clone()
    };

    public void CopyFrom(ChartSettings other)
    {
        General = other.General.Clone();
        Axes = other.Axes.Clone();
        Candles = other.Candles.Clone();
        GridAndBackground = other.GridAndBackground.Clone();
        DrawingTools = other.DrawingTools.Clone();
    }
}
