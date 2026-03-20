using Godot;

/// <summary>
/// A self-contained row with auto-scrolling text and a hover-reveal × delete button.
/// Emits NavigatePressed when the label area is clicked, DeletePressed when × is clicked.
/// Used for faction rows, sub-location rows, and similar lists.
/// </summary>
public partial class EntityRow : PanelContainer
{
    [Signal] public delegate void NavigatePressedEventHandler();
    [Signal] public delegate void DeletePressedEventHandler();

    private string       _text = "";
    private Label        _label;
    private StyleBoxFlat _rowHoverBox;
    private StyleBoxFlat _deleteHoverBox;

    public string Text
    {
        get => _text;
        set { _text = value; if (_label != null) _label.Text = value; }
    }

    public override void _Ready()
    {
        _rowHoverBox    = GetThemeStylebox("row_hover",    "DndBuilder") as StyleBoxFlat ?? MakeBox(new Color(0.35f, 0.50f, 0.70f));
        _deleteHoverBox = GetThemeStylebox("delete_hover", "DndBuilder") as StyleBoxFlat ?? MakeBox(new Color(0.76f, 0.46f, 0.54f));

        MouseDefaultCursorShape = CursorShape.PointingHand;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 0);

        // ── scrolling text clip ──────────────────────────────────────────────
        var clip = new Control();
        clip.ClipContents       = true;
        clip.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        clip.CustomMinimumSize  = new Vector2(0, 28);

        _label = new Label
        {
            Text              = _text,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode      = TextServer.AutowrapMode.Off,
            Position          = new Vector2(4, 0),
        };
        clip.AddChild(_label);

        // Transparent overlay: handles click and hover for the text area
        var navBtn = new Button { Flat = true, MouseDefaultCursorShape = CursorShape.PointingHand };
        navBtn.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        clip.AddChild(navBtn);

        clip.Resized += () =>
            _label.Size = new Vector2(Mathf.Max(_label.GetMinimumSize().X + 8, clip.Size.X), clip.Size.Y);

        // ── delete button ────────────────────────────────────────────────────
        var delBtn = new Button { Text = "×", Flat = true, MouseDefaultCursorShape = CursorShape.PointingHand };
        delBtn.Modulate = new Color(1, 1, 1, 0);  // hidden until row hovered

        // ── hover effects ────────────────────────────────────────────────────
        Tween tween = null;

        navBtn.MouseEntered += () =>
        {
            delBtn.Modulate = Colors.White;
            AddThemeStyleboxOverride("panel", _rowHoverBox);
            tween?.Kill();
            float overflow = _label.GetMinimumSize().X + 8 - clip.Size.X;
            if (overflow > 0)
            {
                tween = clip.CreateTween().SetTrans(Tween.TransitionType.Linear);
                tween.TweenProperty(_label, "position:x", 4f - overflow, overflow / 80f);
            }
        };
        navBtn.MouseExited += () =>
        {
            delBtn.Modulate = new Color(1, 1, 1, 0);
            RemoveThemeStyleboxOverride("panel");
            tween?.Kill();
            tween = clip.CreateTween();
            tween.TweenProperty(_label, "position:x", 4f, 0.2f);
        };

        delBtn.MouseEntered += () => { delBtn.Modulate = Colors.White; AddThemeStyleboxOverride("panel", _deleteHoverBox); };
        delBtn.MouseExited  += () => RemoveThemeStyleboxOverride("panel");

        var confirmDialog = DialogHelper.Make(text: "Remove this entry? This cannot be undone.");
        confirmDialog.Confirmed += () => EmitSignal(SignalName.DeletePressed);
        AddChild(confirmDialog);

        navBtn.Pressed += () => EmitSignal(SignalName.NavigatePressed);
        delBtn.Pressed += () => DialogHelper.Show(confirmDialog);

        hbox.AddChild(clip);
        hbox.AddChild(delBtn);
        AddChild(hbox);
    }

    private static StyleBoxFlat MakeBox(Color color)
    {
        var box = new StyleBoxFlat { BgColor = color };
        box.SetCornerRadiusAll(3);
        return box;
    }
}
