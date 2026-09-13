# Chart Settings — Architecture Plan (Draft for review, NOT yet implemented)

## Scope decision (PS, 2026-09-13)
This is foundational infrastructure, not a quick feature. The system is ~90% an analysis
platform, not a trading terminal, and the future "Model" menu (CSV Analysis / Strategy Tester /
Strategy Builder) will sit on top of this same visual foundation and matters more than the
chart itself long-term. Settings must therefore be:
- Professional-grade and granular, covering EVERY visual component of the system, not just
  the chart canvas (toolbar, grid, candles, crosshair, drawing tools, chrome).
- Color settings built on soft/muted base colors with an adjustable percentage/shade control
  per element (not raw hex-only pickers).
- Multiple style options and detail-level sub-options per element (not just on/off toggles).
- Researched against professional platforms (TradingView, etc.) for which capabilities and
  levels of detail are worth having — not to copy their UI, but to know what "professional"
  actually covers so nothing important is missing.
- Live Apply: every change previews immediately on the chart (PS's explicit choice, not
  Apply/OK-only like the sibling ForexAnalysis project's Properties dialog).

## Reference points
- **ForexAnalysis (sibling project) ColorPickerButton**: proven, already-built component -
  HSV saturation/value square + hue slider + alpha slider + hex input + muted preset swatches
  + live preview + OK/Cancel-per-picker. Reusable pattern here instead of a plain WPF color
  dialog. Recommend porting the same interaction model (not the same assembly - separate repo).
- **TradingView's chart settings categories** (for reference on granularity, not UI copying):
  Symbol/Appearance splits candle body, borders, and wicks into THREE independently
  toggleable+colorable parts (not just one up/down pair); Scales tab separates price-scale
  behavior (auto/log/percentage/indexed-to-100) from time-scale formatting; Canvas/background
  splits grid line color from grid opacity from background color as separate controls;
  Crosshair has its own line style + a "magnet" snapping mode setting.

## Proposed settings categories (draft - open to changes)
1. **General** - chart type (Candlestick/Bar/Line/Area), symbol precision/digits
2. **Candles** - body up/down colors (with shade %), border on/off + color, wick on/off +
   color (up/down wicks independently, per the TradingView reference above), body width ratio
3. **Grid** - visibility, line style (solid/dashed/dotted), color + shade %, opacity/density
4. **Scale (Price & Time)** - Chart Shift percentage, bar-spacing min/max, price-scale mode
   (auto / fixed / 1:1), time-axis label format
5. **Crosshair** - line style, color + shade %, snap/magnet behavior
6. **Chrome / Toolbar** - background, hover/pressed shades, border colors (currently theme-only;
   this would let PS fine-tune beyond just Light/Dark/System)
7. (Future, once drawing tools are built out further) **Drawing Tools** - default color/style
   per tool type, matching the already-planned "per-object drawing colors" work

## Proposed architecture (not yet built)
- A central `ChartSettings` model (mirrors the sibling project's `ChartColorSettings` /
  `EnumerateSlots()` definition-list pattern) - one class per category, each color stored as a
  base hue/muted-color anchor plus a separate adjustable shade/opacity percentage, so "soft,
  professional, consistent" colors stay easy to retune without picking raw hex every time.
- A `ChartSettingsWindow` (modal, tabbed - one tab per category above) built the same way the
  Toolbar/Chrome work was done: build the full tab/category shell first (even inert), then wire
  each category to real behavior one at a time - matching PS's established methodology for this
  project.
- Every control change calls into `ChartController`/`MainWindow` immediately (live Apply, per
  PS's choice) rather than batching until an OK button.
- Persistence: extend the same `.fxtpl`-style JSON template concept the sibling project uses,
  so a full settings profile can be saved/loaded/reset, not just the current in-memory state.

## Suggested phasing (proposal, needs PS's sign-off)
1. Build the `ChartSettings` model classes only (no UI yet) - defines every category/field this
   plan lists, with sensible defaults. Verify structure with PS before any UI work.
2. Build the `ChartSettingsWindow` shell with all tabs present but mostly inert (matches PS's
   "build the full scope visible first" methodology used for the Toolbar/Chrome work).
3. Wire one category at a time to real live-Apply behavior, starting with whichever PS picks
   first (Grid was the immediate trigger for this whole effort).

## DECISION (PS, 2026-09-13) - superseded plan above with final scope
PS provided two detailed spec documents (docs/CHART_SETTINGS_FULL_SPEC.txt structure below)
defining a 10-category professional settings window: Chart, Axes, Candles, Grid & Background,
Drawing Tools, Analytical Modules, HUD & Overlay, Performance, Workspace, Advanced - plus a
window layout (Sidebar categories -> Header -> grouped Content -> Footer with
Apply/OK/Cancel/Reset to Default).

Important context PS clarified: a separate C++/Python calculation engine already exists,
built in a different chat/session (not in this memory) - this ForexPanel-A/Forex-PRG project
is the chart/UI layer that will eventually be merged with that engine once this chart's own
buildout is done. That is WHY categories like "Analytical Modules", "Performance"
(GPU/multi-threading), and "Advanced" (Python/C Bridge Settings) are in the spec even though
nothing in THIS repo implements them yet - they are reserved integration points, not fantasy.

FINAL SCOPE for this pass: show all 10 sidebar categories now (full structure visible per
PS's established "build full scope first" methodology), but only wire 5 to real, live-Apply
behavior because only these have real functionality behind them in this repo today:
- Chart, Axes, Candles, Grid & Background, Drawing Tools
The other 5 (Analytical Modules, HUD & Overlay, Performance, Workspace, Advanced) are visible
in the sidebar but show an inert/"not yet available" placeholder panel - clickable, but no
functioning controls - reserved for when the C++/Python engine is merged in, or when this
project's own roadmap reaches those features (indicators, HUD, etc.), in a later session.

Known behavioral conflict flagged to PS: the spec's "Mouse Wheel: Zoom/Scroll" option would
let the user override the already-verified MT4-standard convention (wheel=pan, +/-=zoom) -
kept in the spec as an option, not defaulted to something that breaks the existing standard.

## Open questions for PS
- Confirm or adjust the 6-7 categories above (add/remove/rename).
- For the color "shade/percentage" control specifically: a single lightness-style percentage
  slider per color (simplest), or the fuller HSV square + hue + alpha combo like the sibling
  project's ColorPickerButton (more powerful, more UI to build)?
- Should Chrome/Toolbar colors live in this same window, or stay purely theme-driven
  (Light/Dark/System) and be excluded from per-element overrides for now?
