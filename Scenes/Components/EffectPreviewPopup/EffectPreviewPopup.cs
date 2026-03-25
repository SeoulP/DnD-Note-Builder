using Godot;

/// <summary>
/// A floating read-only overlay that shows an ability's Effect text on hover.
/// Implemented as a PanelContainer (not a Window/Popup) so it never steals focus
/// or interrupts ScrollContainer scrolling. Must be added inside a CanvasLayer
/// so it renders above the ScrollContainer without being clipped by it.
/// MouseFilter = Ignore means it consumes zero input events.
/// </summary>
public partial class EffectPreviewPopup : PanelContainer
{
    private Label _label;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Hide();

        var bg = new StyleBoxFlat { BgColor = ThemeManager.Instance.Current.Component };
        bg.SetCornerRadiusAll(6);
        bg.ContentMarginLeft   = bg.ContentMarginRight  = 12;
        bg.ContentMarginTop    = bg.ContentMarginBottom = 8;
        AddThemeStyleboxOverride("panel", bg);

        _label = new Label
        {
            AutowrapMode      = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(240, 0),
            MouseFilter       = MouseFilterEnum.Ignore,
        };
        _label.AddThemeFontSizeOverride("font_size", 13);
        AddChild(_label);
    }

    /// <summary>
    /// Position below the hovered row at the cursor's X, then show.
    /// anchor.GetGlobalRect() and the CanvasLayer share the same coordinate space.
    /// </summary>
    public void ShowFor(string effect, Control anchor)
    {
        if (string.IsNullOrWhiteSpace(effect)) { Hide(); return; }

        _label.Text = effect;

        var rect   = anchor.GetGlobalRect();
        var mouseX = anchor.GetGlobalMousePosition().X;

        Position = new Vector2(mouseX, rect.End.Y + 4);
        Show();
    }
}
