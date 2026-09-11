using System.Collections.Generic;

namespace ForexPanel.App.Toolbar
{
    public sealed class ToolbarLayout
    {
        public int Version { get; set; } = 2;

        public List<string> GroupOrder { get; set; } = new();

        public Dictionary<string, List<string>> ToolOrder { get; set; } = new();

        public Dictionary<string, int> GroupRows { get; set; } = new();
    }
}
