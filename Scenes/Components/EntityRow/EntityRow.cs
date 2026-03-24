using Godot;

/// <summary>
/// A self-contained row with auto-scrolling text and a hover-reveal × delete button.
/// Emits NavigatePressed when the label area is clicked, DeletePressed when × is clicked.
/// Used for faction rows, sub-location rows, and similar lists.
/// </summary>
public partial class EntityRow : PanelContainer
{
    [Signal] public delegate void NavigatePressedEventHandler();
    [Signal] public delegate void NavigatePressedNewTabEventHandler();
    [Signal] public delegate void DeletePressedEventHandler();

    private string       _text        = "";
    private string       _description = "";
    private Label        _label;
    private Label        _descLabel;
    private StyleBoxFlat _rowHoverBox;
    private StyleBoxFlat _deleteHoverBox;

    public string Text
    {
        get => _text;
        set { _text = value; if (_label != null) _label.Text = value; }
    }

    public string Description
    {
        get => _description;
        set { _description = value; if (_descLabel != null) _descLabel.Text = value; }
    }

    public bool ShowDelete      { get; set; } = true;
    public bool ShowDescription { get; set; } = false;

    public override void _Ready()
    {
        _rowHoverBox    = GetThemeStylebox("row_hover",    "DndBuilder") as StyleBoxFlat ?? MakeBox(ThemeManager.Instance.Current.Hover);
        _deleteHoverBox = GetThemeStylebox("delete_hover", "DndBuilder") as StyleBoxFlat ?? MakeBox(ThemeManager.DeleteHoverColor);

        MouseDefaultCursorShape = CursorShape.PointingHand;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 0);

        // ── scrolling text clip ──────────────────────────────────────────────
        var clip = new Control();
        clip.ClipContents        = true;
        clip.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        clip.CustomMinimumSize   = new Vector2(0, 28);
        clip.MouseFilter         = MouseFilterEnum.Ignore;

        _label = new Label
        {
            Text              = _text,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode      = TextServer.AutowrapMode.Off,
            Position          = new Vector2(6, 0),
        };
        clip.AddChild(_label);

        // Transparent overlay: handles clicks for the text area.
        // MouseFilter = Pass so hover events propagate to the PanelContainer.
        var navBtn = new Button { Flat = true, MouseDefaultCursorShape = CursorShape.PointingHand, MouseFilter = MouseFilterEnum.Pass };
        navBtn.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        clip.AddChild(navBtn);

        clip.Resized += () =>
            _label.Size = new Vector2(Mathf.Max(_label.GetMinimumSize().X + 8, clip.Size.X), clip.Size.Y);

        // ── delete button (optional) ─────────────────────────────────────────
        Button delBtn = null;
        if (ShowDelete)
        {
            delBtn = new Button { Text = "×", Flat = true, MouseDefaultCursorShape = CursorShape.PointingHand, MouseFilter = MouseFilterEnum.Pass };
            delBtn.Modulate = new Color(1, 1, 1, 0);  // hidden until row hovered

            // Restore row highlight when leaving the delete button (still inside the row)
            delBtn.MouseEntered += () => AddThemeStyleboxOverride("panel", _deleteHoverBox);
            delBtn.MouseExited  += () => AddThemeStyleboxOverride("panel", _rowHoverBox);

            var confirmDialog = DialogHelper.Make(text: "Remove this entry? This cannot be undone.");
            confirmDialog.Confirmed += () => EmitSignal(SignalName.DeletePressed);
            AddChild(confirmDialog);

            delBtn.Pressed += () => DialogHelper.Show(confirmDialog);
        }

        navBtn.GuiInput += e =>
        {
            if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb)
            {
                navBtn.AcceptEvent();
                if (mb.CtrlPressed)
                    EmitSignal(SignalName.NavigatePressedNewTab);
                else
                    EmitSignal(SignalName.NavigatePressed);
            }
        };

        // ── hover effects on the whole row ───────────────────────────────────
        Tween tween = null;

        MouseEntered += () =>
        {
            if (delBtn != null) delBtn.Modulate = Colors.White;
            AddThemeStyleboxOverride("panel", _rowHoverBox);
            tween?.Kill();
            float overflow = _label.GetMinimumSize().X + 8 - clip.Size.X;
            if (overflow > 0)
            {
                tween = clip.CreateTween().SetTrans(Tween.TransitionType.Linear);
                tween.TweenProperty(_label, "position:x", 6f - overflow, overflow / 80f);
            }
        };
        MouseExited += () =>
        {
            if (delBtn != null) delBtn.Modulate = new Color(1, 1, 1, 0);
            RemoveThemeStyleboxOverride("panel");
            tween?.Kill();
            tween = clip.CreateTween();
            tween.TweenProperty(_label, "position:x", 6f, 0.2f);
        };

        hbox.AddChild(clip);
        if (delBtn != null) hbox.AddChild(delBtn);

        if (ShowDescription)
        {
            var vbox = new VBoxContainer();
            vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            vbox.AddThemeConstantOverride("separation", 2);
            vbox.AddChild(hbox);

            var descMargin = new MarginContainer();
            descMargin.AddThemeConstantOverride("margin_left", 6);
            descMargin.MouseDefaultCursorShape = CursorShape.PointingHand;
            descMargin.GuiInput += e =>
            {
                if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb)
                {
                    descMargin.AcceptEvent();
                    if (mb.CtrlPressed)
                        EmitSignal(SignalName.NavigatePressedNewTab);
                    else
                        EmitSignal(SignalName.NavigatePressed);
                }
            };

            _descLabel = new Label
            {
                Text         = _description,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            _descLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.65f, 0.65f));
            _descLabel.AddThemeFontSizeOverride("font_size", 12);
            descMargin.AddChild(_descLabel);
            vbox.AddChild(descMargin);

            AddChild(vbox);
        }
        else
        {
            AddChild(hbox);
        }
    }

    private static StyleBoxFlat MakeBox(Color color)
    {
        var box = new StyleBoxFlat { BgColor = color };
        box.SetCornerRadiusAll(3);
        return box;
    }
}
