# ForexPanel-A — Feature Status Checklist

Purpose: a single running record of what actually works vs. what is broken/partial/untested,
verified by running the app, so future work does not re-fix or re-break already-confirmed behavior.
Based on `docs/FOREXPANEL_A_IMPLEMENTATION_BASELINE_2026-09-11.md`.

Legend: ✅ Working | ⚠️ Partial/Broken-in-part | ❌ Broken | 🔲 Not yet tested

| Area | Baseline doc claim | Verified status | Notes |
|---|---|---|---|
| Zoom In | ✅ confirmed | 🔲 | |
| Zoom Out | ✅ confirmed | 🔲 | |
| Reset View | ✅ confirmed | 🔲 | |
| Timeframe (M1–D1) | ✅ confirmed, first-class Toolbar Tool | 🔲 | |
| AutoScroll | ✅ confirmed | 🔲 | |
| ChartShift | ✅ confirmed | 🔲 | |
| Grid (toolbar execution) | ✅ works, currently frozen | 🔲 | do not modify while frozen |
| View → Grid (presence checkbox) | ❌ not correctly wired (deliberately deferred) | 🔲 | needs general View-menu↔Toolbar mechanism |
| Pan | ❌ movement excessively large/uncontrollable | 🔲 | needs mouse→coordinate conversion review |
| Crosshair (pre-Navigation baseline) | ✅ previously worked | 🔲 | |
| Crosshair (unified Navigation mode) | ❌ mode switch fails, can freeze chart | 🔲 | needs ownership/event review |
| Zoom Area | ⚠️ works, but breaks normal price-axis zoom afterward | 🔲 | check capture/handled state |
| Cursor mode | 🔲 needs verification | 🔲 | |
| Volume | 🔲 registered, not fully wired | 🔲 | |
| Drawing tools (H/V/Trend/Ray/Rectangle) | 🔲 registered, not fully wired | 🔲 | |
| Indicators | 🔲 registered, not fully wired | 🔲 | |
| Fibonacci | 🔲 registered, not fully wired | 🔲 | |
| ParallelChannel | 🔲 registered, not fully wired | 🔲 | |
| Delete / DeleteAll | 🔲 registered, not fully wired | 🔲 | |
| Settings | 🔲 registered, not fully wired | 🔲 | |
| Toolbar layout/customization (boxes, move, overflow) | ✅ complete and closed | 🔲 | do not reopen without proven regression |
| Chart context menu | ✅ complete | 🔲 | |
| Model menu (CSV Analysis/Strategy/Backtest) | ✅ placeholder structure added | 🔲 | |
| Theme (dark UI) | ✅ recently stabilized | 🔲 | |
| Toolbar icons | ⚠️ some disappeared during Navigation work | 🔲 | needs regression audit vs. reference icon set |
| Bridge/MT4 communication | ✅ no new failures reported | 🔲 | |

## How to use this file
1. Run the app (`dotnet run --project .\ForexPanel.App\ForexPanel.App.csproj`).
2. Go through each row, replace the "Verified status" 🔲 with ✅ / ⚠️ / ❌ based on actual behavior.
3. Add a short note for anything ⚠️ or ❌ (exact symptom).
4. Commit the updated file so the record persists across sessions.
