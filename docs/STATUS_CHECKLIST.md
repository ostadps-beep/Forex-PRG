# ForexPanel-A — Feature Status Checklist

Purpose: a single running record of what actually works vs. what is broken/partial/untested,
verified by running the app, so future work does not re-fix or re-break already-confirmed behavior.
Based on `docs/FOREXPANEL_A_IMPLEMENTATION_BASELINE_2026-09-11.md`.

Legend: OK = working | PARTIAL = broken in part | BROKEN = not working | UNTESTED = not yet checked

For each item below, replace UNTESTED with OK / PARTIAL / BROKEN after testing, and add a short
note after the colon describing the exact symptom if it's PARTIAL or BROKEN.

------------------------------------------------------------
1. Zoom In
   Baseline claim: confirmed working
   Status: OK - confirmed by PS

2. Zoom Out
   Baseline claim: confirmed working
   Status: OK - confirmed by PS

3. Reset View
   Baseline claim: confirmed working
   Status: OK - PS confirmed working (via the shared PerformResetView() method, also confirmed through the right-click "Reset View" context-menu item). Implemented from scratch (commit 1019dbd/599033c): restores default bar spacing/right-offset on the time axis and auto-scales the price axis to whatever candles end up visible.

4. Timeframe (M1/M5/M15/M30/H1/H4/D1)
   Baseline claim: confirmed working, first-class Toolbar Tool
   Status: OK - PS confirmed working. Fix was widening the combo box 58px→68px so two-digit labels (M15/M30/H4) display fully (commit 599033c).

5. AutoScroll
   Baseline claim: confirmed working
   Status: FIXED (needs PS verification) - was actually not implemented at all: the "AutoScroll" toolbar tool had zero click handling (confirmed by code review). Implemented from scratch (commit 1019dbd) as a toggle: when turned ON, immediately snaps/pins the view to the latest candle (right-offset reset to 0). Note: there is no live data feed yet to continuously re-anchor to as new bars arrive (that's separate, later work per the candle-engine scope decision) - so this delivers the "jump to and stay pinned on latest" behavior that's testable right now, not continuous live tracking. PLEASE VERIFY: pan/scroll away from the latest candle, then click AutoScroll - it should jump back to show the most recent candle and toggle-highlight correctly.
   CLARIFICATION (PS follow-up): the "AutoScroll in the right-click menu" PS remembered was actually ScottPlot's own built-in "Autoscale" default context-menu item, unrelated to the toolbar AutoScroll tool - it called the raw AutoScale() which fits ALL candles into view (hence the "compressed" look), completely bypassing our CandleLayoutModel. Fixed and PS confirmed OK (commit 599033c): that context-menu item is now repointed to the same proper Reset View logic as the toolbar's Reset button (relabeled "Reset View" instead of "Autoscale").
   The toolbar AutoScroll toggle itself (jump-to-latest behavior) is still PLEASE VERIFY - not yet explicitly re-confirmed by PS.

6. ChartShift
   Baseline claim: confirmed working
   Status: FIXED (needs PS verification) - was actually not implemented at all: the "ChartShift" toolbar tool had zero click handling (confirmed by code review). Implemented from scratch (commit 1019dbd) as a toggle: when ON, adds MT4-style blank space after the last candle (15% of the visible span) instead of pinning the last candle to the chart's right edge; toggling OFF removes it again. Known minor approximation noted in code comments: bar-spacing recalculated immediately after toggling while zoomed may be very slightly off, since the visible span briefly includes the blank margin - not a functional break. PLEASE VERIFY: click ChartShift - a visible gap should appear after the last candle; click again to remove it; also check it stays correctly highlighted/un-highlighted across theme switches.

7. Grid (toolbar execution)
   Baseline claim: works, currently frozen - do not modify while frozen
   Status: PARTIAL - works but only a basic/initial implementation, not yet fully complete (still frozen, do not modify without PS's go-ahead)
   NOTE (PS, 2026-09-13): Grid needs multiple configurable options (spacing/style/color etc.), which requires a general Chart Settings dialog/infrastructure to exist FIRST before those options can be added - this is scoped as a future task, not started yet.

8. View menu -> Grid (presence checkbox)
   Baseline claim: not correctly wired, deliberately deferred
   Status: UNTESTED -

9. Pan
   Baseline claim: BROKEN - movement excessively large / uncontrollable
   Status: RESOLVED-DIFFERENTLY - the dedicated Pan toolbar icon was removed/set aside, since panning is now handled natively by the chart itself (ScottPlot's own built-in pan) instead of through the toolbar tool

10. Crosshair (basic, pre-Navigation)
    Baseline claim: previously worked
    Status: OK - PS confirmed Crosshair works

11. Crosshair (in unified Navigation mode)
    Baseline claim: BROKEN - mode switch fails, can freeze the chart
    Status: PARTIAL - Crosshair itself now works (PS confirmed), but the mode is not correctly "de-selected"/reset after use (see item 13, Cursor mode) - contradicts baseline's freeze report, but a related mode-management issue remains

12. Zoom Area
    Baseline claim: PARTIAL - works, but breaks normal price-axis mouse zoom afterward
    Status: OK - PS confirmed it works (no mention of the price-axis zoom breakage from the baseline doc - may be fixed, keep an eye on it)

13. Cursor mode
    Baseline claim: needs verification
    Status: OK - PS confirmed working. Cursor now acts as the standard/neutral pointer mode (commit 599033c) - clicking it deactivates Crosshair if it's currently active (and un-highlights that button).

14. Volume
    Baseline claim: registered in toolbar, not fully wired
    Status: UNTESTED -

15. Drawing tools (Horizontal Line, Vertical Line, Trend Line, Ray, Rectangle)
    Baseline claim: registered in toolbar, not fully wired
    Status: PARTIAL - BUILT for 3 of 5 (needs PS verification, commit f29b4d1, 2026-09-14). Horizontal Line, Vertical Line, and Trend Line are now real: click the toolbar icon to arm it (radio-group style - only one active, click again to cancel), click the chart to place (Trend Line needs a second click for the end point), styled live from Chart Settings > Drawing Tools (color/thickness/line style - all three now genuinely wired, previously only color was and line style was disabled). Delete removes the most recently placed drawing. Ray and Rectangle are still unwired (Ray needs one-sided-infinite-line logic; Rectangle needs a filled/outlined box between two points - neither built yet). KNOWN PHASE-1 LIMITATION: placed drawings do not persist across timeframe/symbol changes yet - they're cleared along with the rest of the chart on Rebuild, matching the "simple draw only" phase used for the sibling project before its own per-object editing pass. PLEASE VERIFY: place a horizontal line, a vertical line, and a trend line (2 clicks); change their color/thickness/style in Settings and place new ones to confirm the style applies; use Delete to remove the last one; confirm clicking Cursor or the same tool again cancels an armed tool.
    FOLLOW-UP FIX (PS found this on first test, commit b421b1e, 2026-09-14): Horizontal Line drew in the middle of the chart regardless of click position, and Trend Line didn't draw at all. Root cause: HorizontalLine/LinePlot both default to a brand-new independent Y-axis object (not the chart's actual Right/price axis) when created via Plot.Add - so the computed price coordinate was correct but got rendered against the wrong axis mapping (auto-scaled to just that one line, landing near the middle, or off-screen entirely for the two-point trend line). Fixed by explicitly setting .Axes.YAxis = Chart.Plot.Axes.Right on all three drawing types (Vertical Line already worked since it only depends on the X-axis, which matched by coincidence). PLEASE RE-VERIFY all three at various click positions.

16. Indicators
    Baseline claim: registered in toolbar, not fully wired
    Status: UNTESTED -

17. Fibonacci
    Baseline claim: registered in toolbar, not fully wired
    Status: UNTESTED -

18. Parallel Channel
    Baseline claim: registered in toolbar, not fully wired
    Status: UNTESTED -

19. Delete / Delete All
    Baseline claim: registered in toolbar, not fully wired
    Status: UNTESTED -

20. Settings
    Baseline claim: registered in toolbar, not fully wired
    Status: UNTESTED -

21. Toolbar layout/customization (custom boxes, move, overflow menu)
    Baseline claim: complete and closed - do not reopen without proven regression
    Status: OK - implemented; PS notes it may need changes/revisions in the future

22. Chart context menu (right-click)
    Baseline claim: complete
    Status: UNTESTED -

23. Model menu (CSV Analysis / Strategy / Backtest placeholders)
    Baseline claim: placeholder structure added
    Status: UNTESTED -

24. Theme (dark UI across title bar, menu, toolbar, chart)
    Baseline claim: recently stabilized
    Status: OK - implemented; PS notes it may need changes/revisions in the future

24b. Color system
    Baseline claim: (not covered in baseline doc)
    Status: OK - implemented, per PS

24c. Candle display settings
    Baseline claim: (not covered in baseline doc)
    Status: UNTESTED - PS has no information on this in the current version, needs review

25. Toolbar icons (overall visual check)
    Baseline claim: PARTIAL - some icons disappeared during Navigation work
    Status: UNTESTED -

26. Bridge / MT4 pipe communication
    Baseline claim: no new failures reported
    Status: UNTESTED - note: the pipe only implements a connection-test handshake (HELLO/WELCOME/PING/PONG); it does not send or receive any real price/candle data

27. Candle calculation engine
    Baseline claim: (not covered in baseline doc)
    Status: FIXED (needs PS verification) - added a real resampling engine, decoupled from any live data source per PS's decision (data ingestion is a separate later step). New `ForexPanel.Core/CandleResampler.cs`: real OHLC aggregation (Open=first/High=max/Low=min/Close=last/Volume=sum) from an M1 baseline into any higher timeframe - fixed-minute buckets for M5/M15/M30/H1/H4, calendar-day buckets for D1 (and ready for W1/MN later). `ChartController` no longer calls the old `CreateTestCandles()` (which generated fake data directly at the target timeframe's spacing, so every timeframe switch was a totally different fake dataset). It now generates one M1 baseline per symbol (`GenerateM1Baseline`, ~260 days, cached in `baselineCache`) and resamples from that same baseline via `CandleResampler.Resample()` for every timeframe - so switching timeframe now shows a consistent, correctly-aggregated view of the SAME underlying price history instead of unrelated random data each time. Note: the M1 baseline itself is still placeholder/generated data, NOT live MT4 data - that is intentionally out of scope for now per PS.
    FOLLOW-UP FIX (PS found this on first test): H4 (and any timeframe >60 min) candles were rendering bunched/compressed together, while other timeframes rendered evenly spaced. Root cause: the fixed-minute bucketing floored `c.Time.Minute` (always 0-59) against the target size (240 for H4), which is always 0 - so H4 buckets were silently collapsing to 1-hour granularity while the candle body was still drawn 4-hours wide, causing overlap. Fixed by flooring against minutes-since-midnight instead of minutes-within-hour (commit 0ba977d).
    PS CONFIRMED WORKING after this fix (2026-09-13).

28. Theme system reaching the chart itself
    Baseline claim: (not covered in baseline doc)
    Status: FIXED (needs PS verification) - ChartController now owns axis-text/candle-up/candle-down/crosshair colors via a new ApplyTheme() method, cached and automatically re-applied on every Rebuild (so timeframe/symbol changes no longer reset the chart to hardcoded white/default colors). MainWindow's ApplyChartSurfaceTheme() now also reads Color.Chart.CandleUp/CandleDown/Crosshair and passes them through. The duplicate Resources/GlobalColors.xaml (which had drifted values) now simply merges Theme/DarkTheme.xaml instead of redefining every color a second time. Also added the missing SubmenuContent ControlTemplate to DarkTheme.xaml (LightTheme.xaml had it, Dark didn't).
    FOLLOW-UP FIX (PS found this on first test): if a toggle-style toolbar button (Crosshair, Grid) was left in its selected/highlighted state and the theme was then switched, the highlight color did NOT update to the new theme - it kept showing the old theme's color. Root cause: these two buttons set their active Background/BorderBrush imperatively via TryFindResource(...) at click time, not via a {DynamicResource ...} XAML binding, so they don't auto-update when the resource dictionary changes. Fixed (commit a03b86f): ApplyTheme() now calls a new RefreshToggleToolbarButtonBackgrounds() that walks the toolbar's visual tree and re-applies fresh theme colors to any currently-active toggle button.
    PS CONFIRMED WORKING after both fixes (2026-09-13).

29. Chart Settings window (new, per PS's spec docs/CHART_SETTINGS_FULL_SPEC.txt)
    Baseline claim: (not covered in baseline doc - new feature)
    Status: BUILT (needs PS verification) - full 10-category sidebar (Chart, Axes, Candles, Grid & Background, Drawing Tools, Analytical Modules, HUD & Overlay, Performance, Workspace, Advanced), Header+Content+Footer(Apply/OK/Cancel/Reset to Default), opened via the toolbar's Settings button. Only the first 5 categories have real live-apply controls; the other 5 show a "not yet available" placeholder (reserved for this project's future roadmap and the separate C++/Python engine merge PS mentioned). Colors use a base color + adjustable shade% (ColorSetting class) per PS's professional-color requirement. Individual fields with no real backing yet (non-Candlestick chart types, wick/body visibility toggles, axis position/format overrides, mouse-wheel-zoom override) are shown disabled with a tooltip explaining why, rather than silently doing nothing. KNOWN LIMITATION (documented in code/plan): if the Light/Dark theme is switched AFTER customizing Candle/Axis colors here, the theme switch will currently overwrite them again (last-write-wins) - proper Settings-over-Theme precedence is a follow-up. PLEASE VERIFY: open Settings from the toolbar, try changing candle colors/shade, grid color/style/visibility, background color - confirm live preview works, and test Apply/OK/Cancel/Reset to Default.
    FOLLOW-UP FIX (PS found this on first test): the color-picker popup had a hardcoded white background while its text inherited the theme's light foreground color, making the text nearly invisible ("white halo" look). Fixed (commit f11d4ac): the popup and sidebar now use theme-aware live resource bindings (SetResourceReference) instead of hardcoded colors/one-time snapshots, matching the rest of the app. PLEASE RE-VERIFY: open a color picker and confirm the popup text is clearly readable.
    UPGRADE (PS requested, 2026-09-13): replaced the hex-only color picker with a full HSV picker (commit a169fec) - a draggable saturation/value square, a draggable hue spectrum bar, and a row of 10 muted professional preset swatches (click to apply instantly), alongside the existing hex textbox and shade% slider. All controls stay in sync with each other.
    PS CONFIRMED WORKING (2026-09-13).
    DEEPER WIRING (PS asked to push this as far as technically feasible before moving on, commit 0a19375, 2026-09-13): Show Wicks/Show Body (Candles tab) are now real - a new `ConfigurableCandlestickPlot` subclass overrides ScottPlot's Render() to allow hiding wick or body independently (the stock CandlestickPlot has no such option, but Render() is virtual and every member it needs is public, so this was feasible without forking the library). Show Horizontal Grid/Show Vertical Grid (Axes tab) are now real and independent of each other (IGrid.XAxisStyle/YAxisStyle.IsVisible, combined with the master Grid&Background "Show Grid" toggle). Axis Thickness (Axes tab) is now real (FrameLineStyle.Width on Bottom+Right axes) and got a slider control it was previously missing.
    STILL NOT WIRED (genuinely require bigger separate work, not quick wins - left honestly disabled): Chart Type other than Candlestick (Line/Histogram/Combined would need a real alternate rendering pipeline), Visible Candles count (would need CandleCount to become dynamic instead of a fixed constant), Auto Fit toggle (not connected to any behavior), Zoom Behavior Time/Price/Both (current zoom already conflates both axes; splitting them needs zoom-logic rework), mouse-wheel-zoom override (deliberately left disabled - would conflict with the verified MT4-standard wheel=pan convention), price-axis Show Last Price, Decimal Places, and time-axis Position/Format (need deeper tick-label-formatter work).
    PS initially found Show Wicks/Show Body only worked one-way (turning off worked, turning back on had no visual effect) - turned out to be a stale build cache, not a code bug. PS CONFIRMED WORKING after a clean `dotnet clean` + rebuild (2026-09-13).
------------------------------------------------------------

30. Chart Type toolbar icons (Candlestick/Hollow Candlestick/Bar/Line/Area)
    Baseline claim: (not covered in baseline doc - gap identified 2026-09-14)
    Status: BUILT (needs PS verification) - PS clarified this checked git history first (all 30 commits of this repo's history were reviewed - no prior trace found; likely conflated with the sibling ForexAnalysis project, or lost in an earlier repo-copy/access-transition PS described). Implemented from scratch (commits 0b91519, e7bbab6, 2026-09-14): 5 real rendering modes now exist - Candlestick, Hollow Candlestick (new `ConfigurableCandlestickPlot.HollowBody` - draws an outline-only body via `Drawing.DrawRectangle` instead of a filled rect, with its own dedicated HollowUpColor/HollowDownColor settings per PS's explicit request), Bar (ScottPlot's built-in `OhlcPlot`), Line (Scatter of Close prices, no markers), Area (`FillY` from Close down to the visible low as baseline). `ChartController.SetChartType()` switches modes by reusing the already-loaded candle data (no re-fetch). 5 new toolbar icons added (hand-drawn 16x16 geometries in MainWindow.xaml, following the existing icon-system convention - regular Candlestick uses a special-cased filled body like the sibling project does, matching its own default hollow-by-transparency look for every other icon). Toolbar buttons act as a radio group (only one highighted at a time) even though the toolbar model has no built-in mutual-exclusivity - handled by `RefreshChartTypeToolbarHighlight()`, kept in sync with the Settings window's Chart Type dropdown and refreshed on theme switch. Toolbar `LayoutVersion` bumped 4->5 since the tool catalog changed (existing saved layouts are safely ignored/reset, not merged incorrectly). PLEASE VERIFY: click through all 5 toolbar chart-type icons, confirm each renders correctly and only one shows highlighted; open Settings > Candles and set Hollow Up/Down colors, then switch to Hollow Candlestick and confirm those specific colors show (not the regular Bullish/Bearish ones).
    FOLLOW-UP FIXES (PS found these on first test, commit ad5b041, 2026-09-14):
    (a) Line/Area had no color settings and changed color on every switch - was ScottPlot auto-assigning a palette color to each newly-created Scatter/FillY plottable since none was ever set explicitly. Fixed: added General.LineColor/AreaColor (Area defaults to a common pale translucent blue), applied every time settings are pushed to the chart.
    (b) No thickness setting existed for candle wicks/hollow outline. Added CandleSettings.WickThickness (regular candles, in the main Candles section) and HollowThickness (in the Hollow Candles section) - each in its own relevant part of the panel as PS asked.
    (c) Hollow candle bodies showed a line running straight through the empty interior (the wick, drawn top-to-bottom in one piece, was visible through the un-filled body). Fixed: when HollowBody is on, the wick is now drawn as two separate segments (above and below the body box), leaving the interior clean.
    (d) All Button controls in the Settings window (Apply/OK/Cancel/Reset to Default, also the color-picker swatch buttons) turned solid white on mouse-hover. Root cause: WPF's native button chrome ignores plain Background/BorderBrush Style Setters unless the Style also provides a real ControlTemplate - a project-wide gap, not specific to these 4 buttons. Fixed at the theme level (both DarkTheme.xaml and LightTheme.xaml) so every Button anywhere in the app is now correctly themed on hover/press, not just Settings.
    PLEASE RE-VERIFY all four.
31. Chart Settings > Chart tab redesign (MT4 Common/Colors reference)
    Baseline claim: (not covered in baseline doc - PS provided MT4 screenshots as reference, 2026-09-14)
    Status: BUILT (needs PS verification) - rebuilt the Chart tab to match MT4's own Common properties tab layout: two columns, left = checkboxes (Offline chart/Chart on foreground disabled-placeholder, Chart shift/Chart autoscroll REAL and shared with the toolbar toggles, Scale fix variants disabled-placeholder), right = chart-type radio group (Bar/Candlesticks/Hollow candlesticks/Line/Area, all real) + checkboxes (Show OHLC/Ask line/period separators/volumes/object descriptions disabled-placeholder, Show grid REAL and cross-linked to the same value as the Grid & Background tab's own checkbox). Chart Shift/Auto Scroll state moved from private MainWindow fields into the shared ChartSettings model so the toolbar buttons and this tab always agree. Also per PS's feedback: shrunk the color-picker swatch box, and gave every Slider in the app a slim custom look (thin 2px line track, small 12px round thumb) instead of the bulkier default. PLEASE VERIFY: toggle Chart shift/Auto scroll from this tab and confirm the toolbar buttons highlight to match (and vice versa); toggle Show grid here and confirm it matches the Grid & Background tab; try the chart-type radio buttons; check slider look/feel and the smaller color swatches.
    CLARIFICATION (PS, 2026-09-14): the goal was never to visually clone MT4 - the existing two-column layout stays exactly as built. The goal is full COVERAGE of MT4's option list (so nothing is missing / no need to cross-reference other software later), added alongside what already exists, not replacing it. PS asked to confirm understanding before proceeding - confirmed. Two gaps filled (commit a2dd24c): (a) Scale fix "Fixed maximum"/"Fixed minimum" textboxes were entirely missing - added as disabled placeholders under Scale fix. (b) Bar Up/Bar Down colors (from PS's Colors reference image) were a genuine, real gap - the Bar chart type had NO color settings at all, always using ScottPlot's own default colors; added CandleSettings.BarUpColor/BarDownColor (defaults teal/maroon matching PS's reference) and wired them into the OhlcPlot's RisingStyle/FallingStyle. PS also reported a color-picker bug ("some boxes show only one sample color, no sign of the real assigned colors") but did not yet specify exactly where - still needs to be pinned down and fixed.

32. Line/Area color+thickness in Candles tab, Area timeframe-drift fix, centralized Grid controls in Chart tab
    Status: OK - PS confirmed all working (commit 91cdbfb, 2026-09-14).
    OPEN QUESTION (PS, 2026-09-14, deferred for later discussion): the toolbar already has a Grid on/off icon - is a SEPARATE "Show grid" checkbox in the Chart settings tab actually needed, or is having both slightly confusing/overlapping? Not resolved yet - PS wants to revisit this later, not decide now.

## How to use this file
1. Run the app: dotnet run --project .\ForexPanel.App\ForexPanel.App.csproj
33. Axes tab review (per PS's priority pick, 2026-09-14)
    Status: BUILT (needs PS verification) - made 3 previously-disabled items genuinely real (commit 384035b): Decimal Places (price axis tick labels now use a real F-format formatter via ScottPlot's NumericAutomatic.LabelFormatter, 0-8 decimals clamped), Time Format (HH:MM/Date/Combined, via DateTimeAutomatic.LabelFormatter), Show Last Price (a dashed horizontal line + text label at the latest close, colored to match axis text, correctly recreated every Rebuild since Plot.Clear() invalidates the old plottable). STILL DISABLED (genuinely require a bigger architecture change, left honestly placeholder): Price Axis Position (Left) and Time Axis Position (Top) - the current renderer always docks price-right/time-bottom; Show Sessions (never implemented, no session-marking concept exists). PLEASE VERIFY: change Decimal Places and confirm the price axis labels update; try all 3 Time Format options; toggle Show Last Price and confirm a dashed line with the current close price appears/disappears, and updates correctly across timeframe/symbol changes.
    FOLLOW-UP (PS, 2026-09-14, commit 805d577): Show Last Price was reusing the axis text color, causing visual overlap/confusion with the axis. Fixed: added dedicated Axes.LastPriceLineColor and LastPriceLineStyle (Solid/Dash/Dot) settings, fully independent from the axis color.
    CLARIFICATION (PS asked, 2026-09-14): the line doesn't move continuously with "live" price because there is no live data feed yet (data ingestion is deliberately out of scope per the earlier candle-engine decision) - it shows the Close of the last candle in the static generated dataset, updated only when settings are (re)applied. This is expected, not a bug. ApplyLastPriceLine() is already structured to update correctly once a live feed exists later - just needs to be called from wherever live ticks eventually arrive.

## How to use this file
1. Run the app: dotnet run --project .\ForexPanel.App\ForexPanel.App.csproj
2. Go through each numbered item, replace UNTESTED with OK / PARTIAL / BROKEN.
3. Add a short note after the dash for anything PARTIAL or BROKEN (exact symptom).
4. Commit the updated file so the record persists across sessions.
