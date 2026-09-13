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
   Status: BROKEN - does not work (contradicts baseline doc's "confirmed" claim)

4. Timeframe (M1/M5/M15/M30/H1/H4/D1)
   Baseline claim: confirmed working, first-class Toolbar Tool
   Status: PARTIAL - display bug with two-digit timeframe numbers (e.g. M15, M30, H4): the second digit is not visible/rendered

5. AutoScroll
   Baseline claim: confirmed working
   Status: PARTIAL - toolbar icon is disabled/inactive; works only via right-click, but reverts to an initial state with candles bunched/compressed together

6. ChartShift
   Baseline claim: confirmed working
   Status: BROKEN - does not work (contradicts baseline doc's "confirmed" claim)

7. Grid (toolbar execution)
   Baseline claim: works, currently frozen - do not modify while frozen
   Status: PARTIAL - works but only a basic/initial implementation, not yet fully complete (still frozen, do not modify without PS's go-ahead)

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
    Status: PARTIAL - needs proper state management: after selecting Cursor/Crosshair mode, it should be able to deactivate/reset to neutral, but currently it does not

14. Volume
    Baseline claim: registered in toolbar, not fully wired
    Status: UNTESTED -

15. Drawing tools (Horizontal Line, Vertical Line, Trend Line, Ray, Rectangle)
    Baseline claim: registered in toolbar, not fully wired
    Status: UNTESTED -

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
    Status: FIXED (needs PS verification) - added a real resampling engine, decoupled from any live data source per PS's decision (data ingestion is a separate later step). New `ForexPanel.Core/CandleResampler.cs`: real OHLC aggregation (Open=first/High=max/Low=min/Close=last/Volume=sum) from an M1 baseline into any higher timeframe - fixed-minute buckets for M5/M15/M30/H1/H4, calendar-day buckets for D1 (and ready for W1/MN later). `ChartController` no longer calls the old `CreateTestCandles()` (which generated fake data directly at the target timeframe's spacing, so every timeframe switch was a totally different fake dataset). It now generates one M1 baseline per symbol (`GenerateM1Baseline`, ~260 days, cached in `baselineCache`) and resamples from that same baseline via `CandleResampler.Resample()` for every timeframe - so switching timeframe now shows a consistent, correctly-aggregated view of the SAME underlying price history instead of unrelated random data each time. Note: the M1 baseline itself is still placeholder/generated data, NOT live MT4 data - that is intentionally out of scope for now per PS. PLEASE VERIFY: pull, rebuild, run, and confirm timeframe switching now looks coherent (e.g. an H4 candle's range should visually correspond to the M1/M5 candles under it, not look unrelated).

28. Theme system reaching the chart itself
    Baseline claim: (not covered in baseline doc)
    Status: FIXED (needs PS verification) - ChartController now owns axis-text/candle-up/candle-down/crosshair colors via a new ApplyTheme() method, cached and automatically re-applied on every Rebuild (so timeframe/symbol changes no longer reset the chart to hardcoded white/default colors). MainWindow's ApplyChartSurfaceTheme() now also reads Color.Chart.CandleUp/CandleDown/Crosshair and passes them through. The duplicate Resources/GlobalColors.xaml (which had drifted values) now simply merges Theme/DarkTheme.xaml instead of redefining every color a second time. Also added the missing SubmenuContent ControlTemplate to DarkTheme.xaml (LightTheme.xaml had it, Dark didn't). PLEASE VERIFY: rebuild, switch Light/Dark/timeframe repeatedly and confirm candle up/down colors and axis text stay themed, and that dark-mode submenus render with the dark background/border.
------------------------------------------------------------

## How to use this file
1. Run the app: dotnet run --project .\ForexPanel.App\ForexPanel.App.csproj
2. Go through each numbered item, replace UNTESTED with OK / PARTIAL / BROKEN.
3. Add a short note after the dash for anything PARTIAL or BROKEN (exact symptom).
4. Commit the updated file so the record persists across sessions.
