using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ForexPanel.App;

public sealed class StyledChartMenu : ScottPlot.IPlotMenu
{
    private readonly ScottPlot.WPF.WpfPlotBase chart;
    private readonly ScottPlot.WPF.WpfPlotMenu defaults;

    public List<ScottPlot.ContextMenuItem> ContextMenuItems { get; } = new();

    public StyledChartMenu(ScottPlot.WPF.WpfPlotBase chart)
    {
        this.chart = chart;
        defaults = new ScottPlot.WPF.WpfPlotMenu(chart);
        Reset();
    }

    public void Reset()
    {
        Clear();
        ContextMenuItems.AddRange(defaults.GetDefaultContextMenuItems());
    }

    public void Clear() => ContextMenuItems.Clear();

    public void Add(string Label, Action<ScottPlot.Plot> action)
    {
        ContextMenuItems.Add(new ScottPlot.ContextMenuItem { Label = Label, OnInvoke = action });
    }

    public void AddSeparator()
    {
        ContextMenuItems.Add(new ScottPlot.ContextMenuItem { IsSeparator = true });
    }

    public void ShowContextMenu(ScottPlot.Pixel pixel)
    {
        var plot = chart.Plot;
        if (plot is null || ContextMenuItems.Count == 0)
            return;

        var menuBackground = GetBrush("Menu.Static.Background", Brushes.White);

        var menu = new ContextMenu
        {
            Width = 250,
            MinWidth = 250,
            MaxWidth = 250,
            PlacementTarget = chart,
            Placement = PlacementMode.MousePoint,
            Background = menuBackground,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0, 0, 0, 0),
            Padding = new Thickness(0, 0, 0, 0),
            HasDropShadow = false,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
            Template = CreateMenuTemplate(menuBackground)
        };

        foreach (var item in ContextMenuItems)
        {
            if (item.IsSeparator)
            {
                menu.Items.Add(new Separator
                {
                    Height = 1,
                    Margin = new Thickness(4, 3, 4, 3),
                    Background = GetBrush("Menu.Static.Border", Brushes.Gray),
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(0, 0, 0, 0)
                });
                continue;
            }

            var menuItem = new MenuItem
            {
                Header = item.Label,
                Foreground = GetBrush("Color.App.Foreground", Brushes.Black),
                Background = menuBackground,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0, 0, 0, 0),
                Padding = new Thickness(10, 6, 10, 6),
                MinHeight = 30
            };

            menuItem.Click += (_, _) => item.OnInvoke(plot);
            menu.Items.Add(menuItem);
        }

        menu.IsOpen = true;
    }

    private static ControlTemplate CreateMenuTemplate(Brush background)
    {
        var template = new ControlTemplate(typeof(ContextMenu));

        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, background);
        border.SetValue(Border.BorderBrushProperty, Brushes.Transparent);
        border.SetValue(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 0));
        border.SetValue(Border.PaddingProperty, new Thickness(0, 0, 0, 0));

        var itemsPresenter = new FrameworkElementFactory(typeof(ItemsPresenter));
        border.AppendChild(itemsPresenter);
        template.VisualTree = border;

        return template;
    }

    private Brush GetBrush(string key, Brush fallback)
    {
        return chart.TryFindResource(key) as Brush
            ?? Application.Current?.TryFindResource(key) as Brush
            ?? fallback;
    }
}
