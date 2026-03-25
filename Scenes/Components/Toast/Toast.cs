using Godot;

public partial class Toast : CanvasLayer
{
    private PanelContainer _panel;
    private Label          _icon;
    private Label          _message;
    private Tween          _tween;

    private StyleBoxFlat _errorStyle;
    private StyleBoxFlat _warnStyle;
    private StyleBoxFlat _infoStyle;

    public override void _Ready()
    {
        _panel   = GetNode<PanelContainer>("Root/Panel");
        _icon    = GetNode<Label>("Root/Panel/HBoxContainer/Icon");
        _message = GetNode<Label>("Root/Panel/HBoxContainer/Message");

        _panel.Modulate = new Color(1, 1, 1, 0);

        _errorStyle = MakeStyle(new Color(0.75f, 0.20f, 0.20f));
        _warnStyle  = MakeStyle(new Color(0.72f, 0.55f, 0.10f));
        _infoStyle  = MakeStyle(new Color(0.25f, 0.35f, 0.50f));
    }

    public void Show(string message, LogLevel level)
    {
        _tween?.Kill();

        _message.Text = message;
        _icon.Text = level switch
        {
            LogLevel.Error   => "✕",
            LogLevel.Warning => "⚠",
            _                => "ℹ",
        };
        _panel.AddThemeStyleboxOverride("panel", level switch
        {
            LogLevel.Error   => _errorStyle,
            LogLevel.Warning => _warnStyle,
            _                => _infoStyle,
        });

        _tween = CreateTween();
        _tween.TweenProperty(_panel, "modulate:a", 1.0f, 0.15f);
        _tween.TweenInterval(4.0);
        _tween.TweenProperty(_panel, "modulate:a", 0.0f, 0.30f);
    }

    private static StyleBoxFlat MakeStyle(Color bg)
    {
        var sb = new StyleBoxFlat { BgColor = bg };
        sb.SetCornerRadiusAll(6);
        sb.ContentMarginLeft  = sb.ContentMarginRight  = 12;
        sb.ContentMarginTop   = sb.ContentMarginBottom =  8;
        return sb;
    }
}
