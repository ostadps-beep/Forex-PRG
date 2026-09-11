using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ForexPanel.App.Toolbar
{
    public sealed class ToolbarCustomizeWindow : Window
    {
        private readonly ToolbarManager _manager;
        private readonly string _groupId;
        private readonly ListBox _availableList;
        private readonly ListBox _boxList;

        public ToolbarCustomizeWindow(ToolbarManager manager, string groupId)
        {
            _manager = manager;
            _groupId = groupId;

            var group = _manager.Groups.First(g => g.Id == groupId);

            Title = $"Customize - {group.Title}";
            Width = 760;
            Height = 480;
            MinWidth = 650;
            MinHeight = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            ShowInTaskbar = false;
            SetResourceReference(BackgroundProperty, "Color.App.Background");
            SetResourceReference(ForegroundProperty, "Color.App.Foreground");

            var root = new Grid { Margin = new Thickness(14) };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var availableLabel = new TextBlock
            {
                Text = "Available Tools",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            };
            Grid.SetColumn(availableLabel, 0);
            Grid.SetRow(availableLabel, 0);
            root.Children.Add(availableLabel);

            var boxLabel = new TextBlock
            {
                Text = "Tools in this Box",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            };
            Grid.SetColumn(boxLabel, 2);
            Grid.SetRow(boxLabel, 0);
            root.Children.Add(boxLabel);

            _availableList = CreateListBox();
            _boxList = CreateListBox();

            Grid.SetColumn(_availableList, 0);
            Grid.SetRow(_availableList, 1);
            root.Children.Add(_availableList);

            Grid.SetColumn(_boxList, 2);
            Grid.SetRow(_boxList, 1);
            root.Children.Add(_boxList);

            var transferPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(transferPanel, 1);
            Grid.SetRow(transferPanel, 1);
            root.Children.Add(transferPanel);

            var addButton = CreateActionButton("Add →");
            addButton.Click += (_, _) => AddSelected();
            transferPanel.Children.Add(addButton);

            var removeButton = CreateActionButton("← Remove");
            removeButton.Click += (_, _) => RemoveSelected();
            transferPanel.Children.Add(removeButton);

            var reorderLabel = new TextBlock
            {
                Text = "Order",
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 4)
            };
            Grid.SetColumn(reorderLabel, 2);
            Grid.SetRow(reorderLabel, 2);
            root.Children.Add(reorderLabel);

            var reorderPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetColumn(reorderPanel, 2);
            Grid.SetRow(reorderPanel, 3);
            root.Children.Add(reorderPanel);

            var upButton = CreateSmallButton("Move Up");
            upButton.Click += (_, _) => MoveSelected(-1);
            reorderPanel.Children.Add(upButton);

            var downButton = CreateSmallButton("Move Down");
            downButton.Click += (_, _) => MoveSelected(1);
            reorderPanel.Children.Add(downButton);

            var bottomPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 0, 0)
            };
            Grid.SetColumn(bottomPanel, 0);
            Grid.SetColumnSpan(bottomPanel, 3);
            Grid.SetRow(bottomPanel, 4);
            root.Children.Add(bottomPanel);

            var okButton = CreateSmallButton("OK");
            okButton.MinWidth = 80;
            okButton.Click += (_, _) =>
            {
                DialogResult = true;
                Close();
            };
            bottomPanel.Children.Add(okButton);

            Content = root;
            RefreshLists();
        }

        private static ListBox CreateListBox()
        {
            var list = new ListBox
            {
                SelectionMode = SelectionMode.Single,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4)
            };

            list.SetResourceReference(Control.BackgroundProperty, "Color.Control.Background");
            list.SetResourceReference(Control.ForegroundProperty, "Color.Control.Foreground");
            list.SetResourceReference(Control.BorderBrushProperty, "Color.Control.Border");
            return list;
        }

        private static Button CreateActionButton(string text)
        {
            var button = new Button
            {
                Content = text,
                Width = 78,
                Margin = new Thickness(3),
                Padding = new Thickness(4)
            };

            button.SetResourceReference(Control.BackgroundProperty, "Color.Control.Background");
            button.SetResourceReference(Control.ForegroundProperty, "Color.Control.Foreground");
            button.SetResourceReference(Control.BorderBrushProperty, "Color.Control.Border");
            return button;
        }

        private static Button CreateSmallButton(string text)
        {
            var button = new Button
            {
                Content = text,
                MinWidth = 88,
                Margin = new Thickness(4, 0, 0, 0),
                Padding = new Thickness(8, 5, 8, 5)
            };

            button.SetResourceReference(Control.BackgroundProperty, "Color.Control.Background");
            button.SetResourceReference(Control.ForegroundProperty, "Color.Control.Foreground");
            button.SetResourceReference(Control.BorderBrushProperty, "Color.Control.Border");
            return button;
        }

        private void RefreshLists(string? selectedBoxToolId = null)
        {
            _availableList.Items.Clear();
            _boxList.Items.Clear();

            var group = _manager.Groups.First(g => g.Id == _groupId);

            foreach (var tool in _manager.GetAvailableTools(_groupId))
                _availableList.Items.Add(new ToolListItem(tool.Id, tool.Title));

            foreach (var tool in group.OrderedTools)
                _boxList.Items.Add(new ToolListItem(tool.Id, tool.Title));

            if (!string.IsNullOrWhiteSpace(selectedBoxToolId))
            {
                for (int i = 0; i < _boxList.Items.Count; i++)
                {
                    if (_boxList.Items[i] is ToolListItem item && item.Id == selectedBoxToolId)
                    {
                        _boxList.SelectedIndex = i;
                        _boxList.ScrollIntoView(item);
                        break;
                    }
                }
            }
        }

        private void AddSelected()
        {
            if (_availableList.SelectedItem is not ToolListItem item)
                return;

            if (_manager.AddToolToGroup(item.Id, _groupId))
                RefreshLists(item.Id);
        }

        private void RemoveSelected()
        {
            if (_boxList.SelectedItem is not ToolListItem item)
                return;

            if (_manager.RemoveToolFromGroup(item.Id, _groupId))
                RefreshLists();
        }

        private void MoveSelected(int direction)
        {
            if (_boxList.SelectedItem is not ToolListItem item)
                return;

            _manager.MoveTool(item.Id, _groupId, direction);
            RefreshLists(item.Id);
        }

        private sealed record ToolListItem(string Id, string Title)
        {
            public override string ToString() => Title;
        }
    }
}
