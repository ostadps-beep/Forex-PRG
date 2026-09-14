using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ForexPanel.App.Toolbar
{
    public sealed class ToolbarManager
    {
        private const int LayoutVersion = 5;
        private readonly string _layoutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ForexPanel", "toolbar-layout.json");
        private readonly Dictionary<string, ToolbarTool> _catalog = new();
        public List<ToolbarGroup> Groups { get; } = new();

        public ToolbarManager() { Build(); RestoreLayout(); }

        public void Build()
        {
            Groups.Clear(); _catalog.Clear();
            var navigation = AddGroup("Navigation", "Navigation", 10, 0);
            AddTool(navigation, "Cursor", "Cursor", "Icon.Cursor", 10);
            AddTool(navigation, "Crosshair", "Crosshair", "Icon.Crosshair", 20, true);
            AddTool(navigation, "Timeframe", "Timeframe", "", 40, false, true);
            var zoom = AddGroup("Zoom", "Zoom", 20, 0);
            AddTool(zoom, "ZoomIn", "Zoom In", "Icon.ZoomIn", 10);
            AddTool(zoom, "ZoomOut", "Zoom Out", "Icon.ZoomOut", 20);
            AddTool(zoom, "ZoomArea", "Zoom Area", "Icon.ZoomArea", 30);
            AddTool(zoom, "Reset", "Reset", "Icon.Reset", 40);
            var chart = AddGroup("Chart", "Chart", 30, 0);
            AddTool(chart, "AutoScroll", "Auto Scroll", "Icon.AutoScroll", 10, true);
            AddTool(chart, "ChartShift", "Chart Shift", "Icon.ChartShift", 20, true);
            var chartType = AddGroup("ChartType", "Chart Type", 35, 0);
            AddTool(chartType, "ChartType.Candlestick", "Candlestick", "Icon.ChartCandlestick", 10, true);
            AddTool(chartType, "ChartType.HollowCandlestick", "Hollow Candles", "Icon.ChartHollowCandlestick", 20, true);
            AddTool(chartType, "ChartType.Bar", "Bar", "Icon.ChartBar", 30, true);
            AddTool(chartType, "ChartType.Line", "Line", "Icon.ChartLine", 40, true);
            AddTool(chartType, "ChartType.Area", "Area", "Icon.ChartArea", 50, true);
            var display = AddGroup("Display", "Display", 10, 1);
            AddTool(display, "Grid", "Grid", "Icon.Grid", 10, true);
            AddTool(display, "Volume", "Volume", "Icon.Volume", 20, true);
            var drawing = AddGroup("Drawing", "Drawing", 20, 1);
            AddTool(drawing, "HorizontalLine", "Horizontal Line", "Icon.HorizontalLine", 10);
            AddTool(drawing, "VerticalLine", "Vertical Line", "Icon.VerticalLine", 20);
            AddTool(drawing, "TrendLine", "Trend Line", "Icon.TrendLine", 30);
            AddTool(drawing, "Ray", "Ray", "Icon.Ray", 40);
            AddTool(drawing, "Rectangle", "Rectangle", "Icon.Rectangle", 50);
            var analysis = AddGroup("Analysis", "Analysis", 30, 1);
            AddTool(analysis, "Indicators", "Indicators", "Icon.Indicators", 10);
            var fibonacci = AddGroup("Fibonacci", "Fibonacci", 40, 1);
            AddTool(fibonacci, "Fibonacci", "Fibonacci", "Icon.Fibonacci", 10);
            var channels = AddGroup("Channels", "Channels", 50, 1);
            AddTool(channels, "ParallelChannel", "Parallel Channel", "Icon.TrendLine", 10);
            var objects = AddGroup("Objects", "Objects", 60, 1);
            AddTool(objects, "Delete", "Delete", "Icon.Delete", 20);
            AddTool(objects, "DeleteAll", "Delete All", "Icon.DeleteAll", 30);
            AddTool(objects, "Settings", "Settings", "Icon.Settings", 40);
        }

        private ToolbarGroup AddGroup(string id, string title, int order, int row)
        {
            var group = new ToolbarGroup { Id = id, Title = title, Order = order, Priority = order, Row = row };
            Groups.Add(group); return group;
        }

        public ToolbarGroup CreateGroup(string? title = null, int row = 0)
        {
            var index = 1; string id;
            do { id = $"CustomBox{index}"; index++; } while (Groups.Any(g => string.Equals(g.Id, id, StringComparison.Ordinal)));
            var order = GetNextGroupOrder(row);
            var group = new ToolbarGroup { Id = id, Title = string.IsNullOrWhiteSpace(title) ? $"Box {index - 1}" : title.Trim(), Row = Math.Clamp(row, 0, 1), Order = order, Priority = order };
            Groups.Add(group); SaveLayout(); return group;
        }

        public bool DeleteGroup(string groupId)
        {
            var group = Groups.FirstOrDefault(g => g.Id == groupId); if (group == null) return false;
            Groups.Remove(group);
            for (int row = 0; row <= 1; row++) NormalizeRow(Groups.Where(g => g.Row == row).OrderBy(g => g.Order).ThenBy(g => g.Priority).ToList());
            SaveLayout(); return true;
        }

        private int GetNextGroupOrder(int row) => Groups.Where(g => g.Row == row).Select(g => g.Order).DefaultIfEmpty(0).Max() + 10;

        private void AddTool(ToolbarGroup group, string id, string title, string iconKey, int order, bool toggle = false, bool textOnly = false)
        {
            var tool = new ToolbarTool { Id = id, Title = title, ToolTip = title, IconKey = iconKey, GroupId = group.Id, Order = order, Priority = order, CanOverflow = true, IsToggle = toggle, IsChecked = false, IsTextOnly = textOnly };
            group.Tools.Add(tool); _catalog[id] = CloneTool(tool, group.Id);
        }

        public IEnumerable<ToolbarTool> GetAvailableTools(string groupId)
        {
            var assignedIds = Groups.SelectMany(g => g.Tools).Select(t => t.Id).ToHashSet(StringComparer.Ordinal);
            return _catalog.Values.Where(t => !assignedIds.Contains(t.Id)).OrderBy(t => t.Title).Select(t => CloneTool(t, groupId)).ToList();
        }

        public bool AddToolToGroup(string toolId, string groupId)
        {
            var group = Groups.FirstOrDefault(g => g.Id == groupId); if (group == null || !_catalog.TryGetValue(toolId, out var template)) return false;
            if (Groups.SelectMany(g => g.Tools).Any(t => t.Id == toolId)) return false;
            var nextOrder = group.Tools.Count == 0 ? 10 : group.Tools.Max(t => t.Order) + 10;
            var tool = CloneTool(template, group.Id); tool.Order = nextOrder; tool.Priority = nextOrder; group.Tools.Add(tool); SaveLayout(); return true;
        }

        public bool RemoveToolFromGroup(string toolId, string groupId)
        {
            var group = Groups.FirstOrDefault(g => g.Id == groupId); if (group == null) return false;
            var tool = group.Tools.FirstOrDefault(t => t.Id == toolId); if (tool == null) return false;
            group.Tools.Remove(tool); NormalizeTools(group); SaveLayout(); return true;
        }

        private static ToolbarTool CloneTool(ToolbarTool source, string groupId) => new ToolbarTool { Id = source.Id, Title = source.Title, ToolTip = source.ToolTip, IconKey = source.IconKey, GroupId = groupId, Order = source.Order, Priority = source.Priority, CanOverflow = source.CanOverflow, IsToggle = source.IsToggle, IsChecked = source.IsChecked, IsTextOnly = source.IsTextOnly, Execute = source.Execute };

        public void MoveGroup(string groupId, int direction)
        {
            var group = Groups.FirstOrDefault(g => g.Id == groupId); if (group == null) return;
            var rowGroups = Groups.Where(g => g.Row == group.Row).OrderBy(g => g.Order).ThenBy(g => g.Priority).ToList();
            var index = rowGroups.FindIndex(g => g.Id == groupId); if (index < 0) return;
            var newIndex = index + direction; if (newIndex < 0 || newIndex >= rowGroups.Count) return;
            (rowGroups[index], rowGroups[newIndex]) = (rowGroups[newIndex], rowGroups[index]); NormalizeRow(rowGroups); SaveLayout();
        }

        public void MoveGroupTo(string groupId, string? targetGroupId, int targetRow)
        {
            var source = Groups.FirstOrDefault(g => g.Id == groupId); if (source == null) return;
            targetRow = Math.Clamp(targetRow, 0, 1);
            var target = string.IsNullOrWhiteSpace(targetGroupId) ? null : Groups.FirstOrDefault(g => g.Id == targetGroupId);
            if (target?.Id == source.Id) return;
            var sourceRowGroups = Groups.Where(g => g.Row == source.Row && g.Id != source.Id).OrderBy(g => g.Order).ThenBy(g => g.Priority).ToList();
            source.Row = targetRow;
            var targetRowGroups = Groups.Where(g => g.Row == targetRow && g.Id != source.Id).OrderBy(g => g.Order).ThenBy(g => g.Priority).ToList();
            int insertIndex = target == null ? targetRowGroups.Count : targetRowGroups.FindIndex(g => g.Id == target.Id);
            if (insertIndex < 0) insertIndex = targetRowGroups.Count;
            targetRowGroups.Insert(insertIndex, source); NormalizeRow(sourceRowGroups); NormalizeRow(targetRowGroups); SaveLayout();
        }

        private static void NormalizeRow(List<ToolbarGroup> groups) { for (int i = 0; i < groups.Count; i++) { groups[i].Order = (i + 1) * 10; groups[i].Priority = (i + 1) * 10; } }
        private static void NormalizeTools(ToolbarGroup group) { var tools = group.OrderedTools.ToList(); for (int i = 0; i < tools.Count; i++) { tools[i].Order = (i + 1) * 10; tools[i].Priority = (i + 1) * 10; } }

        public void MoveTool(string toolId, string groupId, int direction)
        {
            var group = Groups.FirstOrDefault(g => g.Id == groupId); if (group == null) return;
            var ordered = group.OrderedTools.ToList(); var index = ordered.FindIndex(t => t.Id == toolId); if (index < 0) return;
            var newIndex = index + direction; if (newIndex < 0 || newIndex >= ordered.Count) return;
            (ordered[index], ordered[newIndex]) = (ordered[newIndex], ordered[index]);
            for (int i = 0; i < ordered.Count; i++) { ordered[i].Order = (i + 1) * 10; ordered[i].Priority = (i + 1) * 10; }
            SaveLayout();
        }

        public void SaveLayout()
        {
            try
            {
                var directory = Path.GetDirectoryName(_layoutPath); if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                var layout = new ToolbarLayout { Version = LayoutVersion, GroupOrder = Groups.OrderBy(g => g.Row).ThenBy(g => g.Order).Select(g => g.Id).ToList() };
                foreach (var group in Groups) { layout.GroupRows[group.Id] = group.Row; layout.ToolOrder[group.Id] = group.OrderedTools.Select(t => t.Id).ToList(); }
                var json = JsonSerializer.Serialize(layout, new JsonSerializerOptions { WriteIndented = true }); File.WriteAllText(_layoutPath, json);
            }
            catch { }
        }

        public void RestoreLayout()
        {
            try
            {
                if (!File.Exists(_layoutPath)) return;
                var json = File.ReadAllText(_layoutPath); var layout = JsonSerializer.Deserialize<ToolbarLayout>(json); if (layout == null || layout.Version != LayoutVersion) return;
                if (layout.GroupOrder.Count > 0)
                {
                    var rank = layout.GroupOrder.Select((id, index) => new { id, index }).ToDictionary(x => x.id, x => x.index);
                    foreach (var group in Groups) if (rank.TryGetValue(group.Id, out var index)) group.Order = (index + 1) * 10;
                }
                foreach (var group in Groups)
                {
                    if (layout.GroupRows.TryGetValue(group.Id, out var row)) group.Row = Math.Clamp(row, 0, 1);
                    if (!layout.ToolOrder.TryGetValue(group.Id, out var ids)) continue;
                    var restoredTools = new List<ToolbarTool>();
                    foreach (var id in ids) if (_catalog.TryGetValue(id, out var template)) restoredTools.Add(CloneTool(template, group.Id));
                    group.Tools.Clear();
                    for (int i = 0; i < restoredTools.Count; i++) { restoredTools[i].Order = (i + 1) * 10; restoredTools[i].Priority = (i + 1) * 10; group.Tools.Add(restoredTools[i]); }
                }
                for (int row = 0; row <= 1; row++) NormalizeRow(Groups.Where(g => g.Row == row).OrderBy(g => g.Order).ThenBy(g => g.Priority).ToList());
            }
            catch { }
        }
    }
}
