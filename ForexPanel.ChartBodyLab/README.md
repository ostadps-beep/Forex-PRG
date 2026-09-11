# ForexPanel Chart Body Lab

This is an isolated diagnostic project for the ForexPanel-A chart body.

## Purpose

Compare the current ForexPanel chart interaction with a clean ScottPlot 5 implementation without touching `ForexPanel.App/ChartController.cs`.

## Design

- Candlestick data only.
- Right price axis.
- Bottom DateTime axis.
- Grid enabled.
- No ForexPanel toolbar, menu, theme manager, symbol selector, status bar, or MT4 bridge.
- No custom mouse event handlers.
- ScottPlot 5 built-in interaction system is intentionally left in control.

## Reference basis

ScottPlot 5 officially supports candlestick charts, DateTime bottom axes, right-side price axes, and standard mouse interaction responses.
TradingView Lightweight Charts documentation is used as the behavioral reference for separating chart navigation/scale concepts and for the notion of a visible time range.

## Run

From the ForexPanel-A repository root:

```powershell
dotnet run --project .\ForexPanel.ChartBodyLab\ForexPanel.ChartBodyLab.csproj
```

## Diagnostic interpretation

If this isolated chart behaves correctly while ForexPanel-A's main chart does not, the problem is in ForexPanel's custom chart interaction/range logic rather than the basic ScottPlot candlestick/chart-body rendering.

If this isolated chart also shows the same defect, investigate ScottPlot/WPF interaction configuration or the rendering environment before changing ForexPanel's controller.

`ChartController.cs` is not modified by this diagnostic project.
