# ForexPanel-A — Timeframe as a Toolbar Tool

Date: 2026-09-11
Repository: ostadps-beep/ForexPanel-A
Branch: main

## Architectural milestone

Timeframe was converted from an independent timeframe control into an official Toolbar Tool managed by the Toolbar framework.

This is an architectural capability, not merely a visual placement change.

## Implementation

- Tool ID: `Timeframe`
- Toolbar metadata: `IsTextOnly = true`
- Toolbar metadata: `IsToggle = false`
- Icon: none; the tool is intentionally text-only.
- Supported selections: `M1`, `M5`, `M15`, `M30`, `H1`, `H4`, `D1`.
- The Toolbar exposes the timeframe selection through the existing `TimeframeChanged` event.
- `MainWindow` receives the selected timeframe, maps it to minutes, updates `candleMinutes`, and calls `ChartController.Rebuild(currentSymbol, candleMinutes)`.

## Design significance

The Toolbar framework therefore supports more than icon buttons. A Toolbar Tool may be text-only and may have specialized execution/selection behavior while still being a first-class registered tool.

Timeframe is the reference implementation for this pattern.

## Responsibility boundary

- Toolbar: owns the Timeframe tool registration and selection UI.
- MainWindow: receives the selected timeframe and routes the change.
- ChartController: performs the chart rebuild using the selected timeframe.

This must not be confused with the View-menu presence/visibility mechanism. Toolbar execution/selection and View-menu presence are separate responsibilities.

## Preservation rule

Future Toolbar wiring work must preserve Timeframe as a completed architectural capability and must not convert it back into an independent control.
