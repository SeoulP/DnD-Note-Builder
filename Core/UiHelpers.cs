using Godot;

public static class UiHelpers
{
    public static void WireSectionToggle(Button toggle, Control content, bool startCollapsed = false)
    {
        content.Visible = !startCollapsed;
        string label    = toggle.Text;
        toggle.Text     = (startCollapsed ? "▶  " : "▼  ") + label;
        toggle.Pressed += () =>
        {
            content.Visible = !content.Visible;
            toggle.Text     = (content.Visible ? "▼  " : "▶  ") + label;
        };
    }
}
