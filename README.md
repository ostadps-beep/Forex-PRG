# ForexPanel-A

ForexPanel-A is a WPF desktop application for the Forex chart panel.

## Structure

- `ForexPanel.App` — WPF application, main window, chart controller, theme and toolbar UI.
- `ForexPanel.Core` — shared candle model and MT4 pipe communication.
- `ForexPanel.slnx` — solution containing both projects.

## Main areas

- `ForexPanel.App/Theme` — application themes and theme management.
- `ForexPanel.App/Resources` — shared visual resources and colors.
- `ForexPanel.App/Toolbar` — active toolbar implementation.

## Important rule

`ForexPanel.App/ChartController.cs` is a protected file and must not be modified unless explicitly authorized.

## Build

From the repository root:

```powershell
dotnet build .\ForexPanel.slnx
```

Run the application with:

```powershell
dotnet run --project .\ForexPanel.App\ForexPanel.App.csproj
```
