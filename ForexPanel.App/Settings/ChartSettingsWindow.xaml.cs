using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ForexPanel.App.Settings;

/// <summary>
/// Chart Settings dialog: Sidebar (10 categories, per PS's spec) -> Header -> Content -> Footer
/// (Apply/OK/Cancel/Reset to Default). Only 5 categories (Chart, Axes, Candles, Grid &amp;
/// Background, Drawing Tools) have real, live-applying controls in this pass - the other 5
/// (Analytical Modules, HUD &amp; Overlay, Performance, Workspace, Advanced) are visible but
/// show a "not yet available" placeholder, reserved for this project's future roadmap and the
/// separate C++/Python engine merge. See docs/CHART_SETTINGS_PLAN.md.
///
/// Behavior: live-apply (PS's explicit choice) - every control change calls back into the
/// chart immediately via the supplied applyCallback. Apply/OK/Cancel/Reset to Default are still
/// provided per the spec's footer, with Cancel reverting to a snapshot taken when the window
/// opened.
/// </summary>
public sealed partial class ChartSettingsWindow : Window
{
    private readonly ChartSettings live;
    private readonly ChartSettings original;
    private readonly Action applyCallback;

    private readonly ListBox sidebar;
    private readonly TextBlock headerTitle;
    private readonly TextBlock headerDescription;
    private readonly ContentControl contentHost;

    private static readonly (SettingsCategory Category, string Title, string Description)[] Categories =
    {
        (SettingsCategory.Chart, "Chart", "General chart behavior: type, zoom, scroll, mouse wheel."),
        (SettingsCategory.Axes, "Axes", "Price and time axis appearance and behavior."),
        (SettingsCategory.Candles, "Candles", "Candle colors, body width, wick visibility."),
        (SettingsCategory.GridAndBackground, "Grid & Background", "Chart background and grid line appearance."),
        (SettingsCategory.DrawingTools, "Drawing Tools", "Default color and style for drawing tools."),
        (SettingsCategory.AnalyticalModules, "Analytical Modules", "Reserved for indicators/modules (not built yet)."),
        (SettingsCategory.HudAndOverlay, "HUD & Overlay", "Reserved for on-chart info overlays (not built yet)."),
        (SettingsCategory.Performance, "Performance", "Reserved for the future C++/Python engine integration."),
        (SettingsCategory.Workspace, "Workspace", "Reserved for save/load layout (not built yet)."),
        (SettingsCategory.Advanced, "Advanced", "Reserved for the future C++/Python bridge and engine settings.")
    };

    public ChartSettingsWindow(ChartSettings currentSettings, Action onApply)
    {
        InitializeComponent();

        live = currentSettings;
        original = currentSettings.Clone();
        applyCallback = onApply;

        var root = RootGrid;
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(body, 0);
        root.Children.Add(body);

        sidebar = new ListBox
        {
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4)
        };
        sidebar.SetResourceReference(Control.BackgroundProperty, "Color.Toolbar.Background");
        foreach (var category in Categories)
            sidebar.Items.Add(category.Title);
        sidebar.SelectionChanged += Sidebar_SelectionChanged;
        Grid.SetColumn(sidebar, 0);
        body.Children.Add(sidebar);

        var rightPanel = new DockPanel { Margin = new Thickness(16, 12, 16, 12) };
        Grid.SetColumn(rightPanel, 1);
        body.Children.Add(rightPanel);

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        DockPanel.SetDock(header, Dock.Top);
        headerTitle = new TextBlock { FontSize = 18, FontWeight = FontWeights.SemiBold };
        headerDescription = new TextBlock
        {
            Opacity = 0.7,
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        header.Children.Add(headerTitle);
        header.Children.Add(headerDescription);
        rightPanel.Children.Add(header);

        var scroller = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        contentHost = new ContentControl();
        scroller.Content = contentHost;
        rightPanel.Children.Add(scroller);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(16, 8, 16, 12)
        };
        Grid.SetRow(footer, 1);
        root.Children.Add(footer);

        footer.Children.Add(CreateFooterButton("Reset to Default", ResetToDefault_Click));
        footer.Children.Add(CreateFooterButton("Cancel", Cancel_Click));
        footer.Children.Add(CreateFooterButton("Apply", Apply_Click));
        footer.Children.Add(CreateFooterButton("OK", Ok_Click));

        sidebar.SelectedIndex = 0;
    }

    private static Button CreateFooterButton(string text, RoutedEventHandler handler)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(14, 6, 14, 6),
            Margin = new Thickness(6, 0, 0, 0),
            MinWidth = 90
        };
        button.Click += handler;
        return button;
    }

    private void Sidebar_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sidebar.SelectedIndex < 0)
            return;

        var category = Categories[sidebar.SelectedIndex];
        headerTitle.Text = category.Title;
        headerDescription.Text = category.Description;
        contentHost.Content = BuildPanel(category.Category);
    }

    private UIElement BuildPanel(SettingsCategory category) => category switch
    {
        SettingsCategory.Chart => BuildChartPanel(),
        SettingsCategory.Axes => BuildAxesPanel(),
        SettingsCategory.Candles => BuildCandlesPanel(),
        SettingsCategory.GridAndBackground => BuildGridPanel(),
        SettingsCategory.DrawingTools => BuildDrawingToolsPanel(),
        _ => BuildReservedPanel()
    };

    private UIElement BuildReservedPanel()
    {
        return new TextBlock
        {
            Text = "Not yet available in this project - reserved for a future update.",
            Opacity = 0.6,
            FontStyle = FontStyles.Italic,
            Margin = new Thickness(0, 24, 0, 0)
        };
    }

    // ---------- Chart ----------
    private UIElement BuildChartPanel()
    {
        var panel = new StackPanel();
        var s = live.General;

        panel.Children.Add(Row("Chart Type", ComboBoxFor(
            new[] { ChartTypeOption.Candlestick, ChartTypeOption.Line, ChartTypeOption.Histogram, ChartTypeOption.Combined },
            s.ChartType,
            v => v.ToString(),
            v => { s.ChartType = v; NotifyChanged(); },
            enabledValues: new[] { ChartTypeOption.Candlestick },
            disabledTooltip: "Only Candlestick rendering exists in this project so far.")));

        panel.Children.Add(Row("Visible Candles", NumericBox(s.VisibleCandles, v => { s.VisibleCandles = (int)v; NotifyChanged(); }, isEnabled: false,
            tooltip: "Reserved - the chart currently auto-sizes to the available window.")));

        panel.Children.Add(Row("Auto Fit", CheckBoxFor(s.AutoFit, v => { s.AutoFit = v; NotifyChanged(); })));

        panel.Children.Add(Row("Zoom Behavior", ComboBoxFor(
            new[] { ZoomAxisOption.TimeAxis, ZoomAxisOption.PriceAxis, ZoomAxisOption.Both },
            s.ZoomBehavior,
            v => v.ToString(),
            v => { s.ZoomBehavior = v; NotifyChanged(); })));

        panel.Children.Add(Row("Mouse Wheel", ComboBoxFor(
            new[] { MouseWheelOption.Pan, MouseWheelOption.Zoom },
            s.MouseWheelBehavior,
            v => v.ToString(),
            v => { s.MouseWheelBehavior = v; NotifyChanged(); },
            enabledValues: new[] { MouseWheelOption.Pan },
            disabledTooltip: "Zoom-on-wheel would conflict with this project's verified MT4-standard input (wheel=pan, +/-=zoom). Left as a placeholder for now.")));

        return panel;
    }

    // ---------- Axes ----------
    private UIElement BuildAxesPanel()
    {
        var panel = new StackPanel();
        var s = live.Axes;

        panel.Children.Add(SectionHeader("Price Axis"));
        panel.Children.Add(Row("Position", ComboBoxFor(
            new[] { AxisPositionOption.Left, AxisPositionOption.Right },
            s.PricePosition,
            v => v.ToString(),
            v => { s.PricePosition = v; NotifyChanged(); },
            enabledValues: new[] { AxisPositionOption.Right },
            disabledTooltip: "The current chart renderer always docks the price axis on the right.")));
        panel.Children.Add(Row("Show Last Price", CheckBoxFor(s.ShowLastPrice, v => { s.ShowLastPrice = v; NotifyChanged(); }, isEnabled: false,
            tooltip: "Reserved - no last-price marker exists on the axis yet.")));
        panel.Children.Add(Row("Show Horizontal Grid", CheckBoxFor(s.ShowHorizontalGrid, v => { s.ShowHorizontalGrid = v; NotifyChanged(); })));
        panel.Children.Add(Row("Decimal Places", NumericBox(s.DecimalPlaces, v => { s.DecimalPlaces = (int)v; NotifyChanged(); }, isEnabled: false,
            tooltip: "Reserved - price labels use ScottPlot's automatic formatting for now.")));
        panel.Children.Add(Row("Axis Color", ColorPickerFor(s.AxisColor, NotifyChanged)));

        panel.Children.Add(SectionHeader("Time Axis"));
        panel.Children.Add(Row("Position", ComboBoxFor(
            new[] { TimeAxisPositionOption.Bottom, TimeAxisPositionOption.Top },
            s.TimePosition,
            v => v.ToString(),
            v => { s.TimePosition = v; NotifyChanged(); },
            enabledValues: new[] { TimeAxisPositionOption.Bottom },
            disabledTooltip: "The current chart renderer always docks the time axis on the bottom.")));
        panel.Children.Add(Row("Show Vertical Grid", CheckBoxFor(s.ShowVerticalGrid, v => { s.ShowVerticalGrid = v; NotifyChanged(); })));
        panel.Children.Add(Row("Time Format", ComboBoxFor(
            new[] { TimeFormatOption.HhMm, TimeFormatOption.Date, TimeFormatOption.Combined },
            s.TimeFormat,
            v => v.ToString(),
            v => { s.TimeFormat = v; NotifyChanged(); },
            enabledValues: new[] { TimeFormatOption.HhMm },
            disabledTooltip: "Reserved - the time axis currently always shows ScottPlot's automatic date/time labels.")));

        return panel;
    }

    // ---------- Candles ----------
    private UIElement BuildCandlesPanel()
    {
        var panel = new StackPanel();
        var s = live.Candles;

        panel.Children.Add(Row("Bullish Color", ColorPickerFor(s.BullishColor, NotifyChanged)));
        panel.Children.Add(Row("Bearish Color", ColorPickerFor(s.BearishColor, NotifyChanged)));
        panel.Children.Add(Row("Body Thickness", SliderFor(0.1, 1.0, s.BodyThickness, v => { s.BodyThickness = v; NotifyChanged(); })));
        panel.Children.Add(Row("Show Wicks", CheckBoxFor(s.ShowWicks, v => { s.ShowWicks = v; NotifyChanged(); }, isEnabled: false,
            tooltip: "Reserved - the current rendering engine doesn't expose independent wick visibility yet.")));
        panel.Children.Add(Row("Show Body", CheckBoxFor(s.ShowBody, v => { s.ShowBody = v; NotifyChanged(); }, isEnabled: false,
            tooltip: "Reserved - line-only candle mode isn't implemented yet.")));

        return panel;
    }

    // ---------- Grid & Background ----------
    private UIElement BuildGridPanel()
    {
        var panel = new StackPanel();
        var s = live.GridAndBackground;

        panel.Children.Add(SectionHeader("Background"));
        panel.Children.Add(Row("Background Color", ColorPickerFor(s.BackgroundColor, NotifyChanged)));

        panel.Children.Add(SectionHeader("Grid"));
        panel.Children.Add(Row("Show Grid", CheckBoxFor(s.ShowGrid, v => { s.ShowGrid = v; NotifyChanged(); })));
        panel.Children.Add(Row("Grid Color", ColorPickerFor(s.GridColor, NotifyChanged)));
        panel.Children.Add(Row("Line Style", ComboBoxFor(
            new[] { LineStyleOption.Solid, LineStyleOption.Dash, LineStyleOption.Dot },
            s.LineStyle,
            v => v.ToString(),
            v => { s.LineStyle = v; NotifyChanged(); })));

        return panel;
    }

    // ---------- Drawing Tools ----------
    private UIElement BuildDrawingToolsPanel()
    {
        var panel = new StackPanel();
        var s = live.DrawingTools;

        panel.Children.Add(new TextBlock
        {
            Text = "Shared default style for all drawing tools (per-tool styling is planned for a later \"Drawing Tools phase 2\" pass).",
            Opacity = 0.6,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });
        panel.Children.Add(Row("Default Color", ColorPickerFor(s.DefaultColor, NotifyChanged)));
        panel.Children.Add(Row("Thickness", SliderFor(0.5, 4.0, s.Thickness, v => { s.Thickness = v; NotifyChanged(); })));
        panel.Children.Add(Row("Line Style", ComboBoxFor(
            new[] { LineStyleOption.Solid, LineStyleOption.Dash, LineStyleOption.Dot },
            s.LineStyle,
            v => v.ToString(),
            v => { s.LineStyle = v; NotifyChanged(); },
            enabledValues: new[] { LineStyleOption.Solid },
            disabledTooltip: "Reserved - drawing tools currently always render as solid lines.")));

        return panel;
    }

    // ---------- Shared small control builders ----------
    private static UIElement SectionHeader(string text) => new TextBlock
    {
        Text = text,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 12, 0, 6),
        Opacity = 0.85
    };

    private static UIElement Row(string label, UIElement control)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelBlock = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(labelBlock, 0);
        Grid.SetColumn(control, 1);

        grid.Children.Add(labelBlock);
        grid.Children.Add(control);
        return grid;
    }

    private static CheckBox CheckBoxFor(bool initial, Action<bool> onChanged, bool isEnabled = true, string? tooltip = null)
    {
        var box = new CheckBox { IsChecked = initial, IsEnabled = isEnabled, VerticalAlignment = VerticalAlignment.Center };
        if (tooltip != null)
            box.ToolTip = tooltip;
        box.Checked += (_, _) => onChanged(true);
        box.Unchecked += (_, _) => onChanged(false);
        return box;
    }

    private static ComboBox ComboBoxFor<T>(
        IReadOnlyList<T> values,
        T initial,
        Func<T, string> label,
        Action<T> onChanged,
        IReadOnlyList<T>? enabledValues = null,
        string? disabledTooltip = null) where T : notnull
    {
        var combo = new ComboBox { MinWidth = 160, HorizontalAlignment = HorizontalAlignment.Left };
        int selectedIndex = 0;

        for (int i = 0; i < values.Count; i++)
        {
            var value = values[i];
            bool isEnabled = enabledValues == null || Contains(enabledValues, value);
            var item = new ComboBoxItem { Content = label(value), IsEnabled = isEnabled };
            if (!isEnabled && disabledTooltip != null)
                item.ToolTip = disabledTooltip;
            combo.Items.Add(item);

            if (EqualityComparer<T>.Default.Equals(value, initial))
                selectedIndex = i;
        }

        combo.SelectedIndex = selectedIndex;
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex >= 0)
                onChanged(values[combo.SelectedIndex]);
        };
        return combo;
    }

    private static bool Contains<T>(IReadOnlyList<T> values, T target)
    {
        foreach (var v in values)
        {
            if (EqualityComparer<T>.Default.Equals(v, target))
                return true;
        }
        return false;
    }

    private static UIElement SliderFor(double min, double max, double initial, Action<double> onChanged)
    {
        var panel = new DockPanel();
        var valueLabel = new TextBlock { Width = 40, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(valueLabel, Dock.Right);

        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            Value = initial,
            Width = 180,
            VerticalAlignment = VerticalAlignment.Center
        };

        valueLabel.Text = initial.ToString("0.0", CultureInfo.InvariantCulture);
        slider.ValueChanged += (_, e) =>
        {
            valueLabel.Text = e.NewValue.ToString("0.0", CultureInfo.InvariantCulture);
            onChanged(e.NewValue);
        };

        panel.Children.Add(valueLabel);
        panel.Children.Add(slider);
        return panel;
    }

    private static UIElement NumericBox(double initial, Action<double> onChanged, bool isEnabled = true, string? tooltip = null)
    {
        var box = new TextBox
        {
            Text = initial.ToString(CultureInfo.InvariantCulture),
            Width = 80,
            IsEnabled = isEnabled
        };
        if (tooltip != null)
            box.ToolTip = tooltip;

        box.LostFocus += (_, _) =>
        {
            if (double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                onChanged(value);
            else
                box.Text = initial.ToString(CultureInfo.InvariantCulture);
        };
        return box;
    }

    /// <summary>
    /// Lightweight color picker: a swatch button that opens a small popup with a hex input
    /// and a shade-percentage slider (see ColorSetting). This is a simpler first pass than the
    /// sibling ForexAnalysis project's full HSV-square ColorPickerButton - porting that fuller
    /// control here is a reasonable later upgrade once this pass is verified.
    /// </summary>
    private static UIElement ColorPickerFor(ColorSetting setting, Action onChanged)
    {
        var swatch = new Border
        {
            Width = 28,
            Height = 20,
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Gray,
            Background = new SolidColorBrush(setting.Effective)
        };

        var button = new Button
        {
            Content = swatch,
            Padding = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var popup = new Popup
        {
            PlacementTarget = button,
            Placement = PlacementMode.Bottom,
            StaysOpen = false
        };

        var popupPanel = new StackPanel { Width = 220 };
        popupPanel.SetResourceReference(Panel.BackgroundProperty, "Color.Toolbar.GroupBackground");

        var border = new Border
        {
            Child = popupPanel,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10)
        };
        border.SetResourceReference(Border.BackgroundProperty, "Color.Toolbar.GroupBackground");
        border.SetResourceReference(Border.BorderBrushProperty, "Color.Toolbar.Border");
        popup.Child = border;

        var hexLabel = new TextBlock { Text = "Hex color", Margin = new Thickness(0, 0, 0, 4) };
        hexLabel.SetResourceReference(TextBlock.ForegroundProperty, "Color.App.Foreground");

        var hexBox = new TextBox { Text = ColorToHex(setting.BaseColor), Margin = new Thickness(0, 0, 0, 8) };
        var shadeLabel = new TextBlock { Text = $"Shade: {setting.ShadePercent:0}%", Margin = new Thickness(0, 0, 0, 4) };
        shadeLabel.SetResourceReference(TextBlock.ForegroundProperty, "Color.App.Foreground");
        var shadeSlider = new Slider { Minimum = 0, Maximum = 200, Value = setting.ShadePercent };

        void Refresh()
        {
            swatch.Background = new SolidColorBrush(setting.Effective);
            onChanged();
        }

        hexBox.LostFocus += (_, _) =>
        {
            if (TryParseHex(hexBox.Text, out var color))
            {
                setting.BaseColor = color;
                Refresh();
            }
            else
            {
                hexBox.Text = ColorToHex(setting.BaseColor);
            }
        };

        shadeSlider.ValueChanged += (_, e) =>
        {
            setting.ShadePercent = e.NewValue;
            shadeLabel.Text = $"Shade: {e.NewValue:0}%";
            Refresh();
        };

        popupPanel.Children.Add(hexLabel);
        popupPanel.Children.Add(hexBox);
        popupPanel.Children.Add(shadeLabel);
        popupPanel.Children.Add(shadeSlider);

        button.Click += (_, _) => popup.IsOpen = !popup.IsOpen;

        var container = new Grid();
        container.Children.Add(button);
        container.Children.Add(popup);
        return container;
    }

    private static string ColorToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private static bool TryParseHex(string text, out Color color)
    {
        color = Colors.Transparent;
        text = text.Trim().TrimStart('#');
        if (text.Length != 6 || !int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value))
            return false;

        color = Color.FromRgb((byte)((value >> 16) & 0xFF), (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF));
        return true;
    }

    // ---------- Footer behavior ----------
    private void NotifyChanged() => applyCallback();

    private void Apply_Click(object sender, RoutedEventArgs e) => applyCallback();

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        applyCallback();
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        live.CopyFrom(original);
        applyCallback();
        Close();
    }

    private void ResetToDefault_Click(object sender, RoutedEventArgs e)
    {
        live.CopyFrom(ChartSettings.CreateDefault());
        applyCallback();

        // Refresh the currently visible panel so on-screen controls reflect the new values.
        int selected = sidebar.SelectedIndex;
        sidebar.SelectedIndex = -1;
        sidebar.SelectedIndex = selected;
    }
}
