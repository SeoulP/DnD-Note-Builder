using DndBuilder.Core.Models;
using Godot;

public partial class Pf2eCombatantRow : PanelContainer
{
    [Signal] public delegate void SelectedEventHandler(int combatantId);
    [Signal] public delegate void HpChangedEventHandler(int combatantId, int newHp, string reason);
    [Signal] public delegate void InitiativeChangedEventHandler(int combatantId, int newInitiative);
    [Signal] public delegate void DefeatedEventHandler(int combatantId);
    [Signal] public delegate void ActionsChangedEventHandler(int combatantId, int newCount);
    [Signal] public delegate void RemovedEventHandler(int combatantId);
    [Signal] public delegate void EntityOpenRequestedEventHandler(string entityType, int entityId);

    // Set by the parent pane for drag-reorder coordination
    public System.Action<int, int> OnLiveReorder; // (draggedId, targetId)
    public System.Action           OnDrop;
    public int CombatantId => _combatant?.Id ?? -1;

    [Export] private Panel   _colorDot;
    [Export] private Button  _nameLabel;
    [Export] private SpinBox _initiativeInput;
    [Export] private Button  _hpButton;
    [Export] private Button  _action1Btn;
    [Export] private Button  _action2Btn;
    [Export] private Button  _action3Btn;
    [Export] private Button  _deleteButton;

    private DatabaseService        _db;
    private Pf2eEncounterCombatant _combatant;
    private PopupPanel             _hpPopup;
    private LineEdit               _deltaInput;
    private LineEdit               _reasonInput;
    private VBoxContainer          _logContainer;
    private Button                 _syncHpBtn;
    private bool                   _loading;

    private static readonly Color CreatureColor  = new Color(0.9f, 0.3f, 0.3f);
    private static readonly Color PcColor        = new Color(0.3f, 0.8f, 0.4f);
    private static readonly Color CustomColor    = new Color(0.9f, 0.8f, 0.2f);
    private static readonly Color HoverDeleteBg  = new Color(0.75f, 0.10f, 0.10f, 0.35f);

    public override void _Ready()
    {
        _db = GetNode<DatabaseService>("/root/DatabaseService");

        _initiativeInput.ValueChanged += v =>
        {
            if (_loading || _combatant == null) return;
            _combatant.Initiative = (int)v;
            EmitSignal(SignalName.InitiativeChanged, _combatant.Id, (int)v);
        };
        _initiativeInput.GuiInput += e =>
        {
            if (e is not InputEventMouseButton { Pressed: true } btn) return;
            if (btn.ButtonIndex == MouseButton.WheelUp)
                { _initiativeInput.Value += 1; _initiativeInput.AcceptEvent(); }
            else if (btn.ButtonIndex == MouseButton.WheelDown)
                { _initiativeInput.Value -= 1; _initiativeInput.AcceptEvent(); }
        };

        _hpButton.Pressed   += ShowHpPopup;
        _action1Btn.Pressed += () => SetActionCount(1);
        _action2Btn.Pressed += () => SetActionCount(2);
        _action3Btn.Pressed += () => SetActionCount(3);
        _nameLabel.Pressed  += OpenLinkedEntity;

        _deleteButton.MouseEntered += () =>
        {
            var sb = new StyleBoxFlat { BgColor = HoverDeleteBg };
            AddThemeStyleboxOverride("panel", sb);
        };
        _deleteButton.MouseExited += () => RemoveThemeStyleboxOverride("panel");
        _deleteButton.Pressed += () =>
        {
            if (_combatant == null) return;
            var dlg = DialogHelper.Make("Remove Combatant");
            AddChild(dlg);
            DialogHelper.Show(dlg, $"Remove \"{_combatant.DisplayName}\" from this encounter?");
            dlg.Confirmed += () => EmitSignal(SignalName.Removed, _combatant.Id);
        };

        GuiInput += e =>
        {
            if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                EmitSignal(SignalName.Selected, _combatant?.Id ?? 0);
        };

        BuildHpPopup();
    }

    public void Setup(DatabaseService db, Pf2eEncounterCombatant combatant, bool isActiveTurn)
    {
        _db        = db;
        _combatant = combatant;
        Refresh(isActiveTurn);
    }

    public void Refresh(bool isActiveTurn)
    {
        if (_combatant == null) return;
        _loading = true;

        bool hasEntity = _combatant.CharacterId.HasValue || _combatant.CreatureId.HasValue;
        _nameLabel.Text          = hasEntity ? $"{_combatant.DisplayName} ↗" : _combatant.DisplayName;
        _nameLabel.AddThemeColorOverride("font_color", hasEntity ? new Color(0.55f, 0.80f, 1.0f) : new Color(1, 1, 1));
        _nameLabel.MouseDefaultCursorShape = hasEntity ? CursorShape.PointingHand : CursorShape.Arrow;
        _initiativeInput.Value   = _combatant.Initiative;
        _hpButton.Text           = $"❤ {_combatant.CurrentHp}/{_combatant.MaxHp}";
        if (GetParent() is MarginContainer wrapper)
            wrapper.AddThemeConstantOverride("margin_left", isActiveTurn ? 0 : 14);

        var dot = new StyleBoxFlat
        {
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            BgColor = _combatant.CharacterId.HasValue ? PcColor
                    : _combatant.CreatureId.HasValue  ? CreatureColor
                    : CustomColor
        };
        _colorDot.AddThemeStyleboxOverride("panel", dot);

        RefreshActionPips();
        Modulate = _combatant.IsActive ? new Color(1, 1, 1, 1) : new Color(1, 1, 1, 0.4f);

        _loading = false;
    }

    private void RefreshActionPips()
    {
        int r = _combatant?.ActionsRemaining ?? 3;
        SetPip(_action1Btn, r >= 1);
        SetPip(_action2Btn, r >= 2);
        SetPip(_action3Btn, r >= 3);
    }

    private static void SetPip(Button btn, bool filled)
    {
        btn.Text     = filled ? "●" : "○";
        btn.Modulate = filled ? new Color(1, 1, 1) : new Color(0.5f, 0.5f, 0.5f);
    }

    // pip=1,2,3: if remaining >= pip → spend to pip-1; else restore to pip.
    private void SetActionCount(int pip)
    {
        if (_combatant == null) return;
        int newRemaining = _combatant.ActionsRemaining >= pip ? pip - 1 : pip;
        newRemaining = Godot.Mathf.Clamp(newRemaining, 0, 3);
        _combatant.ActionsRemaining = newRemaining;
        _db.Pf2eEncounterCombatants.SetActionsRemaining(_combatant.Id, newRemaining);
        RefreshActionPips();
        EmitSignal(SignalName.ActionsChanged, _combatant.Id, newRemaining);
    }

    private void OpenLinkedEntity()
    {
        if (_combatant?.CharacterId.HasValue == true)
            EmitSignal(SignalName.EntityOpenRequested, "pf2e_pc", _combatant.CharacterId.Value);
        else if (_combatant?.CreatureId.HasValue == true)
            EmitSignal(SignalName.EntityOpenRequested, "pf2e_creature", _combatant.CreatureId.Value);
    }

    // ── Drag and Drop ────────────────────────────────────────────────────────

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (_combatant == null) return default;
        SetDragPreview(BuildDragGhost());
        return _combatant.Id;
    }

    public override bool _CanDropData(Vector2 at, Variant data)
    {
        if (data.VariantType != Variant.Type.Int) return false;
        int draggedId = data.AsInt32();
        if (_combatant == null || draggedId == _combatant.Id) return false;
        OnLiveReorder?.Invoke(draggedId, _combatant.Id);
        return true;
    }

    public override void _DropData(Vector2 at, Variant data) => OnDrop?.Invoke();


    private Control BuildDragGhost()
    {
        var entColor = _combatant.CharacterId.HasValue ? PcColor
                     : _combatant.CreatureId.HasValue  ? CreatureColor
                     : CustomColor;

        var ghost = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, ZIndex = 100 };
        ghost.CustomMinimumSize = new Vector2(Size.X, 0);
        var sb = new StyleBoxFlat { BgColor = new Color(0.15f, 0.15f, 0.25f, 0.92f) };
        sb.SetBorderWidthAll(1);
        sb.BorderColor = entColor;
        sb.SetCornerRadiusAll(3);
        ghost.AddThemeStyleboxOverride("panel", sb);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 0);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 6);

        var dot = new Panel { CustomMinimumSize = new Vector2(10, 10), MouseFilter = MouseFilterEnum.Ignore };
        var dotSb = new StyleBoxFlat { BgColor = entColor };
        dotSb.SetCornerRadiusAll(5);
        dot.AddThemeStyleboxOverride("panel", dotSb);

        var name = new Label
        {
            Text                = _combatant.DisplayName,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            AutowrapMode        = TextServer.AutowrapMode.Off,
        };

        var hp = new Label { Text = $"❤ {_combatant.CurrentHp}/{_combatant.MaxHp}" };
        hp.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.7f));

        hbox.AddChild(dot);
        hbox.AddChild(name);
        hbox.AddChild(hp);

        var stripe = new Panel { CustomMinimumSize = new Vector2(0, 3), MouseFilter = MouseFilterEnum.Ignore };
        var stripeSb = new StyleBoxFlat { BgColor = entColor };
        stripe.AddThemeStyleboxOverride("panel", stripeSb);

        vbox.AddChild(hbox);
        vbox.AddChild(stripe);
        ghost.AddChild(vbox);
        return ghost;
    }

    // ── HP Popup ─────────────────────────────────────────────────────────────

    private void BuildHpPopup()
    {
        _hpPopup = new PopupPanel { Size = new Vector2I(260, 260) };
        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left",   8);
        margin.AddThemeConstantOverride("margin_right",  8);
        margin.AddThemeConstantOverride("margin_top",    8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        _hpPopup.AddChild(margin);

        var vbox = new VBoxContainer();
        margin.AddChild(vbox);

        var changeRow = new HBoxContainer();
        var changeLabel = new Label { Text = "Change:" };
        changeLabel.CustomMinimumSize = new Vector2(58, 0);
        _deltaInput = new LineEdit { PlaceholderText = "+5, -8, =20" };
        _deltaInput.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        changeRow.AddChild(changeLabel);
        changeRow.AddChild(_deltaInput);
        vbox.AddChild(changeRow);

        var reasonRow = new HBoxContainer();
        var reasonLabel = new Label { Text = "Reason:" };
        reasonLabel.CustomMinimumSize = new Vector2(58, 0);
        _reasonInput = new LineEdit { PlaceholderText = "Optional" };
        _reasonInput.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        reasonRow.AddChild(reasonLabel);
        reasonRow.AddChild(_reasonInput);
        vbox.AddChild(reasonRow);

        var btnRow = new HBoxContainer();
        var applyBtn = new Button { Text = "Apply" };
        var undoBtn  = new Button { Text = "Undo" };
        btnRow.AddChild(applyBtn);
        btnRow.AddChild(undoBtn);
        vbox.AddChild(btnRow);

        _syncHpBtn = new Button { Text = "↺ Sync HP from Sheet", Visible = false };
        vbox.AddChild(_syncHpBtn);

        vbox.AddChild(new HSeparator());

        var logScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 80) };
        logScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _logContainer = new VBoxContainer();
        _logContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        logScroll.AddChild(_logContainer);
        vbox.AddChild(logScroll);

        AddChild(_hpPopup);

        _deltaInput.TextSubmitted += _ => ApplyHpChange();
        applyBtn.Pressed  += ApplyHpChange;
        undoBtn.Pressed   += UndoHpChange;
        _syncHpBtn.Pressed += SyncHpFromSheet;
    }

    private void ShowHpPopup()
    {
        if (_combatant == null) return;
        _deltaInput.Text   = "";
        _reasonInput.Text  = "";
        _syncHpBtn.Visible = _combatant.CharacterId.HasValue;
        PopulateHpLog();
        _hpPopup.PopupOnParent(new Rect2I(
            (int)GlobalPosition.X,
            (int)(GlobalPosition.Y + Size.Y),
            260, 260));
        _deltaInput.GrabFocus();
    }

    private void PopulateHpLog()
    {
        foreach (Node child in _logContainer.GetChildren()) child.QueueFree();
        var entries = _db.Pf2eEncounterCombatantHpLog.GetRecent(_combatant.Id, 10);
        foreach (var entry in entries)
        {
            var lbl = new Label { Text = $"{(entry.Delta >= 0 ? "+" : "")}{entry.Delta}  {entry.ReasonText}" };
            lbl.AddThemeColorOverride("font_color", entry.Delta >= 0
                ? new Color(0.4f, 0.9f, 0.4f)
                : new Color(0.9f, 0.4f, 0.4f));
            _logContainer.AddChild(lbl);
        }
    }

    private void ApplyHpChange()
    {
        if (_combatant == null) return;
        string raw = _deltaInput.Text.Trim();
        if (string.IsNullOrEmpty(raw)) { _hpPopup.Hide(); return; }

        int newHp;
        if (raw.StartsWith("=") && int.TryParse(raw[1..], out int abs))
            newHp = Godot.Mathf.Clamp(abs, 0, _combatant.MaxHp);
        else if (raw.StartsWith("+") && int.TryParse(raw[1..], out int heal))
            newHp = Godot.Mathf.Min(_combatant.CurrentHp + heal, _combatant.MaxHp);
        else if (raw.StartsWith("-") && int.TryParse(raw[1..], out int dmg))
            newHp = Godot.Mathf.Max(_combatant.CurrentHp - dmg, 0);
        else if (int.TryParse(raw, out int bare))
            newHp = Godot.Mathf.Max(_combatant.CurrentHp - bare, 0);
        else { _hpPopup.Hide(); return; }

        string reason = _reasonInput.Text.Trim();
        _db.Pf2eEncounterCombatants.UpdateHp(_combatant.Id, newHp, reason);
        _combatant = _db.Pf2eEncounterCombatants.Get(_combatant.Id);
        if (_combatant == null) return;

        Refresh(false);
        EmitSignal(SignalName.HpChanged, _combatant.Id, newHp, reason);
        if (newHp == 0 && _combatant.IsActive)
            EmitSignal(SignalName.Defeated, _combatant.Id);

        _deltaInput.Text  = "";
        _reasonInput.Text = "";
        PopulateHpLog();
    }

    private void UndoHpChange()
    {
        if (_combatant == null) return;
        _db.Pf2eEncounterCombatants.UndoLastHpChange(_combatant.Id);
        _combatant = _db.Pf2eEncounterCombatants.Get(_combatant.Id);
        if (_combatant == null) return;
        Refresh(false);
        EmitSignal(SignalName.HpChanged, _combatant.Id, _combatant.CurrentHp, "Undo");
        PopulateHpLog();
    }

    private void SyncHpFromSheet()
    {
        if (_combatant?.CharacterId == null) return;
        var pc = _db.Pf2eCharacters.Get(_combatant.CharacterId.Value);
        if (pc == null) return;
        _db.Pf2eEncounterCombatants.UpdateHp(_combatant.Id, pc.CurrentHp, "Sync from sheet");
        _combatant = _db.Pf2eEncounterCombatants.Get(_combatant.Id);
        if (_combatant == null) return;
        Refresh(false);
        EmitSignal(SignalName.HpChanged, _combatant.Id, _combatant.CurrentHp, "Sync from sheet");
        PopulateHpLog();
    }
}
