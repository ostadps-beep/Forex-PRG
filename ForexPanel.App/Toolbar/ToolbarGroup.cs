using System.Collections.Generic;
using System.Linq;

namespace ForexPanel.App.Toolbar
{
    public sealed class ToolbarGroup
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public int Order { get; set; }
        public int Priority { get; set; }
        public int Row { get; set; }

        public List<ToolbarTool> Tools { get; } = new();

        public IEnumerable<ToolbarTool> OrderedTools =>
            Tools.OrderBy(t => t.Order).ThenBy(t => t.Priority).ThenBy(t => t.Id);
    }
}
