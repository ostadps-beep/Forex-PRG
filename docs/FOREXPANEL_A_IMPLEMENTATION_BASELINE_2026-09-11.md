# ForexPanel-A — Master Implementation Baseline / Handoff

Date: 2026-09-11  
Repository: `ostadps-beep/ForexPanel-A`  
Branch: `main`  
Local path: `C:\Users\OstadPS\Desktop\ForexPanel-A`  
Solution: `ForexPanel.slnx`  
Application: `ForexPanel.App`  
Target: `net10.0-windows`

## 1. Purpose

This is the master handoff for the recent ForexPanel-A development cycle. It records completed architecture, confirmed behavior, known partial/broken behavior, deferred work, safety boundaries, and the next implementation direction. Future chats must use this document together with the current GitHub `main` state instead of redesigning completed work.

## 2. Source of truth and workflow

- GitHub `main` is the authoritative project source.
- Local project is updated by pulling from GitHub; the user does not manually edit project files unless explicitly agreed.
- After a GitHub code/document change, synchronize locally before testing.
- Standard local sequence:

```powershell
cd C:\Users\OstadPS\Desktop\ForexPanel-A
git pull origin main
dotnet build .\ForexPanel.slnx
```

- After a successful build:

```powershell
dotnet run --project .\ForexPanel.App\ForexPanel.App.csproj --no-build
```

- A feature is not marked complete merely because Build succeeds. Runtime behavior must be explicitly tested and confirmed.
- Do not use `git diff` for this project.
- For risky code changes, create a backup branch first.

## 3. Critical architecture rules

### View menu versus Toolbar execution

These are two different responsibilities:

- View-menu checkboxes control whether the corresponding tool/icon is present or visible in the Toolbar.
- Toolbar buttons execute the actual runtime action.
- A Toolbar click must not automatically toggle the View-menu presence checkbox.
- Grid is the clearest current example of this distinction.
- The future solution for View-menu presence is a reusable View-menu ↔ Toolbar presence/visibility mechanism, not coupling menu presence to runtime execution.

### ChartController boundary

- `ChartController.cs` is a protected/sensitive component during Navigation/Toolbar wiring.
- For Navigation wiring, treat it as read-only and do not modify it unless a concrete, separately approved requirement makes a change unavoidable.
- Grid behavior is frozen during Navigation architecture work.

### Interaction architecture target

The intended Navigation structure is:

Toolbar → Central Tool Request / Command Router → independent Chart Interaction Layer → Cursor / Crosshair / Pan / Zoom Area → existing ChartController → ScottPlot.

Interaction modes should be unified rather than scattered across unrelated MainWindow mouse handlers.

## 4. Toolbar framework — implemented

The Toolbar is now a real framework rather than a collection of unrelated buttons.

- Groups can exist across two rows.
- Groups have order/priority and row placement.
- Groups can be created, deleted, moved, reordered, and moved between rows.
- Tools have IDs, titles/tooltips, icon keys, order/priority, overflow capability, toggle metadata, and text-only metadata.
- Layout persistence uses `%LOCALAPPDATA%\ForexPanel\toolbar-layout.json`.
- Current layout persistence version is 4.
- Toolbar overflow is implemented.
- Toolbar buttons retain the actual `ToolbarTool` in their `Tag`, allowing exact ID-based routing instead of tooltip-text matching.

Current catalog:

`Cursor`, `Crosshair`, `Timeframe`, `ZoomIn`, `ZoomOut`, `ZoomArea`, `Reset`, `AutoScroll`, `ChartShift`, `Grid`, `Volume`, `HorizontalLine`, `VerticalLine`, `TrendLine`, `Ray`, `Rectangle`, `Indicators`, `Fibonacci`, `ParallelChannel`, `Delete`, `DeleteAll`, `Settings`.

## 5. Toolbar customization — implemented and closed

- Custom toolbar boxes are supported on both rows.
- Empty custom boxes are visibly rendered as real small rectangles.
- New Box creation works.
- Customize works.
- Delete Box works.
- Move Box works.
- Groups and tool order can be customized.
- The New Box implementation is CLOSED and must not be reopened unless a new regression is proven.

The empty-box visibility fix used `MinWidth=30` and `MinHeight=30` on the ToolbarControl border.

## 6. Toolbar execution plumbing

`ToolbarTool.Run()` is centralized:

- If `Execute` is assigned, it invokes the delegate.
- Otherwise it raises `ToolbarTool.ToolRequested` with the tool ID.

This establishes a reusable command/request path for future tool behavior.

The current runtime route in `MainWindow` resolves the exact `ToolbarTool.Id`.

## 7. Timeframe — important architectural milestone

Timeframe was deliberately converted from an independent timeframe control into an official first-class Toolbar Tool.

Current definition:

- Tool ID: `Timeframe`
- `IsTextOnly = true`
- `IsToggle = false`
- No icon
- Supported values: M1, M5, M15, M30, H1, H4, D1
- Toolbar exposes `TimeframeChanged`
- MainWindow maps the selected timeframe to minutes and calls `ChartController.Rebuild(currentSymbol, candleMinutes)`

Mapping:

- M1 → 1
- M5 → 5
- M15 → 15
- M30 → 30
- H1 → 60
- H4 → 240
- D1 → 1440

This is an architectural decision: Timeframe must not be converted back into an independent control. It also proves that the Toolbar framework supports specialized text-only first-class tools, not only icon buttons.

A dedicated architecture note also exists at `docs/TOOLBAR_TIMEFRAME_ARCHITECTURE.md`.

## 8. Crosshair — implemented runtime behavior, but Navigation integration currently problematic

Implemented Toolbar behavior:

- Crosshair is a Toolbar toggle.
- Runtime state is held by `MainWindow.crosshairEnabled`.
- Toolbar click toggles the state.
- Active visual state is applied to the button.
- ScottPlot Crosshair plottables have visibility updated from that state.
- Symbol/timeframe rebuilds re-apply visibility.
- Startup default is off in the current toolbar behavior.

However, the later unified Navigation wiring introduced a conflict: selecting the Crosshair Toolbar icon did not correctly switch modes and could freeze the chart. The existing Crosshair creation/control path and mouse-event ownership must therefore be reviewed before further patching. Do not treat the current Navigation-mode integration as complete.

## 9. Grid — implemented, currently frozen

Grid runtime state is implemented in `ChartController`:

- `gridEnabled` state exists.
- `GridEnabled` property exists.
- `SetGridEnabled(bool)` updates `Plot.Grid.IsVisible`.
- Grid configuration preserves the current state instead of forcing Grid on.
- Grid state survives chart rebuilds.
- Toolbar Grid click is wired and works.
- Toolbar Grid click toggles chart Grid and updates the button pressed visual.
- Toolbar Grid handling resolves the tool by `ToolbarTool.Id`.

Important distinction:

- `View → Grid` is a Toolbar presence/visibility configuration item and is not correctly wired yet.
- Do not fix `View → Grid` by coupling it to Toolbar Grid execution.
- The future fix should be a general reusable View-menu ↔ Toolbar presence/visibility system.
- Grid is frozen while Navigation wiring is completed.

## 10. Other confirmed Toolbar/runtime capabilities

Before the current Navigation investigation, the following were confirmed working:

- Zoom In
- Zoom Out
- Reset View
- Timeframe
- AutoScroll
- ChartShift

These confirmed behaviors must be preserved during future changes.

The following Toolbar tools are registered but are not yet all fully wired to final runtime behavior: Pan, Zoom Area, Cursor, Volume, drawing tools, Indicators, Fibonacci, ParallelChannel, Delete, DeleteAll, Settings, and other catalog entries as applicable.

## 11. Navigation wiring — current status

The intended next architecture is one independent Chart Interaction Layer behind the Toolbar command router.

Latest known compile fix in the Navigation phase:

- `ef21726` — use `UIElement.PreviewMouseLeftButtonDownEvent` / `PreviewMouseLeftButtonUpEvent` rather than the incorrect Mouse event names.

Current known issues:

### Pan — FAILED

Symptom: chart movement is excessively large and difficult to control.

Required investigation: mouse movement → chart coordinate conversion and pan sensitivity.

### Crosshair — FAILED in unified Navigation mode

Symptom: Crosshair existed and worked as an existing chart feature, but selecting the Toolbar icon during Navigation wiring did not correctly switch modes and could freeze the chart.

Required investigation: existing Crosshair creation/control, event ownership, and conflict with the new mode path.

### Zoom Area — PARTIAL

Rectangle selection and initial zoom work, but after Zoom Area the normal mouse zoom from the price axis no longer works.

Required investigation: mouse capture/handled state and whether Zoom Area leaves an event captured or handled in a way that blocks ScottPlot's normal price-axis interaction.

### Cursor

Verify as part of the unified interaction architecture review.

Do not patch Pan, Crosshair, and Zoom Area as unrelated isolated fixes before understanding the complete interaction ownership model.

## 12. Icon system and regression requirement

Toolbar icons use WPF vector `Geometry` resources and `ToolIconStyle` rather than bitmap assets.

Known Geometry keys include:

`Icon.Cursor`, `Icon.Crosshair`, `Icon.Pan`, `Icon.ZoomIn`, `Icon.ZoomOut`, `Icon.ZoomArea`, `Icon.Reset`, `Icon.AutoScroll`, `Icon.ChartShift`, `Icon.Grid`, `Icon.Volume`, `Icon.HorizontalLine`, `Icon.VerticalLine`, `Icon.TrendLine`, `Icon.Ray`, `Icon.Rectangle`, `Icon.Indicators`, `Icon.Fibonacci`, `Icon.Delete`, `Icon.DeleteAll`, `Icon.Settings`.

The icon style uses a transparent fill, dynamic stroke color, and a thin professional vector stroke.

During Navigation wiring, some Toolbar icons were observed to disappear. This is a required regression investigation before Navigation wiring can be declared complete.

Procedure:

1. Identify exactly which icon keys/tools disappeared.
2. Compare current Toolbar registration with the previously confirmed icon set and archived/reference Toolbar files.
3. Preserve confirmed icons unless a deliberate replacement is documented.
4. Check both ToolbarManager/ToolbarTool registration and Geometry resource keys.
5. Do not use arbitrary replacement icons merely to make a missing icon visible.
6. Test visually after Build/Run.

## 13. Chart context menu — implemented

Recent work included:

- Restoring caption icons.
- Styling the ScottPlot chart context menu.
- Fixing build issues in chart-menu XAML/code.
- Widening/cleaning menu styling.
- Fixing menu border/width presentation.

This area should not be redesigned during Toolbar execution wiring unless a concrete regression is found.

## 14. Model menu — implemented structure

The Model menu was added with placeholders for future modules:

- CSV Analysis
- Strategy
- Backtest

This menu is separate from Theme and Toolbar behavior.

## 15. Theme/UI work — recent state

The application has a dark professional UI direction covering the title bar, menu, toolbar, and chart surfaces.

Recent theme milestones included:

- Fix custom title bar hit testing and chart host theme surface.
- Fix ScottPlot color conversion compile error.
- Apply theme at startup and fix chart grid/axis label theming.
- Improve dark-theme Grid contrast.
- Improve dark-theme Timeframe text contrast.

Recurring build warnings have included:

- NU1701 for `SkiaSharp.Views.WPF 3.119.0` being restored from .NET Framework rather than the target Windows framework.
- NU1900 when NuGet vulnerability lookup is unavailable.

These warnings are not, by themselves, build failures.

A previous XAML startup crash involved `SystemColors.MenuHighlightTextBrushKey` not resolving inside a loaded theme resource. Subsequent theme work addressed that issue; current runtime status must be determined by actual Build/Run rather than assumed from history.

## 16. Recent milestone commits

The recent implementation history includes these important milestones (short hashes shown for navigation):

- `8a7864b` — Document Timeframe as official Toolbar Tool architecture milestone.
- `304c5b9` — Fix Grid toolbar click target resolution.
- `01e5db0` — Wire Grid toolbar toggle and preserve Grid state across theme changes.
- `5c0bb7a` — Implement Grid toggle state in ChartController.
- `6efa794` — Add Crosshair toolbar toggle behavior.
- `b9b602d` — Make Crosshair a toolbar toggle.
- `40b8cb2` — Make Crosshair and Grid opt-in toolbar features.
- `27da7af` — Fix chart context menu border and width.
- `4fed3f5` — Widen and clean chart context menu styling.
- `055a225` — Fix ScottPlot chart menu build errors.
- `9acc727` — Restore caption icons and style chart context menu.
- `a87fd3b` — Remove redundant Pan toolbar tool.
- `b3e9e5e` — Support custom toolbar boxes on both rows.
- `4049f3c` — Remove obsolete Select Object toolbar tool.
- `10d7a27` — Add centralized Toolbar tool request event.
- `eb6b9b8` — Implement toolbar overflow menu.
- `687d028` — Use ScottPlot standard four-direction chart pan.
- `76b661c` — Add Model menu with analysis/strategy placeholders.
- `58078d3` — Fix custom title bar hit testing and chart host theme surface.
- `d684f4e` — Fix ScottPlot color conversion compile error.
- `3a7e33c` — Fix theme startup/chart grid/axis labels.
- `4786de6` — Fix dark-theme grid contrast.
- `3ac0d59` — Fix dark-theme timeframe text contrast.
- `ef21726` — Navigation compile fix using UIElement PreviewMouseLeftButtonDown/Up event names.
- `ec85314` — Make empty custom boxes visibly render with minimum dimensions.
- `f71539f` — Restore compatibility methods after an incomplete change.

A dedicated Timeframe architecture documentation commit was most recently added as `8a7864b66dd4baa26f588638f01b3f99257464ea`.

## 17. Current reference file SHAs

At the documented baseline:

- `ForexPanel.App/MainWindow.xaml.cs` — `bba2a270cd059502ba03f19f707e81a247f9bb57`
- `ForexPanel.App/ChartController.cs` — `192b4e7f824d6f1c589c7cbdf7b857062b60c639`
- `ForexPanel.App/Toolbar/ToolbarManager.cs` — `d4c6b2a874d672e51693b94098c5e671a4e363de`
- `ForexPanel.App/Toolbar/ToolbarTool.cs` — `040bd8a23b5c610f49b6155f8300c874d0df8572`

These are baseline references; always compare with the current GitHub `main` before editing.

## 18. Recovery / safety points

A separate recovery point exists:

- Branch: `backup-before-grid-toggle-20260911`

Historical recovery documentation also exists for earlier Toolbar restoration and Navigation phases. Those records are references, not permission to roll back completed later work.

## 19. Explicitly closed areas

Do not reopen these without a proven regression:

- New Box implementation.
- Completed Toolbar layout/customization architecture.
- Completed Toolbar group/two-row behavior.
- Toolbar overflow architecture.
- Timeframe's status as a first-class Toolbar Tool.
- Confirmed working Zoom In, Zoom Out, Reset View, Timeframe, AutoScroll, and ChartShift.
- Existing Bridge/MT4 work unless a new failure appears.

## 20. Deferred / future work

- General View-menu ↔ Toolbar presence/visibility mechanism, including the currently incorrect `View → Grid` presence behavior.
- Unified Chart Interaction Layer and its event ownership model.
- Correct Pan sensitivity/coordinate conversion.
- Correct Crosshair mode integration without freezing/conflicting with existing ScottPlot behavior.
- Restore normal price-axis mouse zoom after Zoom Area.
- Verify Cursor mode.
- Audit and restore any missing Toolbar icons.
- Complete executable wiring of the remaining Toolbar tools.

## 21. Immediate next direction

The project is now at the point where the Toolbar framework and its recent architecture should be treated as established infrastructure.

The next development phase is **executable Toolbar tool wiring**, but it must begin with architecture inspection where interaction modes are involved:

1. Pull and inspect the current GitHub `main` state.
2. Preserve all confirmed behavior.
3. For navigation tools, map Toolbar request routing, MainWindow mouse/preview handlers, existing Crosshair ownership, ScottPlot interaction ownership, Pan coordinate conversion, Zoom Area capture/handled state, and Cursor behavior.
4. Establish the minimum independent Chart Interaction Layer.
5. Then wire remaining Toolbar actions through the existing `ToolbarTool`/request plumbing.
6. Keep Grid frozen and do not modify `ChartController.cs` during this Navigation phase.
7. After each logical GitHub change, pull locally, build, run, and test only the behavior relevant to that change.

## 22. Master status

**Infrastructure:** established.  
**Toolbar layout/customization:** implemented and closed.  
**Timeframe architecture:** established as first-class Toolbar Tool.  
**Grid Toolbar execution:** implemented and frozen.  
**Crosshair basic Toolbar toggle:** implemented; unified Navigation integration currently problematic.  
**Navigation architecture:** not yet complete.  
**Remaining executable Toolbar wiring:** next major implementation phase.  
**View-menu presence synchronization:** deliberately deferred.
