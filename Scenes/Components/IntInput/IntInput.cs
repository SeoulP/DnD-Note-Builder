using Godot;
using System;

public partial class IntInput : PanelContainer
{
    [Export] public int MinValue { get; set; } = 0;
    [Export] public int MaxValue { get; set; } = 99;

    private int          _value;
    private LineEdit     _edit;
    private StyleBoxFlat _editHoverStyle;
    private StyleBoxFlat _upHoverStyle;
    private StyleBoxFlat _downHoverStyle;

    [Export]
    public int Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, MinValue, MaxValue);
            if (_edit != null) _edit.Text = _value.ToString();
        }
    }

    [Signal] public delegate void ValueChangedEventHandler(int value);

    public override void _Ready()
    {
        _value = Math.Clamp(_value, MinValue, MaxValue);
        CustomMinimumSize = new Vector2(CustomMinimumSize.X, Mathf.Max(CustomMinimumSize.Y, 32));

        // No panel override — inherit theme default, same as EntityRow at rest.

        var hbox = new HBoxContainer(); hbox.AddThemeConstantOverride("separation", 0);
        _edit = new LineEdit
        {
            Text                = _value.ToString(),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Alignment           = HorizontalAlignment.Center,
        };
        var noStyle = new StyleBoxFlat
        {
            DrawCenter      = false,
            BorderWidthLeft = 0, BorderWidthRight  = 0,
            BorderWidthTop  = 0, BorderWidthBottom = 0,
        };
        _editHoverStyle = new StyleBoxFlat
        {
            BgColor                = ThemeManager.Instance.Current.Hover,
            DrawCenter             = true,
            CornerRadiusTopLeft    = 3, CornerRadiusTopRight    = 0,
            CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 0,
        };
        _edit.AddThemeStyleboxOverride("normal",    noStyle);
        _edit.AddThemeStyleboxOverride("focus",     noStyle);
        _edit.AddThemeStyleboxOverride("read_only", noStyle);
        _edit.AddThemeStyleboxOverride("hover",     _editHoverStyle);

        var arrowCol = new VBoxContainer(); arrowCol.AddThemeConstantOverride("separation", 0); arrowCol.CustomMinimumSize = new Vector2(16, 0);

        _upHoverStyle = new StyleBoxFlat { BgColor = ThemeManager.Instance.Current.Hover };
        _upHoverStyle.CornerRadiusTopRight = 3;
        _downHoverStyle = new StyleBoxFlat { BgColor = ThemeManager.Instance.Current.Hover };
        _downHoverStyle.CornerRadiusBottomRight = 3;

        var emptyStyle = new StyleBoxEmpty();

        var upBtn = new Button { Text = "▲", Flat = false, FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand };
        upBtn.AddThemeFontSizeOverride("font_size", 7); upBtn.SizeFlagsVertical = SizeFlags.ExpandFill;
        upBtn.AddThemeStyleboxOverride("normal",   emptyStyle);
        upBtn.AddThemeStyleboxOverride("pressed",  emptyStyle);
        upBtn.AddThemeStyleboxOverride("focus",    emptyStyle);
        upBtn.AddThemeStyleboxOverride("disabled", emptyStyle);
        upBtn.AddThemeStyleboxOverride("hover",    _upHoverStyle);

        var downBtn = new Button { Text = "▼", Flat = false, FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand };
        downBtn.AddThemeFontSizeOverride("font_size", 7); downBtn.SizeFlagsVertical = SizeFlags.ExpandFill;
        downBtn.AddThemeStyleboxOverride("normal",   emptyStyle);
        downBtn.AddThemeStyleboxOverride("pressed",  emptyStyle);
        downBtn.AddThemeStyleboxOverride("focus",    emptyStyle);
        downBtn.AddThemeStyleboxOverride("disabled", emptyStyle);
        downBtn.AddThemeStyleboxOverride("hover",    _downHoverStyle);

        void Commit(string text)
        {
            if (!int.TryParse(text, out int v)) { _edit.Text = _value.ToString(); return; }
            Value = v;
            EmitSignal(SignalName.ValueChanged, _value);
        }
        upBtn.Pressed      += () => { Value = _value + 1; EmitSignal(SignalName.ValueChanged, _value); };
        downBtn.Pressed    += () => { Value = _value - 1; EmitSignal(SignalName.ValueChanged, _value); };
        _edit.FocusExited   += () => Commit(_edit.Text);
        _edit.TextSubmitted += t  => Commit(t);

        arrowCol.AddChild(upBtn); arrowCol.AddChild(downBtn);
        hbox.AddChild(_edit); hbox.AddChild(arrowCol);
        AddChild(hbox);

        ThemeManager.Instance.ThemeChanged += OnThemeChanged;
    }

    public override void _ExitTree()
    {
        if (ThemeManager.Instance != null)
            ThemeManager.Instance.ThemeChanged -= OnThemeChanged;
    }

    private void OnThemeChanged()
    {
        var hover           = ThemeManager.Instance.Current.Hover;
        _editHoverStyle.BgColor  = hover;
        _upHoverStyle.BgColor    = hover;
        _downHoverStyle.BgColor  = hover;
    }

    public static IntInput Make(int value, int min, int max, Action<int> onChange)
    {
        var inp = new IntInput { MinValue = min, MaxValue = max, Value = value };
        inp.ValueChanged += v => onChange(v);
        return inp;
    }
}
