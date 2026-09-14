using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ForexPanel.App.Toolbar
{
    public partial class ToolbarControl : UserControl
    {
        private const string GroupDragFormat = "ForexPanel.ToolbarGroup";
        private const double ToolbarRowHeight = 35;
        private static readonly string[] TimeframeOptions = { "M1", "M5", "M15", "M30", "H1", "H4", "D1" };
        private readonly ToolbarManager _manager;
        private readonly ComboBox _symbolCombo;
        private readonly List<(ToolbarGroup Group, FrameworkElement Element)> _groupElements = new();
        private Point _dragStartPoint;
        private bool _dragCandidate;
        private bool _suppressNextToolClick;
        private bool _isToolbarCollapsed;
        private bool _updatingOverflow;
        private string _selectedTimeframe = "M15";

        public event EventHandler<string>? SymbolChanged;
        public event EventHandler<string>? TimeframeChanged;
        public bool IsCollapsed => _isToolbarCollapsed;

        public ToolbarControl()
        {
            InitializeComponent();
            _manager = new ToolbarManager();
            _symbolCombo = CreateSymbolCombo();
            ContextMenu = CreateToolbarContextMenu(0);
            TopGroupsScroll.ContextMenu = CreateToolbarContextMenu(0);
            BottomGroupsScroll.ContextMenu = CreateToolbarContextMenu(1);
            TopGroupsPanel.ContextMenu = CreateToolbarContextMenu(0);
            BottomGroupsPanel.ContextMenu = CreateToolbarContextMenu(1);
            SizeChanged += (_, _) => { if (!IsLoaded || _updatingOverflow) return; Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(UpdateOverflow)); };
            Loaded += (_, _) => UpdateOverflow();
            Build();
        }

        public void Build()
        {
            TopGroupsPanel.Children.Clear(); BottomGroupsPanel.Children.Clear(); _groupElements.Clear();
            TopOverflowButton.Visibility = Visibility.Collapsed; BottomOverflowButton.Visibility = Visibility.Collapsed;
            var groups = _manager.Groups.OrderBy(g => g.Row).ThenBy(g => g.Order).ThenBy(g => g.Priority).ToList();
            foreach (var group in groups)
            {
                var element = CreateGroup(group);
                if (group.Row == 0) TopGroupsPanel.Children.Add(element); else BottomGroupsPanel.Children.Add(element);
                _groupElements.Add((group, element));
            }
            UpdateRowHeights(groups);
            if (IsLoaded) Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateOverflow));
        }

        private void UpdateRowHeights(IEnumerable<ToolbarGroup> groups)
        {
            var materialized = groups.ToList();
            var hasTopGroups = materialized.Any(g => g.Row == 0);
            var hasBottomGroups = materialized.Any(g => g.Row == 1);
            if (_isToolbarCollapsed) { TopRowDefinition.Height = new GridLength(0); BottomRowDefinition.Height = new GridLength(0); return; }
            TopRowDefinition.Height = hasTopGroups || !hasBottomGroups ? new GridLength(ToolbarRowHeight) : new GridLength(0);
            BottomRowDefinition.Height = hasBottomGroups ? new GridLength(ToolbarRowHeight) : new GridLength(0);
        }

        private void EnsureBothRowsVisibleForDrag()
        {
            if (_isToolbarCollapsed) return;
            if (TopRowDefinition.Height.Value == ToolbarRowHeight && BottomRowDefinition.Height.Value == ToolbarRowHeight) return;
            TopRowDefinition.Height = new GridLength(ToolbarRowHeight); BottomRowDefinition.Height = new GridLength(ToolbarRowHeight);
        }

        private ComboBox CreateSymbolCombo()
        {
            var combo = new ComboBox { Width = 86, Height = 30, Margin = new Thickness(5, 0, 3, 0), VerticalAlignment = VerticalAlignment.Center };
            combo.SetResourceReference(Control.BackgroundProperty, "Color.Control.Background"); combo.SetResourceReference(Control.ForegroundProperty, "Color.Control.Foreground"); combo.SetResourceReference(Control.BorderBrushProperty, "Color.Control.Border");
            combo.Items.Add("EURUSD"); combo.Items.Add("GBPUSD"); combo.Items.Add("USDJPY"); combo.SelectedIndex = 0;
            combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is string symbol) SymbolChanged?.Invoke(this, symbol); };
            return combo;
        }

        private Border CreateGroup(ToolbarGroup group)
        {
            var border = new Border
            {
                Margin = new Thickness(0.5), Padding = new Thickness(3, 0, 3, 0), CornerRadius = new CornerRadius(3), Tag = group.Id,
                AllowDrop = true, Height = ToolbarRowHeight - 2, MinHeight = ToolbarRowHeight - 2, ContextMenu = CreateGroupContextMenu(group)
            };
            border.SetResourceReference(Control.BackgroundProperty, "Color.Toolbar.GroupBackground"); border.SetResourceReference(Control.BorderBrushProperty, "Color.Toolbar.GroupBorder"); border.BorderThickness = new Thickness(1);
            border.PreviewMouseLeftButtonDown += Group_PreviewMouseLeftButtonDown; border.PreviewMouseMove += Group_PreviewMouseMove; border.Drop += Group_Drop; border.DragOver += Group_DragOver;
            var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            foreach (var tool in group.OrderedTools)
            {
                FrameworkElement element = tool.Id == "Timeframe" ? CreateTimeframeCombo() : CreateButton(tool);
                element.Tag = tool; panel.Children.Add(element);
            }
            border.Child = panel; return border;
        }

        private ComboBox CreateTimeframeCombo()
        {
            var combo = new ComboBox { Width = 68, Height = 30, Margin = new Thickness(3, 0, 3, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = "Timeframe" };
            combo.SetResourceReference(Control.BackgroundProperty, "Color.Control.Background"); combo.SetResourceReference(Control.ForegroundProperty, "Color.Control.Foreground"); combo.SetResourceReference(Control.BorderBrushProperty, "Color.Control.Border");
            foreach (var timeframe in TimeframeOptions) combo.Items.Add(timeframe);
            var selectedIndex = Array.IndexOf(TimeframeOptions, _selectedTimeframe); if (selectedIndex < 0) selectedIndex = 0; combo.SelectedIndex = selectedIndex;
            combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is string timeframe && timeframe != _selectedTimeframe) { _selectedTimeframe = timeframe; TimeframeChanged?.Invoke(this, timeframe); } };
            return combo;
        }

        private ContextMenu CreateGroupContextMenu(ToolbarGroup group)
        {
            var contextMenu = CreateStyledContextMenu();
            var customize = new MenuItem { Header = "Customize" }; customize.Click += (_, _) => OpenCustomize(group.Id); contextMenu.Items.Add(customize);
            contextMenu.Items.Add(new Separator());
            var newBox = new MenuItem { Header = "New Box" }; newBox.Click += (_, _) => { var newGroup = _manager.CreateGroup(null, group.Row); Build(); OpenCustomize(newGroup.Id); }; contextMenu.Items.Add(newBox);
            var deleteBox = new MenuItem { Header = "Delete Box" };
            deleteBox.Click += (_, _) => { var result = MessageBox.Show($"Delete the box '{group.Title}'? Its tools will become available again.", "Delete Toolbar Box", MessageBoxButton.YesNo, MessageBoxImage.Question); if (result == MessageBoxResult.Yes && _manager.DeleteGroup(group.Id)) Build(); };
            contextMenu.Items.Add(deleteBox); return contextMenu;
        }

        private ContextMenu CreateToolbarContextMenu(int row)
        {
            var contextMenu = CreateStyledContextMenu();
            var newBox = new MenuItem { Header = "New Box" };
            newBox.Click += (_, _) => { var newGroup = _manager.CreateGroup(null, row); Build(); OpenCustomize(newGroup.Id); };
            contextMenu.Items.Add(newBox); return contextMenu;
        }

        private ContextMenu CreateStyledContextMenu()
        {
            var contextMenu = new ContextMenu();
            contextMenu.SetResourceReference(Control.BackgroundProperty, "Color.Toolbar.GroupBackground");
            contextMenu.SetResourceReference(Control.BorderBrushProperty, "Color.Toolbar.GroupBorder");
            contextMenu.BorderThickness = new Thickness(1); contextMenu.Padding = new Thickness(2);
            return contextMenu;
        }

        private void OpenCustomize(string groupId)
        {
            var ownerWindow = Window.GetWindow(this); var dialog = new ToolbarCustomizeWindow(_manager, groupId) { Owner = ownerWindow }; if (dialog.ShowDialog() == true) Build();
        }

        private ButtonBase CreateButton(ToolbarTool tool)
        {
            var button = new Button();
            button.Click += (_, _) => { if (_suppressNextToolClick) { _suppressNextToolClick = false; return; } tool.Run(); };
            button.ToolTip = tool.ToolTip; button.SetResourceReference(FrameworkElement.StyleProperty, "ToolbarButtonStyle");
            if (tool.IsTextOnly)
            {
                var text = new TextBlock { Text = tool.Text, FontWeight = FontWeights.SemiBold, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                text.SetResourceReference(TextBlock.ForegroundProperty, "Color.Toolbar.Icon"); button.Content = text; button.Width = tool.Id == "MN1" ? 42 : tool.Id.Length <= 2 ? 32 : 38; button.Padding = new Thickness(2, 0, 2, 0);
            }
            else
            {
                var path = new System.Windows.Shapes.Path(); path.SetResourceReference(System.Windows.Shapes.Path.DataProperty, tool.IconKey); path.SetResourceReference(FrameworkElement.StyleProperty, "ToolbarIconStyle");
                if (tool.IconKey is "Icon.ChartCandlestick" or "Icon.ChartArea")
                    path.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "Color.Toolbar.Icon");
                button.Content = path;
            }
            return button;
        }

        private void UpdateOverflow()
        {
            if (!IsLoaded || _updatingOverflow || _isToolbarCollapsed) return; _updatingOverflow = true;
            try { UpdateOverflowForRow(TopGroupsPanel, TopOverflowButton); UpdateOverflowForRow(BottomGroupsPanel, BottomOverflowButton); }
            finally { _updatingOverflow = false; }
        }

        private static List<(ToolbarGroup Group, ToolbarTool Tool, FrameworkElement Element)> GetToolElements(StackPanel panel)
        {
            var result = new List<(ToolbarGroup, ToolbarTool, FrameworkElement)>();
            foreach (var child in panel.Children.OfType<Border>())
            {
                if (child.Child is not StackPanel toolPanel) continue;
                var group = child.Tag is string groupId ? new ToolbarGroup { Id = groupId, Title = groupId } : null; if (group == null) continue;
                foreach (var element in toolPanel.Children.OfType<FrameworkElement>()) if (element.Tag is ToolbarTool tool) result.Add((group, tool, element));
            }
            return result;
        }

        private void UpdateOverflowForRow(StackPanel panel, Button overflowButton)
        {
            var tools = GetToolElements(panel); if (tools.Count == 0) { overflowButton.Visibility = Visibility.Collapsed; return; }
            foreach (var item in tools) item.Element.Visibility = Visibility.Visible;
            overflowButton.Visibility = Visibility.Collapsed; panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); if (panel.DesiredSize.Width <= panel.ActualWidth + 0.5) return;
            overflowButton.Visibility = Visibility.Visible; UpdateLayout(); panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var candidates = tools.Where(t => t.Tool.CanOverflow).Reverse().ToList();
            foreach (var candidate in candidates) { if (panel.DesiredSize.Width <= panel.ActualWidth + 0.5) break; candidate.Element.Visibility = Visibility.Collapsed; panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); }
            if (tools.All(t => t.Element.Visibility == Visibility.Visible)) overflowButton.Visibility = Visibility.Collapsed;
        }

        private void OverflowButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;
            var panel = ReferenceEquals(button, TopOverflowButton) ? TopGroupsPanel : BottomGroupsPanel; var menu = CreateStyledContextMenu();
            var hiddenTools = GetToolElements(panel).Where(t => t.Element.Visibility == Visibility.Collapsed && t.Tool.CanOverflow).ToList(); string? lastGroupId = null;
            foreach (var item in hiddenTools)
            {
                if (lastGroupId != item.Group.Id) { if (menu.Items.Count > 0) menu.Items.Add(new Separator()); menu.Items.Add(new MenuItem { Header = item.Group.Title, IsEnabled = false }); lastGroupId = item.Group.Id; }
                if (item.Tool.Id == "Timeframe")
                {
                    var timeframeMenu = new MenuItem { Header = item.Tool.Title };
                    foreach (var timeframe in TimeframeOptions) { var option = new MenuItem { Header = timeframe, IsChecked = timeframe == _selectedTimeframe }; option.Click += (_, _) => { _selectedTimeframe = timeframe; TimeframeChanged?.Invoke(this, timeframe); }; timeframeMenu.Items.Add(option); }
                    menu.Items.Add(timeframeMenu);
                }
                else { var tool = item.Tool; var toolMenu = new MenuItem { Header = tool.Title }; toolMenu.Click += (_, _) => tool.Run(); menu.Items.Add(toolMenu); }
            }
            if (menu.Items.Count == 0) return; menu.IsOpen = true; menu.PlacementTarget = button; menu.Placement = PlacementMode.Bottom;
        }

        public void ToggleCollapse()
        {
            _isToolbarCollapsed = !_isToolbarCollapsed; UpdateRowHeights(_manager.Groups); if (!_isToolbarCollapsed) Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateOverflow));
        }

        private void Group_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border border || border.Tag is not string) return; _suppressNextToolClick = false; _dragStartPoint = e.GetPosition(this); _dragCandidate = true;
        }

        private void Group_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragCandidate || e.LeftButton != MouseButtonState.Pressed) return;
            var current = e.GetPosition(this); var horizontal = Math.Abs(current.X - _dragStartPoint.X); var vertical = Math.Abs(current.Y - _dragStartPoint.Y);
            if (horizontal < SystemParameters.MinimumHorizontalDragDistance && vertical < SystemParameters.MinimumVerticalDragDistance) return;
            if (sender is not Border border || border.Tag is not string groupId) return;
            _dragCandidate = false; _suppressNextToolClick = true; var data = new DataObject(GroupDragFormat, groupId); DragDrop.DoDragDrop(border, data, DragDropEffects.Move);
        }

        private void Group_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(GroupDragFormat)) { EnsureBothRowsVisibleForDrag(); e.Effects = DragDropEffects.Move; } else e.Effects = DragDropEffects.None; e.Handled = true;
        }

        private void Group_Drop(object sender, DragEventArgs e)
        {
            _dragCandidate = false; if (!e.Data.GetDataPresent(GroupDragFormat)) return; var sourceId = e.Data.GetData(GroupDragFormat) as string; if (string.IsNullOrWhiteSpace(sourceId)) return;
            var targetId = (sender as Border)?.Tag as string; var target = _manager.Groups.FirstOrDefault(g => g.Id == targetId); var targetRow = target?.Row ?? GetDropRow(sender); _manager.MoveGroupTo(sourceId, targetId, targetRow); Build(); e.Handled = true;
        }

        private void GroupsPanel_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(GroupDragFormat)) { EnsureBothRowsVisibleForDrag(); e.Effects = DragDropEffects.Move; } else e.Effects = DragDropEffects.None; e.Handled = true;
        }

        private void GroupsPanel_Drop(object sender, DragEventArgs e)
        {
            _dragCandidate = false; if (!e.Data.GetDataPresent(GroupDragFormat)) return; var sourceId = e.Data.GetData(GroupDragFormat) as string; if (string.IsNullOrWhiteSpace(sourceId)) return;
            var row = GetDropRow(sender); _manager.MoveGroupTo(sourceId, null, row); Build(); e.Handled = true;
        }

        private int GetDropRow(object sender) => ReferenceEquals(sender, BottomGroupsPanel) || ReferenceEquals(sender, BottomGroupsScroll) ? 1 : 0;
        public void SaveLayout() => _manager.SaveLayout();
    }
}
