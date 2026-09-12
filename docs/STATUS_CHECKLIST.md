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
    Status: UNTESTED -
------------------------------------------------------------

## How to use this file
1. Run the app: dotnet run --project .\ForexPanel.App\ForexPanel.App.csproj
2. Go through each numbered item, replace UNTESTED with OK / PARTIAL / BROKEN.
3. Add a short note after the dash for anything PARTIAL or BROKEN (exact symptom).
4. Commit the updated file so the record persists across sessions.
