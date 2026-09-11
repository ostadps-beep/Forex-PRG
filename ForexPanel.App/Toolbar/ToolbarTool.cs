using System;

namespace ForexPanel.App.Toolbar
{
    public sealed class ToolbarTool
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Text
        {
            get => Title;
            set => Title = value;
        }

        public string ToolTip { get; set; } = "";
        public string IconKey { get; set; } = "";
        public string GroupId { get; set; } = "";
        public int Order { get; set; }
        public int Priority { get; set; }
        public bool CanOverflow { get; set; } = true;
        public bool IsToggle { get; set; }
        public bool IsChecked { get; set; }
        public bool IsTextOnly { get; set; }

        public Action? Execute { get; set; }

        public static event EventHandler<string>? ToolRequested;

        public void Run()
        {
            if (Execute != null)
            {
                Execute.Invoke();
                return;
            }

            ToolRequested?.Invoke(this, Id);
        }
    }
}
